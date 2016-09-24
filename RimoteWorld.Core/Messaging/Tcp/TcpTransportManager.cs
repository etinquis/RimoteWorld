using Polenter.Serialization;
using RimoteWorld.Core.Messaging.Tcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RimoteWorld.Core.Messaging.Tcp
{
    public class TcpTransportManager
    {
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
            private readonly object _queueLock = new object();
            private readonly Queue<PendingTcpWrite> _pendingWrites = new Queue<PendingTcpWrite>();
            private readonly ManualResetEvent _hasPendingWrites = new ManualResetEvent(false);
            private readonly Thread _thread = new Thread(WriteThreadProc);
            private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);

            public void Start()
            {
                _thread.Start(this);
            }

            public void ShutDown()
            {
                _shutdownEvent.Set();
                _thread.Join();
            }

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

            private List<PendingTcpWrite> DequeueAll(TimeSpan timeout)
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

            private static void WriteThreadProc(object istate)
            {
                var state = (WriteQueue)istate;

                while (!state._shutdownEvent.WaitOne(0))
                {
                    var pendingWrites = state.DequeueAll(TimeSpan.FromSeconds(1));

                    foreach (var pendingWrite in pendingWrites)
                    {
                        if (state._shutdownEvent.WaitOne(0)) return;

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
        }

        private class MonitoredClientList
        {
            private readonly object _clientListLock = new object();
            private readonly object _clientListProcessLock = new object();
            private readonly List<MonitoredClient> _clientList = new List<MonitoredClient>();
            private readonly ManualResetEvent _clientAdded = new ManualResetEvent(false);
            private readonly Thread _thread = new Thread(ReadThreadProc);
            private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);

            public void Start()
            {
                _thread.Start(this);
            }

            public void ShutDown()
            {
                _shutdownEvent.Set();
                _thread.Join();
            }

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

            private void ForEachAvailable(Action<MonitoredClient> clientAction)
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

            private bool WaitForNonEmptyAndAvailable(TimeSpan timeout)
            {
                return RemoveDisconnectedClients().Any(c => c.Client.Available > 0) || _clientAdded.WaitOne(timeout);
            }

            private static void ReadThreadProc(object istate)
            {
                var state = (MonitoredClientList)istate;

                while (!state._shutdownEvent.WaitOne(0))
                {
                    if (state.WaitForNonEmptyAndAvailable(TimeSpan.FromSeconds(1)))
                    {
                        state.ForEachAvailable((client) =>
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

        public void Start()
        {
            _clientList.Start();
            _writeQueue.Start();
        }

        public void Shutdown()
        {
            _writeQueue.ShutDown();
            _clientList.ShutDown();
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
    }
}
