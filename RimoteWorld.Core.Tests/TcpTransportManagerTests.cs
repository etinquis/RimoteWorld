using NUnit.Framework;
using RimoteWorld.Core.Messaging.Instancing;
using RimoteWorld.Core.Messaging.Tcp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RimoteWorld.Core.Tests
{
    [TestFixture]
    class TcpTransportManagerTests
    {
        private static readonly IPAddress boundIP = IPAddress.Parse("127.0.0.1");
        private static readonly int boundPort = 40123;

        protected class ServerDef
        {
            public TcpListener TcpListener = new TcpListener(boundIP, boundPort);
            public TcpTransportManager Manager = new TcpTransportManager();
        }

        protected class ClientDef
        {
            public TcpClient FromClientToServer = new TcpClient();
            public TcpTransportManager Manager = new TcpTransportManager();
        }

        [TestFixture]
        class WithServerStarted : TcpTransportManagerTests
        {
            protected ServerDef Server = null;
            protected ConcurrentQueue<Result<Message>> ServerRecievedMessages = new ConcurrentQueue<Result<Message>>();
            protected Semaphore ServerRecievedMessageSem = new Semaphore(0, int.MaxValue);

            [SetUp]
            public void SetUp()
            {
                Server = new ServerDef();
                Server.TcpListener.Start();
                Server.Manager.Start();
            }

            [TearDown]
            public void TearDown()
            {
                Server.TcpListener.Stop();
                Server.Manager.Shutdown();
            }

            class WithClientConnected : WithServerStarted
            {
                protected ClientDef Client = null;
                protected ConcurrentQueue<Result<Message>> ClientRecievedMessages = new ConcurrentQueue<Result<Message>>();
                protected Semaphore ClientRecievedMessageSem = new Semaphore(0, int.MaxValue);
                protected TcpClient FromServerToClient = null;

                class TestAPI
                {
                    public void TestMethod()
                    {
                        
                    }
                }

                [SetUp]
                public void SetUp()
                {
                    Client = new ClientDef();
                    Client.FromClientToServer.Connect(boundIP, boundPort);
                    Client.Manager.MonitorClientForMessages(Client.FromClientToServer,
                        (client, res) =>
                        {
                            ClientRecievedMessages.Enqueue(res);
                            ClientRecievedMessageSem.Release();
                        });
                    Client.Manager.Start();

                    FromServerToClient = Server.TcpListener.AcceptTcpClient();
                    Server.Manager.MonitorClientForMessages(FromServerToClient,
                        (client, result) =>
                        {
                            ServerRecievedMessages.Enqueue(result);
                            ServerRecievedMessageSem.Release();
                        });
                }

                [TearDown]
                public void TearDown()
                {
                    Client.FromClientToServer.Close();
                    FromServerToClient.Close();

                    Client.Manager.Shutdown();
                }

                [Test]
                public void ClientCanPostMessageToServer()
                {
                    Client.Manager.PostMessageToAsync(new RequestMessage<TestAPI>()
                    {
                        InstanceLocator = new StaticInstanceLocator<TestAPI>(),
                        RemoteCall = "TestMethod"
                    }, Client.FromClientToServer);

                    Assert.That(ServerRecievedMessageSem.WaitOne(TimeSpan.FromMilliseconds(300)), Is.True,
                        "Timed out waiting for server to receive message");

                    Result<Message> receivedMessage = null;
                    Assert.That(ServerRecievedMessages.TryDequeue(out receivedMessage), Is.True);
                    Assert.That(receivedMessage.GetValueOrThrow, Throws.Nothing);

                    var message = receivedMessage.GetValueOrThrow();
                    Assert.That(message.ID, Is.EqualTo(1));
                    Assert.That(message, Is.InstanceOf<RequestMessage>());
                    var requestMessage = (RequestMessage) message;
                    Assert.That(requestMessage.APIType, Is.EqualTo(typeof(TestAPI)));
                    Assert.That(requestMessage.TypeName, Is.EqualTo(typeof(TestAPI).Name));
                    Assert.That(requestMessage.RemoteCall, Is.EqualTo("TestMethod"));
                }

                [Test]
                public void ServerCanPostMessageToClient()
                {
                    Server.Manager.PostMessageToAsync(new RequestMessage<TestAPI>()
                    {
                        InstanceLocator = new StaticInstanceLocator<TestAPI>(),
                        RemoteCall = "TestMethod"
                    }, FromServerToClient);

                    Assert.That(ClientRecievedMessageSem.WaitOne(TimeSpan.FromMilliseconds(300)), Is.True,
                        "Timed out waiting for server to receive message");

                    Result<Message> receivedMessage = null;
                    Assert.That(ClientRecievedMessages.TryDequeue(out receivedMessage), Is.True);
                    Assert.That(receivedMessage.GetValueOrThrow, Throws.Nothing);

                    var message = receivedMessage.GetValueOrThrow();
                    Assert.That(message.ID, Is.EqualTo(1));
                    Assert.That(message, Is.InstanceOf<RequestMessage>());
                    var requestMessage = (RequestMessage)message;
                    Assert.That(requestMessage.APIType, Is.EqualTo(typeof(TestAPI)));
                    Assert.That(requestMessage.TypeName, Is.EqualTo(typeof(TestAPI).Name));
                    Assert.That(requestMessage.RemoteCall, Is.EqualTo("TestMethod"));
                }


                [Test]
                public void RequestResponseRoundTrip()
                {
                    Client.Manager.PostMessageToAsync(new RequestMessage<TestAPI>()
                    {
                        InstanceLocator = new StaticInstanceLocator<TestAPI>(),
                        RemoteCall = "TestMethod"
                    }, Client.FromClientToServer);

                    {
                        // server handling
                        Assert.That(ServerRecievedMessageSem.WaitOne(TimeSpan.FromMilliseconds(800)), Is.True,
                            "Timed out waiting for server to receive message");

                        Result<Message> receivedMessageOnServer = null;
                        Assert.That(ServerRecievedMessages.TryDequeue(out receivedMessageOnServer), Is.True);
                        var messageOnServer = receivedMessageOnServer.GetValueOrThrow();
                        Assert.That(messageOnServer, Is.InstanceOf<RequestMessage>());
                        var requestMessageOnServer = (RequestMessage) messageOnServer;

                        Server.Manager.PostMessageToAsync(new ResponseWithResultMessage<TestAPI, bool>
                        {
                            OriginalMessage = requestMessageOnServer,
                            Result = true
                        }, FromServerToClient);
                    }

                    { // client handling
                        Assert.That(ClientRecievedMessageSem.WaitOne(TimeSpan.FromMilliseconds(800)), Is.True,
                        "Timed out waiting for server to receive message");

                        Result<Message> clientReceivedMessage = null;
                        Assert.That(ClientRecievedMessages.TryDequeue(out clientReceivedMessage), Is.True);
                        Assert.That(clientReceivedMessage.GetValueOrThrow, Throws.Nothing);

                        var messageOnClient = clientReceivedMessage.GetValueOrThrow();
                        Assert.That(messageOnClient.ID, Is.EqualTo(1));
                        Assert.That(messageOnClient, Is.InstanceOf<ResponseWithResultMessage<TestAPI, bool>>());
                        var responseMessage = (ResponseWithResultMessage<TestAPI, bool>)messageOnClient;
                        Assert.That(responseMessage.Result, Is.True);
                        Assert.That(responseMessage.OriginalMessage, Is.InstanceOf<RequestMessage<TestAPI>>());
                        Assert.That((responseMessage.OriginalMessage as RequestMessage<TestAPI>).APIType, Is.EqualTo(typeof(TestAPI)));
                        Assert.That((responseMessage.OriginalMessage as RequestMessage<TestAPI>).TypeName, Is.EqualTo(typeof(TestAPI).Name));
                        Assert.That((responseMessage.OriginalMessage as RequestMessage<TestAPI>).RemoteCall, Is.EqualTo("TestMethod"));
                    }
                }
            }

            [Test]
            public void CanConnectNewClient()
            {
                var client = new ClientDef();
                client.FromClientToServer.Connect(boundIP, boundPort);

                var tcpClient = Server.TcpListener.AcceptTcpClient();
            }
        }
    }
}
