using OmniSharp.Extensions.DebugAdapter.Client.Handlers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Extensions.DebugAdapterProtocol.Client.Tests
{
    /// <summary>
    ///     Tests for <see cref="IHandler"/> and friends.
    /// </summary>
    public class HandlerTests
        : TestBase
    {
        /// <summary>
        ///     Create a new <see cref="IHandler"/> test suite.
        /// </summary>
        /// <param name="testOutput">
        ///     Output for the current test.
        /// </param>
        public HandlerTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        /// <summary>
        ///     Verify that <see cref="DelegateEmptyDapEventHandler"/> specifies the correct payload type.
        /// </summary>
        [Fact(DisplayName = "DelegateEmptyDapEventHandler specifies correct payload type")]
        public void DelegateEmptyDapEventHandler_PayloadType()
        {
            IHandler handler = new DelegateEmptyDapEventHandler(
                method: "test",
                handler: () =>
                {
                    // Nothing to do.
                }
            );

            Assert.Null(handler.PayloadType);
        }

        /// <summary>
        ///     Verify that <see cref="DelegateDapEventHandler"/> specifies the correct payload type.
        /// </summary>
        [Fact(DisplayName = "DelegateDapEventHandler specifies correct payload type")]
        public void DelegateDapEventHandler_PayloadType()
        {
            IHandler handler = new DelegateDapEventHandler<string>(
                method: "test",
                handler: notification =>
                {
                    // Nothing to do.
                }
            );

            Assert.Equal(typeof(string), handler.PayloadType);
        }

        /// <summary>
        ///     Verify that <see cref="DelegateRequestResponseHandler{TRequest, TResponse}"/> specifies the correct payload type (<c>null</c>).
        /// </summary>
        [Fact(DisplayName = "DelegateRequestResponseHandler specifies correct payload type")]
        public void DelegateRequestResponseHandler_PayloadType()
        {
            IHandler handler = new DelegateRequestResponseHandler<string, string>(
                method: "test",
                handler: (request, cancellationToken) =>
                {
                    // Nothing to do.

                    return Task.FromResult<string>("hello");
                }
            );

            Assert.Equal(typeof(string), handler.PayloadType);
        }
    }
}
