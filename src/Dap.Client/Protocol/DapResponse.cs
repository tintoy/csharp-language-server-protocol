using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     DAP message representing the response for a request.
    /// </summary>
    public sealed class DapResponse
        : DapMessage
    {
        /// <summary>
        ///     Create a new <see cref="DapResponse"/>.
        /// </summary>
        public DapResponse()
        {
        }

        /// <summary>
        ///     The Id (<see cref="DapMessage.Id"/>) of the corresponding request.
        /// </summary>
        [JsonProperty("request_seq", Order = BasePropertyOrdinal + 0)]
        public int RequestId { get; set; }

        /// <summary>
        ///     Outcome of the request.
        /// </summary>
        /// <remarks>
        ///     If <c>true</c>, the request was successful and the 'body' attribute may contain the result of the request.
        /// </remarks>
        [JsonProperty("success", Order = BasePropertyOrdinal + 1)]
        public bool Success { get; set; }

        /// <summary>
        ///     The command requested.
        /// </summary>
        [JsonProperty("command", Order = BasePropertyOrdinal + 2)]
        public string Command { get; set; }

        /// <summary>
        ///     If <see cref="Success"/> is <c>false</c>, contains the raw error in short form (otherwise, <c>null</c>).
        /// </summary>
        [JsonProperty("message", Order = BasePropertyOrdinal + 3, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Message { get; set; }

        /// <summary>
        ///     If <see cref="Success"/> is <c>true</c>, the request result (otherwise, optional error details).
        /// </summary>
        [JsonProperty("body", Order = BasePropertyOrdinal + 4, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public JToken Body { get; set; }

        /// <summary>
        ///     Get a <see cref="DapMessageType"/> value indicating the type of serialised message.
        /// </summary>
        /// <returns>
        ///     The serialised message type.
        /// </returns>
        protected override DapMessageType GetMessageType() => DapMessageType.Response;
    }
}
