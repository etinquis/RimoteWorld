using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using RimoteWorld.Core;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Concurrent;
using RimoteWorld.Core.Messaging.Instancing;

namespace RimoteWorld.Client
{
    internal class RPCClient : IDisposable
    {
        private static ConcurrentDictionary<ulong, Action<Result<ResponseMessage>>> _pendingResponses = new ConcurrentDictionary<ulong, Action<Result<ResponseMessage>>>();

        private Lazy<TcpClient> Socket;
        private Lazy<TcpTransportManager> Manager;

        public RPCClient()
        {
            Socket = new Lazy<TcpClient>(() =>
            {
                var client = new TcpClient();
                client.Connect("localhost", 40123);
                return client;
            });

            Manager = new Lazy<TcpTransportManager>(() =>
            {
                var transportManager = new TcpTransportManager();
                transportManager.MonitorClientForMessages(Socket.Value, RecievedMessageFromServer);
                transportManager.Start();
                return transportManager;
            });
        }

        public void Dispose()
        {
            Manager.Value.Shutdown();
            Socket.Value.Close();
        }

        internal void Connect()
        {
            var mgr = Manager.Value;
        }

        private static void RecievedMessageFromServer(TcpClient client, Result<Message> messageResult)
        {
            try
            {
                var message = messageResult.GetValueOrThrow();
                if (message is ResponseMessage)
                {
                    var originalRequest = (message as ResponseMessage).OriginalRequestMessage;
                    Action<Result<ResponseMessage>> pendingAction;
                    if (_pendingResponses.TryRemove(originalRequest.ID, out pendingAction))
                    {
                        pendingAction(message as ResponseMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _pendingResponses.Clear();
            }
        }

        public async Task MakeRemoteStaticCall<TSource>(Expression<Action<TSource>> expr)
        {
            var memExpr = expr.Body as MemberExpression;
            var methodExpr = expr.Body as MethodCallExpression;

            Type declaringType = null;
            string remoteCall = null;
            Type requestMessageType = null;
            RequestMessage sentMessage = null;
            if (memExpr != null)
            {

            }
            else if (methodExpr != null)
            {
                declaringType = methodExpr.Method.DeclaringType;
                remoteCall = methodExpr.Method.Name;
                var parameters = methodExpr.Method.GetParameters();
                var arguments =
                    methodExpr.Arguments.OfType<MemberExpression>()
                        .Select(exp => Expression.Lambda(exp).Compile().DynamicInvoke()).ToArray();

                if (arguments.Length != parameters.Length)
                {
                    throw new NotImplementedException("Can't prepare all arguments for serialization");
                }

                switch (arguments.Length)
                {
                    case 0:
                    {
                        requestMessageType = typeof(RequestMessage<>).MakeGenericType(declaringType);
                        sentMessage = (RequestMessage) Activator.CreateInstance(requestMessageType);
                        break;
                    }
                    case 1:
                    {
                        requestMessageType = typeof(RequestMessageWithArguments<,>).MakeGenericType(declaringType,
                            arguments[0].GetType());
                        sentMessage = (RequestMessage) Activator.CreateInstance(requestMessageType);
                        requestMessageType.GetProperty("Argument1").SetValue(sentMessage, arguments[0]);
                        break;
                    }
                }
            }


            TaskCompletionSource<Result<Message>> completionSource = new TaskCompletionSource<Result<Message>>();

            {
                //var requestMessageType = typeof(RequestMessage<>).MakeGenericType(declaringType);
                //var sentMessage = (RequestMessage)Activator.CreateInstance(requestMessageType);

                var staticInstanceLocatorType = typeof(StaticInstanceLocator<>).MakeGenericType(declaringType);
                var staticIntanceLocator = (InstanceLocator)Activator.CreateInstance(staticInstanceLocatorType, null);
                requestMessageType.GetProperty("InstanceLocator").SetValue(sentMessage, staticIntanceLocator);

                sentMessage.RemoteCall = remoteCall;

                Manager.Value.PostMessageToAsync(sentMessage, Socket.Value, postResult =>
                {
                    try
                    {
                        var postedMessage = postResult.GetValueOrThrow();
                        _pendingResponses.TryAdd(postedMessage.ID, (responseResult) =>
                        {
                            try
                            {
                                completionSource.SetResult(responseResult.GetValueOrThrow());
                            }
                            catch (Exception ex)
                            {
                                completionSource.SetException(ex);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        completionSource.SetException(ex);
                    }
                });
            }

            var messageResult = await completionSource.Task.ConfigureAwait(false);

            var baseMessage = messageResult.GetValueOrThrow();
            if (baseMessage is ResponseWithErrorMessage)
            {
                throw new Exception((baseMessage as ResponseWithErrorMessage).ErrorMessage);
            }
            var message = (ResponseMessage<TSource>)baseMessage;
        }

        public async Task<TResult> MakeRemoteStaticCall<TSource, TResult>(Expression<Func<TSource, TResult>> expr)
        {
            var memExpr = expr.Body as MemberExpression;
            var methodExpr = expr.Body as MethodCallExpression;

            Type declaringType = null;
            string remoteCall = null;
            if (memExpr != null)
            {

            }
            else if (methodExpr != null)
            {
                declaringType = methodExpr.Method.DeclaringType;
                remoteCall = methodExpr.Method.Name;
                var parameterTypes = methodExpr.Method.GetParameters().Select(param => param.ParameterType).ToArray();
            }

            TaskCompletionSource<Result<Message>> completionSource = new TaskCompletionSource<Result<Message>>();

            {
                var requestMessageType = typeof(RequestMessage<>).MakeGenericType(declaringType);
                var sentMessage = (RequestMessage) Activator.CreateInstance(requestMessageType);

                var staticInstanceLocatorType = typeof(StaticInstanceLocator<>).MakeGenericType(declaringType);
                var staticIntanceLocator = (InstanceLocator) Activator.CreateInstance(staticInstanceLocatorType, null);
                requestMessageType.GetProperty("InstanceLocator").SetValue(sentMessage, staticIntanceLocator);

                sentMessage.RemoteCall = remoteCall;

                Manager.Value.PostMessageToAsync(sentMessage, Socket.Value, postResult =>
                {
                    try
                    {
                        var postedMessage = postResult.GetValueOrThrow();
                        _pendingResponses.TryAdd(postedMessage.ID, (responseResult) =>
                        {
                            try
                            {
                                completionSource.SetResult(responseResult.GetValueOrThrow());
                            }
                            catch (Exception ex)
                            {
                                completionSource.SetException(ex);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        completionSource.SetException(ex);
                    }
                });
            }

            var messageResult = await completionSource.Task.ConfigureAwait(false);

            var baseMessage = messageResult.GetValueOrThrow();
            if (baseMessage is ResponseWithErrorMessage)
            {
                throw new Exception((baseMessage as ResponseWithErrorMessage).ErrorMessage);
            }
            var message = (ResponseWithResultMessage<TSource, TResult>)baseMessage;
            return message.Result;
        }
    }
}
