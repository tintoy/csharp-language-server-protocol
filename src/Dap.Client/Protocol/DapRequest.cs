using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     DAP message representing a client or debug adapter initiated request.
    /// </summary>
    public sealed class DapRequest
        : DapMessage
    {
        /// <summary>
        ///     Create a new <see cref="DapRequest"/>.
        /// </summary>
        public DapRequest()
        {
        }

        /// <summary>
        ///     The command to execute.
        /// </summary>
        [JsonProperty("command", Order = BasePropertyOrdinal + 0)]
        public string Command { get; set; }

        /// <summary>
        ///     Arguments (if any) for the command
        /// </summary>
        [JsonProperty("arguments", Order = BasePropertyOrdinal + 1, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public JToken Arguments { get; set; }

        /// <summary>
        ///     Get a <see cref="DapMessageType"/> value indicating the type of serialised message.
        /// </summary>
        /// <returns>
        ///     The serialised message type.
        /// </returns>
        protected override DapMessageType GetMessageType() => DapMessageType.Request;
    }
}
