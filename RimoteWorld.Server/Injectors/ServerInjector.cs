using CommunityCoreLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Polenter.Serialization;
using RimoteWorld.Core;
using System.Threading;
using System.Reflection;
using RimoteWorld.Core.Extensions;
using RimoteWorld.Core.Messaging.Instancing;

namespace RimoteWorld.Server.Injectors
{
    public class ServerInjector : SpecialInjector
    {
        private object _initLock = new object();
        private bool _initialized = false;

        private TcpListener _server = null;
        private TcpTransportManager _manager = null;

        private object _staticAPIsLock = new object();
        private Dictionary<Type, object> _staticAPIs = new Dictionary<Type, object>();

        public override bool Inject()
        {
            Log.Info("Version 0.15.0");

            lock (_initLock)
            {
                if (!_initialized)
                {
                    _initialized = true;

                    _staticAPIs.Add(typeof(Core.API.IServerAPI), new API.ServerAPI());
                    _staticAPIs.Add(typeof(Core.API.UI.IMainMenuAPI), new API.UI.MainMenuAPI());

                    _server = new TcpListener(IPAddress.Parse("127.0.0.1"), 40123);
                    _manager = new TcpTransportManager();

                    _manager.Start();

                    Log.Debug("Starting Server");
                    _server.Start();

                    Log.Debug("Awaiting connections");
                    _server.BeginAcceptTcpClient(ClientConnected, this);
                }
            }

            Log.Debug("Injection complete");
            return true;
        }

        private static void ClientConnected(IAsyncResult result)
        {
            Log.Debug("Client Connected");

            var server = (ServerInjector)result.AsyncState;
            var client = server._server.EndAcceptTcpClient(result);
            server._manager.MonitorClientForMessages(client, (c, r) => ReceivedMessageFromClient(server, c, r));

            server._server.BeginAcceptTcpClient(ClientConnected, server);
        }

