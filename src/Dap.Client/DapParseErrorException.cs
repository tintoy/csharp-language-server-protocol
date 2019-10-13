using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised when an DAP request could not be parsed.
    /// </summary>
    [Serializable]
    public class DapParseErrorException
        : DapRequestException
    {
        /// <summary>
        ///     Create a new <see cref="DapParseErrorException"/>.
        /// </summary>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        public DapParseErrorException(int? requestId)
            : base("Error parsing request.", requestId, DapErrorCodes.ParseError)
        {
        }

        /// <summary>
        ///     Serialisation constructor.
        /// </summary>
        /// <param name="info">
        ///     The serialisation data-store.
        /// </param>
        /// <param name="context">
        ///     The serialisation streaming context.
        /// </param>
        protected DapParseErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
