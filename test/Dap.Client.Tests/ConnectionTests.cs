using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using OmniSharp.Extensions.DebugAdapter.Client.Dispatcher;
using OmniSharp.Extensions.DebugAdapter.Client.Protocol;
using Xunit;
using Xunit.Abstractions;
using OmniSharp.Extensions.DebugAdapter.Protocol.Serialization;

namespace OmniSharp.Extensions.DebugAdapterProtocol.Client.Tests
{
    /// <summary>
    ///     Tests for <see cref="DapConnection"/>.
    /// </summary>
    public class ConnectionTests
        : PipeServerTestBase
    {
        /// <summary>
        ///     Create a new <see cref="DapConnection"/> test suite.
        /// </summary>
        /// <param name="testOutput">
        ///     Output for the current test.
        /// </param>
        public ConnectionTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        /// <summary>
        ///     Verify that a server <see cref="DapConnection"/> can handle an empty notification from a client <see cref="DapConnection"/>.
        /// </summary>
        [Fact(DisplayName = "Server connection can handle empty notification from client")]
        public async Task Client_HandleEmptyNotification_Success()
        {
            var testCompletion = new TaskCompletionSource<object>();

            DapConnection clientConnection = await CreateClientConnection();
            DapConnection serverConnection = await CreateServerConnection();

            var serverDispatcher = CreateDispatcher();
            serverDispatcher.HandleEvent("test", () =>
            {
                Log.LogInformation("Got notification.");

                testCompletion.SetResult(null);
            });
            serverConnection.Connect(serverDispatcher);

            clientConnection.Connect(CreateDispatcher());
            clientConnection.SendEvent("test");

            await testCompletion.Task;

            clientConnection.Disconnect(flushOutgoing: true);
            serverConnection.Disconnect();

            await Task.WhenAll(clientConnection.HasHasDisconnected, serverConnection.HasHasDisconnected);
        }

        /// <summary>
        ///     Verify that a client <see cref="DapConnection"/> can handle an empty notification from a server <see cref="DapConnection"/>.
        /// </summary>
        [Fact(DisplayName = "Client connection can handle empty notification from server")]
        public async Task Server_HandleEmptyNotification_Success()
        {
            var testCompletion = new TaskCompletionSource<object>();

            DapConnection clientConnection = await CreateClientConnection();
            DapConnection serverConnection = await CreateServerConnection();

            var clientDispatcher = CreateDispatcher();
            clientDispatcher.HandleEvent("test", () =>
            {
                Log.LogInformation("Got notification.");

                testCompletion.SetResult(null);
            });
            clientConnection.Connect(clientDispatcher);

            serverConnection.Connect(CreateDispatcher());
            serverConnection.SendEvent("test");

            await testCompletion.Task;

            serverConnection.Disconnect(flushOutgoing: true);
            clientConnection.Disconnect();

            await Task.WhenAll(clientConnection.HasHasDisconnected, serverConnection.HasHasDisconnected);
        }

        /// <summary>
        ///     Verify that a client <see cref="DapConnection"/> can handle a request from a server <see cref="DapConnection"/>.
        /// </summary>
        [Fact(DisplayName = "Client connection can handle request from server")]
        public async Task Server_HandleRequest_Success()
        {
            DapConnection clientConnection = await CreateClientConnection();
            DapConnection serverConnection = await CreateServerConnection();

            var clientDispatcher = CreateDispatcher();
            clientDispatcher.HandleRequest<TestRequest, TestResponse>("test", (request, cancellationToken) =>
            {
                Log.LogInformation("Got request: {@Request}", request);

                return Task.FromResult(new TestResponse
                {
                    Value = request.Value.ToString()
                });
            });
            clientConnection.Connect(clientDispatcher);

            serverConnection.Connect(CreateDispatcher());
            TestResponse response = await serverConnection.SendRequest<TestResponse>("test", new TestRequest
            {
                Value = 1234
            });

            Assert.Equal("1234", response.Value);

            Log.LogInformation("Got response: {@Response}", response);

            serverConnection.Disconnect(flushOutgoing: true);
            clientConnection.Disconnect();

            await Task.WhenAll(clientConnection.HasHasDisconnected, serverConnection.HasHasDisconnected);
        }

        /// <summary>
        ///     Verify that a server <see cref="DapConnection"/> can handle a request from a client <see cref="DapConnection"/>.
        /// </summary>
        [Fact(DisplayName = "Server connection can handle request from client")]
        public async Task Client_HandleRequest_Success()
        {
            DapConnection clientConnection = await CreateClientConnection();
            DapConnection serverConnection = await CreateServerConnection();

            var serverDispatcher = CreateDispatcher();
            serverDispatcher.HandleRequest<TestRequest, TestResponse>("test", (request, cancellationToken) =>
            {
                Log.LogInformation("Got request: {@Request}", request);

                return Task.FromResult(new TestResponse
                {
                    Value = request.Value.ToString()
                });
            });
            serverConnection.Connect(serverDispatcher);

            clientConnection.Connect(CreateDispatcher());
            TestResponse response = await clientConnection.SendRequest<TestResponse>("test", new TestRequest
            {
                Value = 1234
            });

            Assert.Equal("1234", response.Value);

            Log.LogInformation("Got response: {@Response}", response);

            clientConnection.Disconnect(flushOutgoing: true);
            serverConnection.Disconnect();

            await Task.WhenAll(clientConnection.HasHasDisconnected, serverConnection.HasHasDisconnected);
        }

        /// <summary>
        ///     Create an <see cref="DapDispatcher"/> for use in tests.
        /// </summary>
        /// <returns>
        ///     The <see cref="DapDispatcher"/>.
        /// </returns>
        DapDispatcher CreateDispatcher() => new DapDispatcher(new DapProtocolSerializer());
    }
}