        private static void ReceivedMessageFromClient(ServerInjector server, TcpClient client, Result<Message> messageResult)
        {
            try
            {
                var message = messageResult.GetValueOrThrow();

                if (message is RequestMessage)
                {
                    var requestMessage = message as RequestMessage;
                    Log.Debug(string.Format("Message received {0}::{1}", requestMessage.TypeName, requestMessage.RemoteCall));

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var apiType = requestMessage.APIType;

                        object instance = null;
                        {
                            var instanceLocatorProp = requestMessage.GetType().GetProperty("InstanceLocator");
                            if (instanceLocatorProp != null)
                            {
                                var instanceLocator =
                                    (InstanceLocator) instanceLocatorProp.GetValue(requestMessage, null);
                                var staticInstanceLocatorType =
                                    typeof(StaticInstanceLocator<>).MakeGenericType(apiType);

                                if (instanceLocator != null &&
                                    instanceLocator.GetType()
                                        .Equals(staticInstanceLocatorType))
                                {
                                    instance = server._staticAPIs[instanceLocator.InstanceType];
                                }
                                else
                                {
                                    // handle non-static instance locator
                                    Log.Error("Non-static instance locators not implemented");
                                }
                            }
                        }

                        if (instance != null)
                        {
                            var method = apiType.GetMethod(requestMessage.RemoteCall);
                            if (method != null)
                            {
                                Log.Debug(string.Format("Found matching method in api type: {0}::{1}", apiType.Name, method.Name));
                                Log.Debug(string.Format("Method takes {0} parameters", method.GetParameters().Length));

                                object result = null;
                                try
                                {
                                    var expectedParameters = method.GetParameters();
                                    if (expectedParameters.Length > 0)
                                    {
                                        Type messageWithArgsType =
                                            typeof(RequestMessageWithArguments<>).MakeGenericType(apiType);
                                        var args = (object[])messageWithArgsType.GetProperty("Arguments").GetValue(requestMessage, null);
                                        var argTypes = (Type[])messageWithArgsType.GetProperty("ArgumentTypes").GetValue(requestMessage, null);
                                        Log.Debug(
                                            string.Format(
                                                "Invoking with {0} arguments with types [{1}] and values [{2}]",
                                                args.Length,
                                                string.Join(", ", argTypes.Select(typ => typ.Name).ToArray()),
                                                string.Join(", ", args.Select(arg => arg.ToString()).ToArray())));
                                        result = method.Invoke(instance, args);
                                    }
                                    else
                                    {
                                        Log.Debug(string.Format("Invoking without arguments"));
                                        result = method.Invoke(instance, null);
                                    }

                                    if (result != null)
                                    {
                                        Log.Debug(string.Format("Invoked method and got {0} of type {1}", result,
                                            result.GetType()));

                                        var responseType =
                                            typeof(ResponseWithResultMessage<,>).MakeGenericType(
                                                requestMessage.APIType, method.ReturnType);
                                        var responseMessage =
                                            (ResponseMessage)
                                                Activator.CreateInstance(responseType, new object[] {result});
                                        responseMessage.OriginalRequestMessage = requestMessage;

                                        server._manager.PostMessageToAsync(responseMessage, client);
                                    }
                                    else if (method.ReturnType.IsNullable())
                                    {
                                        Log.Debug("Invoked method and got null for nullable return type");
                                        var responseType =
                                            typeof(ResponseWithResultMessage<,>).MakeGenericType(
                                                requestMessage.APIType, method.ReturnType);
                                        var responseMessage =
                                            (ResponseMessage)
                                                Activator.CreateInstance(responseType, new object[] {result});
                                        responseMessage.OriginalRequestMessage = requestMessage;

                                        server._manager.PostMessageToAsync(responseMessage, client);
                                    }
                                    else if (method.ReturnType == typeof(void))
                                    {
                                        Log.Debug("Invoked method and got null for void return type");
                                        var responseType =
                                            typeof(ResponseMessage<>).MakeGenericType(
                                                requestMessage.APIType);
                                        var responseMessage =
                                            (ResponseMessage)
                                                Activator.CreateInstance(responseType, null);
                                        responseMessage.OriginalRequestMessage = requestMessage;

                                        server._manager.PostMessageToAsync(responseMessage, client);
                                    }
                                    else
                                    {
                                        Log.Error("Method invocation failed");
                                        var errorMessage = new ResponseWithErrorMessage()
                                        {
                                            ErrorMessage = "Method invocation failed",
                                            OriginalMessage = message
                                        };
                                        server._manager.PostMessageToAsync(errorMessage, client);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("Exception building response", ex);
                                    var errorMessage = new ResponseWithErrorMessage()
                                    {
                                        ErrorMessage = string.Format("Exception building response (ex: {0})", ex),
                                        OriginalMessage = message
                                    };
                                    server._manager.PostMessageToAsync(errorMessage, client);
                                }
                            }
                            else
                            {
                                Log.Error("Could not find suitable method in api type");
                                var errorMessage = new ResponseWithErrorMessage()
                                {
                                    ErrorMessage = "Could not find suitable method in api type",
                                    OriginalMessage = message
                                };
                                server._manager.PostMessageToAsync(errorMessage, client);
                            }
                        }
                        else
                        {
                            Log.Error("Could not find suitable instance using given locator");
                            var errorMessage = new ResponseWithErrorMessage()
                            {
                                ErrorMessage = "Could not find suitable instance using given locator",
                                OriginalMessage = message
                            };
                            server._manager.PostMessageToAsync(errorMessage, client);
                        }
                        //else
                        //{
                        //server._manager.PostMessageToAsync(
                        //new ResponseWithResultMessage<GlobalState>()
                        //{
                        //Result = new GlobalState(),
                        //OriginalRequest = requestMessage
                        //}, client);
                        //}
                    });
                    return;
                }

                {
                    var errorMsg = string.Format("Message Type not implemented on server {0}", message.GetType());
                    Log.Error(errorMsg);
                    var errorMessage = new ResponseWithErrorMessage()
                    {
                        ErrorMessage = errorMsg,
                        OriginalMessage = message
                    };
                    server._manager.PostMessageToAsync(errorMessage, client);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error receiving message from client", ex);
            }
        }
    }
}
