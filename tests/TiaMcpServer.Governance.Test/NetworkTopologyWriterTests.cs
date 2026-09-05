using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// This class had no test at all until now: it lived in the assembly that needs TIA Portal to
    /// build, so the only way to reach it was to run the whole Openness suite. It touches no
    /// Openness type, and audit finding F5 is exactly about that gap.
    ///
    /// What it produces is a file meant to be diffed between revisions of a project, so the
    /// properties worth asserting are the ones that make a diff trustworthy.
    /// </remarks>
    [TestClass]
    public sealed class NetworkTopologyWriterTests
    {
        private string _root = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _root = Path.Combine(Path.GetTempPath(), $"topology-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        /// <remarks>
        /// The rows are sorted for a reason the class doc states: a file whose line order follows
        /// whatever order TIA happened to enumerate devices in produces diffs that mean nothing,
        /// and a diff that means nothing trains everybody to skip it.
        /// </remarks>
        [TestMethod]
        public void Write_NodesInAnyOrder_ProducesTheSameFile()
        {
            var nodes = new[] { Node("PLC_2", "X1"), Node("PLC_1", "X2"), Node("PLC_1", "X1") };

            NetworkTopologyWriter.Write(_root, nodes);
            var written = Contents();

            NetworkTopologyWriter.Write(_root, nodes.Reverse().ToArray());

            Assert.AreEqual(written, Contents(), "the row order followed the input order");
        }

        [TestMethod]
        public void Write_SeveralNodes_OrdersThemByDeviceThenInterface()
        {
            NetworkTopologyWriter.Write(_root, new[] { Node("PLC_2", "X1"), Node("PLC_1", "X2"), Node("PLC_1", "X1") });

            var rows = Rows();

            CollectionAssert.AreEqual(
                new[] { "PLC_1 | X1", "PLC_1 | X2", "PLC_2 | X1" },
                rows.Select(row => string.Join(" | ", row.Split('|').Take(2).Select(part => part.Trim()))).ToArray());
        }

        /// <remarks>
        /// An interface wired to nothing is a common and otherwise silent reason a download fails,
        /// so the file has to say so rather than leave the column blank.
        /// </remarks>
        [TestMethod]
        public void Write_AnInterfaceWiredToNothing_SaysSoRatherThanLeavingItBlank()
        {
            NetworkTopologyWriter.Write(_root, new[] { new NetworkNodeInfo("PLC_1", "X1", "Ethernet", "192.168.0.1", string.Empty) });

            StringAssert.Contains(Rows().Single(), "<not connected>", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Write_ANode_RecordsEveryColumnItWasGiven()
        {
            NetworkTopologyWriter.Write(_root, new[] { new NetworkNodeInfo("PLC_1", "X1", "Ethernet", "192.168.0.1", "PN/IE_1") });

            Assert.AreEqual("PLC_1 | X1 | Ethernet | 192.168.0.1 | PN/IE_1", Rows().Single());
        }

        [TestMethod]
        public void Write_NoNodesAtAll_StillWritesAFileSayingThereAreNone()
        {
            var relativePath = NetworkTopologyWriter.Write(_root, Array.Empty<NetworkNodeInfo>());

            Assert.AreEqual("network/topology.txt", relativePath, "the path is reported with forward slashes");
            Assert.AreEqual(0, Rows().Count);
            StringAssert.Contains(Contents(), "# 0 interface(s)", StringComparison.Ordinal);
        }

        /// <remarks>
        /// The returned path goes into a snapshot report that is compared between machines, so it
        /// is relative and uses forward slashes whatever the platform separator is.
        /// </remarks>
        [TestMethod]
        public void Write_ANode_ReturnsThePathRelativeToTheSnapshotRoot()
        {
            var relativePath = NetworkTopologyWriter.Write(_root, new[] { Node("PLC_1", "X1") });

            Assert.AreEqual("network/topology.txt", relativePath);
            Assert.IsTrue(File.Exists(Path.Combine(_root, "network", "topology.txt")), "nothing was written there");
        }

        private static NetworkNodeInfo Node(string device, string interfaceName)
        {
            return new NetworkNodeInfo(device, interfaceName, "Ethernet", "192.168.0.1", "PN/IE_1");
        }

        private string Contents()
        {
            return File.ReadAllText(Path.Combine(_root, "network", "topology.txt"));
        }

        private System.Collections.Generic.List<string> Rows()
        {
            return Contents()
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
                .ToList();
        }
    }
}
