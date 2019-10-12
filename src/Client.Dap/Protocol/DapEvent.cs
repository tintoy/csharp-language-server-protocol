using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     DAP message representing a client or debug adapter initiated request.
    /// </summary>
    public sealed class DapEvent
        : DapMessage
    {
        /// <summary>
        ///     Create a new <see cref="DapEvent"/>.
        /// </summary>
        public DapEvent()
        {
        }

        /// <summary>
        ///     The event type.
        /// </summary>
        [JsonProperty("event", Order = BasePropertyOrdinal + 0)]
        public string Event { get; set; }

        /// <summary>
        ///     Event-specific information.
        /// </summary>
        [JsonProperty("body", Order = BasePropertyOrdinal + 1, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public JToken Body { get; set; }

        /// <summary>
        ///     Get a <see cref="DapMessageType"/> value indicating the type of serialised message.
        /// </summary>
        /// <returns>
        ///     The serialised message type.
        /// </returns>
        protected override DapMessageType GetMessageType() => DapMessageType.Event;
    }
}
