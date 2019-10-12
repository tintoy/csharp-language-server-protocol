using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised an DAP request is invalid.
    /// </summary>
    [Serializable]
    public class DapInvalidRequestException
        : DapRequestException
    {
        /// <summary>
        ///     Create a new <see cref="DapInvalidRequestException"/>.
        /// </summary>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        public DapInvalidRequestException(int? requestId)
            : base("Invalid request.", requestId, DapErrorCodes.InvalidRequest)
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
        protected DapInvalidRequestException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
