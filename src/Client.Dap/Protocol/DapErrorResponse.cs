using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     Factory functions for <see cref="DapResponse"/>s representing error conditions.
    /// </summary>
    public static class DapErrorResponse
    {
        public static DapResponse CommandNotFound(DapRequest request) => Create(request, $"Command not found: '{request?.Command}'", DapErrorCodes.CommandNotFound);
        public static DapResponse InvalidArguments(DapRequest request) => Create(request, "Invalid arguments", DapErrorCodes.InvalidArguments);

        public static DapResponse HandlerError(DapRequest request, Exception handlerError)
        {
            if ( request == null )
                throw new ArgumentNullException(nameof(request));

            if ( handlerError == null )
                throw new ArgumentNullException(nameof(handlerError));

            return Create(request,
                errorMessage: $"Error processing request: {handlerError.Message}",
                errorCode: 500,
                configureBody: body =>
                {
                    body.Add("data",
                        handlerError.ToString()
                    );
                }
            );
        }

        public static DapResponse Create(DapRequest request, string errorMessage, int errorCode, Action<JObject> configureBody = null)
        {
            if ( request == null )
                throw new ArgumentNullException(nameof(request));

            if ( string.IsNullOrWhiteSpace(errorMessage) )
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(errorMessage)}.", nameof(errorMessage));

            var response = new DapResponse
            {
                RequestId = request.Id,
                Command = request.Command,
                Message = errorMessage,
                Body = new JObject(
                    new JProperty("code", errorCode)
                )
            };

            if (configureBody != null)
            {
                configureBody(
                    (JObject) response.Body
                );
            }

            return response;
        }
    }
}
