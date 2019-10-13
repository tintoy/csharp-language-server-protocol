using System;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Client.Processes;
using OmniSharp.Extensions.DebugAdapter.Client.Protocol;
using Xunit.Abstractions;

namespace OmniSharp.Extensions.DebugAdapterProtocol.Client.Tests
{
    /// <summary>
    ///     The base class for test suites that use a <see cref="PipeServerProcess"/>.
    /// </summary>
    public abstract class PipeServerTestBase
        : TestBase
    {
        /// <summary>
        ///     The <see cref="PipeServerProcess"/> used to connect client and server streams.
        /// </summary>
        readonly NamedPipeServerProcess _serverProcess;

        /// <summary>
        ///     Create a new <see cref="PipeServerTestBase"/>.
        /// </summary>
        /// <param name="testOutput">
        ///     Output for the current test.
        /// </param>
        protected PipeServerTestBase(ITestOutputHelper testOutput)
            : base(testOutput)
        {
            _serverProcess = new NamedPipeServerProcess(Guid.NewGuid().ToString("N"), LoggerFactory);
            Disposal.Add(_serverProcess);
        }

        /// <summary>
        ///     The workspace root path.
        /// </summary>
        protected virtual string WorkspaceRoot => Path.GetDirectoryName(GetType().Assembly.Location);

        /// <summary>
        ///     The client's output stream (server reads from this).
        /// </summary>
        protected Stream ClientOutputStream => _serverProcess.ClientOutputStream;

        /// <summary>
        ///     The client's input stream (server writes to this).
        /// </summary>
        protected Stream ClientInputStream => _serverProcess.ClientInputStream;

        /// <summary>
        ///     The server's output stream (client reads from this).
        /// </summary>
        protected Stream ServerOutputStream => _serverProcess.ServerOutputStream;

        /// <summary>
        ///     The server's input stream (client writes to this).
        /// </summary>
        protected Stream ServerInputStream => _serverProcess.ServerInputStream;

        /// <summary>
        ///     Create a <see cref="DapConnection"/> that uses the client ends of the the test's <see cref="PipeServerProcess"/> streams.
        /// </summary>
        /// <returns>
        ///     The <see cref="DapConnection"/>.
        /// </returns>
        protected async Task<DapConnection> CreateClientConnection()
        {
            if (!_serverProcess.IsRunning)
                await StartServer();

            await _serverProcess.HasStarted;

            var connection = new DapConnection(LoggerFactory, input: ServerOutputStream, output: ServerInputStream);
            Disposal.Add(connection);

            return connection;
        }

        /// <summary>
        ///     Create a <see cref="DapConnection"/> that uses the server ends of the the test's <see cref="PipeServerProcess"/> streams.
        /// </summary>
        /// <returns>
        ///     The <see cref="DapConnection"/>.
        /// </returns>
        protected async Task<DapConnection> CreateServerConnection()
        {
            if (!_serverProcess.IsRunning)
                await StartServer();

            await _serverProcess.HasStarted;

            var connection = new DapConnection(LoggerFactory, input: ClientOutputStream, output: ClientInputStream);
            Disposal.Add(connection);

            return connection;
        }

        /// <summary>
        ///     Called to start the server process.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        protected virtual Task StartServer() => _serverProcess.Start();

        /// <summary>
        ///     Called to stop the server process.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        protected virtual Task StopServer() => _serverProcess.Stop();
    }
}
