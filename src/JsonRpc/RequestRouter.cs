using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Server;
using JsonRpc.Server.Messages;
using Serilog;

namespace JsonRpc
{
    class RequestRouter : IRequestRouter
    {
        private readonly HandlerCollection _collection;

        public RequestRouter(HandlerCollection collection)
        {
            _collection = collection;
        }

        public IDisposable Add(IJsonRpcHandler handler)
        {
            return _collection.Add(handler);
        }

        public async void RouteNotification(Notification notification)
        {
            var handler = _collection.Get(notification.Method);
            if (handler == null)
            {
                Log.Verbose("No handler registered for notification {NotificationMethod}.", notification.Method);

                return;
            }

            try
            {
                Task result;
                if (handler.Params is null)
                {
                    result = ReflectionRequestHandlers.HandleNotification(handler);
                }
                else
                {
                    var @params = notification.Params.ToObject(handler.Params);
                    result = ReflectionRequestHandlers.HandleNotification(handler, @params);
                }
                await result.ConfigureAwait(false);
            }
            catch (Exception handlerError)
            {
                Log.Error(handlerError, "Unexpected error while handling {NotificationMethod} notification.", notification.Method);
            }
        }


        public Task<ErrorResponse> RouteRequest(Request request)
        {
            return RouteRequest(request, CancellationToken.None);
        }

        protected virtual async Task<ErrorResponse> RouteRequest(Request request, CancellationToken token)
        {
            var handler = _collection.Get(request.Method);

            var method = _collection.Get(request.Method);
            if (method is null)
            {
                Log.Verbose("No handler registered for {RequestMethod} request {RequestId}.", request.Method, request.Id);

                return new MethodNotFound(request.Id);
            }

            try
            {
                Task result;
                if (method.Params is null)
                {
                    result = ReflectionRequestHandlers.HandleRequest(handler, token);
                }
                else
                {
                    object @params;
                    try
                    {
                        @params = request.Params.ToObject(method.Params);
                    }
                    catch
                    {
                        return new InvalidParams(request.Id);
                    }

                    result = ReflectionRequestHandlers.HandleRequest(handler, @params, token);
                }

                await result.ConfigureAwait(false);

                object responseValue = null;
                if (result.GetType().GetTypeInfo().IsGenericType)
                {
                    var property = typeof(Task<>)
                        .MakeGenericType(result.GetType().GetTypeInfo().GetGenericArguments()[0]).GetTypeInfo()
                        .GetProperty(nameof(Task<object>.Result), BindingFlags.Public | BindingFlags.Instance);

                    responseValue = property.GetValue(result);
                }

                return new Client.Response(request.Id, responseValue);
            }
            catch (Exception handlerError)
            {
                Log.Error(handlerError, "Unexpected error while handling {RequestMethod} request {RequestId}.", request.Method, request.Id);

                throw;
            }
        }
    }
}
