using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using RimoteWorld.Core;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Concurrent;
using RimoteWorld.Core.Messaging.Instancing;
using RimoteWorld.Core.Messaging.Tcp;

namespace RimoteWorld.Client
{
    internal class RPCClient : IDisposable
    {
        private static ConcurrentDictionary<ulong, Action<Result<ResponseMessage>>> _pendingResponses = new ConcurrentDictionary<ulong, Action<Result<ResponseMessage>>>();

        private TcpClient _socket;
        private TcpTransportManager _manager;

        private RPCClient(TcpClient client, TcpTransportManager manager)
        {
            _socket = client;
            _manager = manager;
        }

        public static async Task<RPCClient> Connect(string host, int port)
        {
            var manager = new TcpTransportManager();
            var client = new TcpClient();
            manager.MonitorClientForMessages(client, RecievedMessageFromServer);

            while (true)
            {
                try
                {
                    await client.ConnectAsync(host, port);
                    manager.Start();
                    return new RPCClient(client, manager);
                }
                catch (Exception ex)
                {
                    
                }
            }
        }

        public void Dispose()
        {
            _manager.Shutdown();
            _socket.Close();
            foreach (var pendingResponse in _pendingResponses)
            {
                pendingResponse.Value.Invoke(new Exception());
            }
            _pendingResponses.Clear();
        }

        private static void RecievedMessageFromServer(TcpClient client, Result<Message> messageResult)
        {
            try
            {
                var message = messageResult.GetValueOrThrow();
                if (message is ResponseMessage)
                {
                    var originalRequest = (message as ResponseMessage).OriginalMessage;
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

                _manager.PostMessageToAsync(sentMessage, _socket, postResult =>
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

                _manager.PostMessageToAsync(sentMessage, _socket, postResult =>
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
