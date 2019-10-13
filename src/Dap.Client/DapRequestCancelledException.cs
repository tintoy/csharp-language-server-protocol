using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised when an DAP request is cancelled.
    /// </summary>
    [Serializable]
    public class DapRequestCancelledException
        : DapRequestException
    {
        /// <summary>
        ///     Create a new <see cref="DapRequestCancelledException"/>.
        /// </summary>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        public DapRequestCancelledException(int? requestId)
            : base("Request was cancelled.", requestId, DapErrorCodes.RequestCancelled)
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
        protected DapRequestCancelledException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
