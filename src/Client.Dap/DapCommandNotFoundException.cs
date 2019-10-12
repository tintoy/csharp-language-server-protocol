using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised when an DAP request is made for a command not supported by the remote process.
    /// </summary>
    [Serializable]
    public class DapCommandNotFoundException
        : DapRequestException
    {
        /// <summary>
        ///     Create a new <see cref="DapCommandNotFoundException"/>.
        /// </summary>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        /// <param name="command">
        ///     The name of the target command.
        /// </param>
        public DapCommandNotFoundException(string command, int? requestId)
            : base($"Method not found: '{command}'.", requestId, DapErrorCodes.CommandNotFound)
        {
            Command = !string.IsNullOrWhiteSpace(command) ? command : "(unknown)";
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
        protected DapCommandNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Command = info.GetString(nameof(Command));
        }

        /// <summary>
        ///     Get exception data for serialisation.
        /// </summary>
        /// <param name="info">
        ///     The serialisation data-store.
        /// </param>
        /// <param name="context">
        ///     The serialisation streaming context.
        /// </param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(Command), Command);
        }

        /// <summary>
        ///     The name of the command that was not supported by the remote process.
        /// </summary>
        public string Command { get; }
    }
}
