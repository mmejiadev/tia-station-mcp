using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.OpcUa.Test.TestServer;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.OpcUa.Test
{
    /// <summary>
    /// The reader against a real OPC UA server, started in this process.
    /// </summary>
    [TestClass]
    public class OpcUaReaderTests
    {
        private const int TimeoutMilliseconds = 60000;

        private string _directory = string.Empty;

        [TestInitialize]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "tia-mcp-opcua-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void DeleteDirectory()
        {
            // Best effort: the stack can hold a certificate file open for a moment after a server
            // stops, and a leftover temp directory is not a test failure.
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                Console.WriteLine($"Could not delete {_directory}; left for the OS to clean up.");
            }
        }

        [TestMethod]
        [Timeout(TimeoutMilliseconds)]
        public async Task ReadAsync_SiemensStyleNodeIds_ReturnsTheirValuesInvariantly()
        {
            using var server = await StartServerAsync("server", InMemoryOpcUaServer.FreePort(), true);
            using var reader = CreateReader();

            var readings = await reader.ReadAsync(
                OpcUaEndpoint.Parse(server.Url),
                new[] { NodeId(CellNodeManager.RunningId), NodeId(CellNodeManager.SpeedId), NodeId(CellNodeManager.StationStepsId) },
                CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "true", "0.5", "[1, 2, 3, 4]" }, readings.Select(reading => reading.Value).ToList());
        }

        [TestMethod]
        [Timeout(TimeoutMilliseconds)]
        public async Task ReadAsync_OneUnknownNode_ReportsItWithoutLosingTheOthers()
        {
            using var server = await StartServerAsync("server", InMemoryOpcUaServer.FreePort(), true);
            using var reader = CreateReader();

            var readings = await reader.ReadAsync(
                OpcUaEndpoint.Parse(server.Url),
                new[] { NodeId(CellNodeManager.PieceCountId), NodeId("\"DB_Cell\".\"Misspelt\"") },
                CancellationToken.None);

            Assert.AreEqual("42", readings[0].Value);
            Assert.IsFalse(readings[1].IsGood);
            StringAssert.Contains(readings[1].Status, "BadNodeIdUnknown", StringComparison.Ordinal);
        }

        [TestMethod]
        [Timeout(TimeoutMilliseconds)]
        public async Task BrowseAsync_TheCellFolder_ListsNodeIdsThatCanBeReadBack()
        {
            using var server = await StartServerAsync("server", InMemoryOpcUaServer.FreePort(), true);
            using var reader = CreateReader();
            var endpoint = OpcUaEndpoint.Parse(server.Url);

            var children = await reader.BrowseAsync(endpoint, NodeId(CellNodeManager.FolderId), CancellationToken.None);
            var running = children.Single(child => child.DisplayName == "Running");
            var readings = await reader.ReadAsync(endpoint, new[] { running.NodeId }, CancellationToken.None);

            Assert.AreEqual("true", readings[0].Value);
        }

        [TestMethod]
        [Timeout(TimeoutMilliseconds)]
        public async Task BrowseAsync_NoParent_StartsAtTheObjectsFolder()
        {
            using var server = await StartServerAsync("server", InMemoryOpcUaServer.FreePort(), true);
            using var reader = CreateReader();

            var children = await reader.BrowseAsync(OpcUaEndpoint.Parse(server.Url), null, CancellationToken.None);

            Assert.IsTrue(children.Any(child => child.DisplayName == "DB_Cell"));
        }

        [TestMethod]
        [Timeout(TimeoutMilliseconds)]
        public async Task ReadAsync_FirstConnection_PinsTheServerCertificate()
        {
            using var server = await StartServerAsync("server", InMemoryOpcUaServer.FreePort(), true);
            var pins = new ServerCertificatePins(Path.Combine(_directory, "pins.json"));
            using var reader = new OpcUaReader(Path.Combine(_directory, "client-pki"), pins);
            var endpoint = OpcUaEndpoint.Parse(server.Url);

            await reader.ReadAsync(endpoint, new[] { NodeId(CellNodeManager.RunningId) }, CancellationToken.None);

            Assert.IsNotNull(pins.Pinned(endpoint.PolicyTarget));
        }

        [TestMethod]
        [Timeout(TimeoutMilliseconds)]
        public async Task ReadAsync_ADifferentServerCertificateAtTheSameAddress_IsRefused()
        {
            var port = InMemoryOpcUaServer.FreePort();
            using var reader = CreateReader();

            using (var original = await StartServerAsync("original", port, true))
            {
                await reader.ReadAsync(OpcUaEndpoint.Parse(original.Url), new[] { NodeId(CellNodeManager.RunningId) }, CancellationToken.None);
            }

            using var replacement = await StartServerAsync("replacement", port, true);

            var exception = await Assert.ThrowsExceptionAsync<PortalException>(() =>
                reader.ReadAsync(OpcUaEndpoint.Parse(replacement.Url), new[] { NodeId(CellNodeManager.RunningId) }, CancellationToken.None));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
            StringAssert.Contains(exception.Message, "was recorded the first time", StringComparison.Ordinal);
        }

        [TestMethod]
        [Timeout(TimeoutMilliseconds)]
        public async Task ReadAsync_AServerOfferingNoSecurity_IsRefused()
        {
            using var server = await StartServerAsync("server", InMemoryOpcUaServer.FreePort(), false);
            using var reader = CreateReader();

            var exception = await Assert.ThrowsExceptionAsync<PortalException>(() =>
                reader.ReadAsync(OpcUaEndpoint.Parse(server.Url), new[] { NodeId(CellNodeManager.RunningId) }, CancellationToken.None));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
            StringAssert.Contains(exception.Message, "no secured endpoint", StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task ReadAsync_AMalformedNodeId_IsRefusedBeforeConnecting()
        {
            using var reader = CreateReader();

            var exception = await Assert.ThrowsExceptionAsync<PortalException>(() =>
                reader.ReadAsync(OpcUaEndpoint.Parse("opc.tcp://localhost:1"), new[] { "ns=abc;q=nothing" }, CancellationToken.None));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        private Task<InMemoryOpcUaServer> StartServerAsync(string name, int port, bool isSecured)
        {
            return InMemoryOpcUaServer.StartAsync(Path.Combine(_directory, name + "-pki"), port, isSecured);
        }

        private OpcUaReader CreateReader()
        {
            return new OpcUaReader(
                Path.Combine(_directory, "client-pki"),
                new ServerCertificatePins(Path.Combine(_directory, "pins.json")));
        }

        // The test server registers its namespace after the standard ones, so it is index 2 there.
        // A CPU uses 3 for its data blocks; what matters here is the quoted identifier, not the index.
        private static string NodeId(string identifier)
        {
            return "ns=2;s=" + identifier;
        }
    }
}
