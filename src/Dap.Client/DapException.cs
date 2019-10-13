using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised when a Debug Adapter Protocol error is encountered.
    /// </summary>
    [Serializable]
    public class DapException
        : Exception
    {
        /// <summary>
        ///     Create a new <see cref="DapException"/>.
        /// </summary>
        /// <param name="message">
        ///     The exception message.
        /// </param>
        public DapException(string message)
            : base(message)
        {
        }

        /// <summary>
        ///     Create a new <see cref="DapException"/>.
        /// </summary>
        /// <param name="message">
        ///     The exception message.
        /// </param>
        /// <param name="inner">
        ///     The exception that caused this exception to be raised.
        /// </param>
        public DapException(string message, Exception inner)
            : base(message, inner)
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
        protected DapException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
