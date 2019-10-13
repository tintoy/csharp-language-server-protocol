using Newtonsoft.Json;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    /// Request arguments for a DAP cancellation request.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class DapCancellationArguments
    {
        /// <summary>
        ///     Create new <see cref="DapCancellationArguments"/>.
        /// </summary>
        public DapCancellationArguments()
        {
        }

        /// <summary>
        ///     The <see cref="DapMessage.Id"/> of the request to cancel.
        /// </summary>
        [JsonProperty("requestId")]
        public int RequestId { get; set; }
    }
}
