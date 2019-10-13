using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     The base class for DAP messages.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public abstract class DapMessage
    {
        /// <summary>
        ///     The first available property ordinal (i.e. <see cref="JsonPropertyAttribute.Order"/> value) available for derived types.
        /// </summary>
        protected const int BasePropertyOrdinal = 3;

        /// <summary>
        ///     Create a new <see cref="DapMessage"/>.
        /// </summary>
        protected DapMessage()
        {
        }

        /// <summary>
        ///     Sequence number (also known as message ID).
        /// </summary>
        /// <remarks>
        ///     For protocol messages of type 'request' this ID can be used to cancel the request.
        /// </remarks>
        [JsonProperty("seq", Order = 1, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int Id { get; set; }

        /// <summary>
        ///     A <see cref="DapMessageType"/> value indicating the type of serialised message.
        /// </summary>
        [JsonProperty("type", Order = 2)]
        public DapMessageType MessageType => GetMessageType();

        /// <summary>
        ///     Get a <see cref="DapMessageType"/> value indicating the type of serialised message.
        /// </summary>
        /// <returns>
        ///     The serialised message type.
        /// </returns>
        protected abstract DapMessageType GetMessageType();
    }
}
