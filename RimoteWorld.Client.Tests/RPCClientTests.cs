using NUnit.Framework;
using RimoteWorld.Client;
using RimoteWorld.Core;
using RimoteWorld.Core.Messaging.Tcp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RimoteWorld.Client.Tests
{
    [TestFixture]
    public class RimWorldModTests
    {
        
    }

    [TestFixture]
    public class RPCClientTests
    {
        private static readonly IPAddress boundIP = IPAddress.Parse("127.0.0.1");
        private static readonly int boundPort = 40123;

        protected class ServerDef
        {
            public TcpListener TcpListener = new TcpListener(boundIP, boundPort);
            public TcpTransportManager Manager = new TcpTransportManager();
        }

        protected ServerDef Server = null;
        protected ConcurrentQueue<Result<Message>> ServerRecievedMessages = new ConcurrentQueue<Result<Message>>();
        protected Semaphore ServerRecievedMessageSem = new Semaphore(0, int.MaxValue);
        protected TcpClient FromServerToClient = null;

        [SetUp]
        public void SetUp()
        {
            Server = new ServerDef();
            Server.TcpListener.Start();
            Server.Manager.Start();
            
            FromServerToClient = Server.TcpListener.AcceptTcpClient();
            Server.Manager.MonitorClientForMessages(FromServerToClient,
                (_, result) =>
                {
                    ServerRecievedMessages.Enqueue(result);
                    ServerRecievedMessageSem.Release();
                });
        }

        [TearDown]
        public void TearDown()
        {
            Server.TcpListener.Stop();
            Server.Manager.Shutdown();
        }
    }
}
