using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     A well-known type of Debug Adapter Protocol message.
    /// </summary>
    public enum DapMessageType
    {
        /// <summary>
        ///     An unknown message type.
        /// </summary>
        /// <remarks>
        ///     Used to detect uninitialised values.
        /// </remarks>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        ///     A client or debug adapter initiated request.
        /// </summary>
        [EnumMember(Value = "request")]
        Request = 1,

        /// <summary>
        ///     A debug adapter initiated event.
        /// </summary>
        [EnumMember(Value = "event")]
        Event = 2,

        /// <summary>
        ///     Response for a request.
        /// </summary>
        [EnumMember(Value = "response")]
        Response = 3
    }
}
