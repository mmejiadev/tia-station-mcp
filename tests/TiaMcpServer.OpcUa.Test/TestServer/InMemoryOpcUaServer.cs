using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;

namespace TiaMcpServer.OpcUa.Test.TestServer
{
    /// <summary>
    /// An OPC UA server running inside the test process, on a free local port.
    /// </summary>
    /// <remarks>
    /// Each instance has its own PKI directory, so its own certificate. Two instances started one
    /// after the other on the same port therefore look exactly like a CPU that was replaced: same
    /// address, different certificate. That is what the pin test needs, over a real channel.
    ///
    /// The server accepts any client certificate. What is under test is the client's judgement of
    /// the server, not the server's judgement of the client.
    /// </remarks>
    internal sealed class InMemoryOpcUaServer : IDisposable
    {
        private readonly ApplicationInstance _application;
        private readonly CellServer _server;

        private InMemoryOpcUaServer(ApplicationInstance application, CellServer server, int port)
        {
            _application = application;
            _server = server;
            Port = port;
        }

        internal int Port { get; }

        internal string Url => $"opc.tcp://localhost:{Port}";

        internal static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        internal static async Task<InMemoryOpcUaServer> StartAsync(string pkiRoot, int port, bool isSecured)
        {
            var telemetry = DefaultTelemetry.Create(_ => { });
            var application = new ApplicationInstance(telemetry)
            {
                ApplicationName = "tia-station-mcp test cell",
                ApplicationType = ApplicationType.Server
            };

            var policies = application
                .Build("urn:localhost:tia-station-mcp:test-cell", "urn:tia-station-mcp:test-cell")
                .AsServer(new[] { $"opc.tcp://localhost:{port}" });

            var secured = isSecured ? policies.AddSignAndEncryptPolicies(true) : policies.AddUnsecurePolicyNone(true);

            await secured
                .AddUserTokenPolicy(UserTokenType.Anonymous)
                .AddSecurityConfiguration(OwnCertificate(pkiRoot), pkiRoot)
                .SetAutoAcceptUntrustedCertificates(true)
                .CreateAsync()
                .ConfigureAwait(false);

            await application.CheckApplicationInstanceCertificatesAsync(false, 12).ConfigureAwait(false);

            var server = new CellServer();
            await application.StartAsync(server).ConfigureAwait(false);

            return new InMemoryOpcUaServer(application, server, port);
        }

        public void Dispose()
        {
            _application.StopAsync().AsTask().GetAwaiter().GetResult();
            _server.Dispose();
        }

        private static CertificateIdentifierCollection OwnCertificate(string pkiRoot)
        {
            return new CertificateIdentifierCollection
            {
                new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiRoot, "own"),
                    SubjectName = "CN=tia-station-mcp test cell, O=tia-station-mcp",
                    CertificateType = ObjectTypeIds.RsaSha256ApplicationCertificateType
                }
            };
        }
    }
}
