using Polenter.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RimoteWorld.Core
{
    public class TcpTransportManager
    {
        public class TcpWriteError : Exception
        {
            public TcpClient ClientWithError { get; private set; }

            public TcpWriteError(Exception innerException) : base("Tcp Write Error", innerException)
            {
                
            }
        }
        public class TcpReadError : Exception
        {
            public TcpClient ClientWithError { get; private set; }

            public TcpReadError(Exception innerException) : base("Tcp Read Error", innerException)
            {

            }
        }

        private Thread _writeThread = new Thread(WriteThreadProc);
        private Thread _readThread = new Thread(ReadThreadProc);

        private ManualResetEvent _shutdown = new ManualResetEvent(false);
        private WriteQueue _writeQueue = new WriteQueue();
        private MonitoredClientList _clientList = new MonitoredClientList();

        private long _nextMessageId = 0;

        private struct PendingTcpWrite
        {
            public TcpClient Client;
            public MemoryStream FromStream;
            public Action<Result<TcpClient, TcpWriteError>> Callback;
        }

        private struct MonitoredClient
        {
            public TcpClient Client;
            public Action<TcpClient, Result<Message>> Callback;
        }

        private class WriteQueue
        {
            private object _queueLock = new object();
            private Queue<PendingTcpWrite> _pendingWrites = new Queue<PendingTcpWrite>();
            private ManualResetEvent _hasPendingWrites = new ManualResetEvent(false);

            public void Enqueue(PendingTcpWrite pendingWrite)
            {
                EnqueueRange(new PendingTcpWrite[] {pendingWrite});
            }

            public void EnqueueRange(IEnumerable<PendingTcpWrite> pendingWrites)
            {
                lock (_queueLock)
                {
                    foreach (var pendingWrite in pendingWrites.ToArray())
                    {
                        _pendingWrites.Enqueue(pendingWrite);
                    }
                }
                _hasPendingWrites.Set();
            }

            public List<PendingTcpWrite> DequeueAll(TimeSpan timeout)
            {
                List<PendingTcpWrite> pendingWrites = new List<PendingTcpWrite>();
                if (_hasPendingWrites.WaitOne(timeout))
                {
                    lock (_queueLock)
                    {
                        pendingWrites.AddRange(_pendingWrites);
                        _pendingWrites.Clear();
                    }
                }
                return pendingWrites;
            }
        }

        private struct WriteThreadState
        {
            public ManualResetEvent ShutdownEvent;
            public WriteQueue WriteQueue;
        }

        private class MonitoredClientList
        {
            private object _clientListLock = new object();
            private object _clientListProcessLock = new object();
            private List<MonitoredClient> _clientList = new List<MonitoredClient>();
            private ManualResetEvent _clientAdded = new ManualResetEvent(false);

            public void Add(MonitoredClient client)
            {
                lock (_clientListLock)
                {
                    if (_clientList.Any(c => c.Client.Equals(client.Client)))
                    {
                        throw new ArgumentException("Attempting to add TcpClient that is already being monitored");
                    }
                    _clientList.Add(client);
                }
                _clientAdded.Set();
            }

            public void Remove(TcpClient client)
            {
                int removed = 0;
                lock (_clientListLock)
                {
                    removed = _clientList.RemoveAll(c => c.Client.Equals(client));
                }
                if (removed > 0)
                {
                    lock (_clientListProcessLock)
                    {
                        // ensures that by the time we exit, the callback associated with the given TcpClient won't be fired anymore
                    }
                }
            }

            private MonitoredClient[] RemoveDisconnectedClients()
            {
                MonitoredClient[] clients = null;
                lock (_clientListLock)
                {
                    _clientList.RemoveAll(c => !c.Client.Connected);
                    clients = _clientList.ToArray();
                }
                return clients;
            }

            public void ForEachAvailable(Action<MonitoredClient> clientAction)
            {
                MonitoredClient[] clients = RemoveDisconnectedClients().Where(c =>
                {
                    try
                    {
                        return c.Client.Available > 0;
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                }).ToArray();
                lock (_clientListProcessLock)
                {
                    foreach (var client in clients)
                    {
                        clientAction(client);
                    }
                }
            }

            public bool WaitForNonEmptyAndAvailable(TimeSpan timeout)
            {
                return RemoveDisconnectedClients().Any(c => c.Client.Available > 0) || _clientAdded.WaitOne(timeout);
            }
        }

        private struct ReadThreadState
        {
            public ManualResetEvent ShutdownEvent;
            public MonitoredClientList ClientList;
        }

        public void Start()
        {
            _writeThread.Start(new WriteThreadState()
            {
                ShutdownEvent = _shutdown,
                WriteQueue = _writeQueue
            });
            _readThread.Start(new ReadThreadState()
            {
                ShutdownEvent = _shutdown,
                ClientList = _clientList
            });
        }

        public void Shutdown()
        {
            _shutdown.Set();
            _writeThread.Join();
            _readThread.Join();
        }

        public void MonitorClientForMessages(TcpClient client, Action<TcpClient, Result<Message>> callback)
        {
            _clientList.Add(new MonitoredClient() {Client = client, Callback = callback});
        }

        public void PostMessageToAsync(Message msg, TcpClient client, Action<Result<Message>> callback = null)
        {
            msg.ID = (ulong)Interlocked.Increment(ref _nextMessageId);
            if (callback == null)
            {
                callback = (_) => { };
            }
            MessageSerializer.SerializeMessageAsync(msg, (result) =>
            {
                try
                {
                    var memoryStream = result.GetValueOrThrow();
                    _writeQueue.Enqueue(new PendingTcpWrite()
                    {
                        Client = client,
                        FromStream = memoryStream,
                        Callback =
                            (c) => callback(c.ContinueWithOrPropogate(tcpClient => msg).ToDefaultResult())
                    });
                }
                catch (Exception ex)
                {
                    callback(ex);
                }
            });
        }
        
        private static void WriteThreadProc(object istate)
        {
            var state = (WriteThreadState)istate;
            
            while (!state.ShutdownEvent.WaitOne(0))
            {
                var pendingWrites = state.WriteQueue.DequeueAll(TimeSpan.FromSeconds(1));

                foreach (var pendingWrite in pendingWrites)
                {
                    if (state.ShutdownEvent.WaitOne(0)) return;

                    try
                    {
                        pendingWrite.FromStream.WriteTo(pendingWrite.Client.GetStream());
                    }
                    catch (Exception ex)
                    {
                        pendingWrite.Callback(new TcpWriteError(ex));
                        continue;
                    }
                    pendingWrite.Callback(pendingWrite.Client);
                }
            }
        }

        private static void ReadThreadProc(object istate)
        {
            var state = (ReadThreadState)istate;

            while (!state.ShutdownEvent.WaitOne(0))
            {
                if (state.ClientList.WaitForNonEmptyAndAvailable(TimeSpan.FromSeconds(1)))
                {
                    state.ClientList.ForEachAvailable((client) =>
                    {
                        var obj = MessageSerializer.DeserializeFromStream(client.Client.GetStream());
                        if (obj is Message)
                        {
                            ThreadPool.QueueUserWorkItem((o) =>
                            {
                                client.Callback(client.Client, o as Message);
                            }, obj);
                        }
                    });
                }
            }
        }
    }
}
