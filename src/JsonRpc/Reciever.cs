using System.Collections.Generic;
using System.Linq;
using JsonRpc.Server;
using JsonRpc.Server.Messages;
using Newtonsoft.Json.Linq;
using Serilog;

namespace JsonRpc
{
    public class Receiver : IReciever
    {
        public bool IsValid(JToken container)
        {
            // request must be an object or array
            if (container is JObject)
            {
                return true;
            }

            if (container is JArray array)
            {
                return array.Count > 0;
            }

            return false;
        }

        public virtual (IEnumerable<Renor> results, bool hasResponse) GetRequests(JToken container)
        {
            var results = new List<Renor>();

            if (container is JArray)
            {
                results.AddRange(container.Select(GetRenor));
            }
            else
            {
                results.Add(GetRenor(container));
            }
            return (results, results.Any(z => z.IsResponse));
        }

        protected virtual Renor GetRenor(JToken @object)
        {
            Log.Verbose("GetRenor: Identify JSON {Json}", @object.ToString());

            if (!(@object is JObject request))
            {
                Log.Verbose("GetRenor: JSON is not an object.");

                return new InvalidRequest(null, "Not an object");
            }

            var protocol = request["jsonrpc"]?.Value<string>();
            if (protocol != "2.0")
            {
                Log.Verbose("GetRenor: Unexpected protocol {Protocol}.", protocol);

                return new InvalidRequest(null, "Unexpected protocol");
            }

            object requestId = null;
            bool hasRequestId;
            if (hasRequestId = request.TryGetValue("id", out var id) && id != null)
            {
                var idString = id.Type == JTokenType.String ? (string)id : null;
                var idLong = id.Type == JTokenType.Integer ? (long?)id : null;
                requestId = idString ?? (idLong.HasValue ? (object)idLong.Value : null);
            }

            Log.Verbose("GetRenor: RequestId={RequestId} (HasRequestId={HasRequestId}).", requestId, hasRequestId);

            if (hasRequestId && request.TryGetValue("result", out var response) && response != null)
            {
                Log.Verbose("GetRenor: JSON represents a response (RequestId={RequestId}).", requestId);

                return new Response(requestId, response);
            }

            if (hasRequestId && request.TryGetValue("error", out var errorResponse) && errorResponse != null)
            {
                Log.Verbose("GetRenor: JSON represents an error response {ErrorResponse} (RequestId={RequestId}).", errorResponse.ToString(), requestId);

                return new Response(requestId, errorResponse.ToString());
            }

            var method = request["method"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(method))
            {
                Log.Verbose("GetRenor: JSON is missing 'method' property.");

                return new InvalidRequest(requestId, "Method not set");
            }

            var hasParams = request.TryGetValue("params", out var @params) && @params != null;
            if (hasParams && @params.Type != JTokenType.Array && @params.Type != JTokenType.Object)
            {
                Log.Verbose("GetRenor: JSON has invalid 'params' property {Params}.", @params.ToString());

                return new InvalidRequest(requestId, "Invalid params");
            }

            // id == request
            // !id == notification
            if (!hasRequestId)
            {
                Log.Verbose("GetRenor: JSON represents a notification (Params={Params}).", @params?.ToString());
                
                return new Notification(method, @params);
            }
            else
            {
                Log.Verbose("GetRenor: JSON represents a request (RequestId={RequestId}, Params={Params}).", requestId, @params.ToString());

                return new Request(requestId, method, @params);
            }
        }
    }
}
