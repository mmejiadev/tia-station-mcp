using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The file this writes is two things at once: the hardware half of a snapshot, and the backup
    /// taken before a module is plugged. Both are read by diffing them against another revision,
    /// so what is worth asserting is what makes a diff trustworthy — the same input producing the
    /// same bytes, and an absent value reading as absent rather than as blank.
    /// </remarks>
    [TestClass]
    public sealed class HardwareLayoutWriterTests
    {
        private string _root = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _root = Path.Combine(Path.GetTempPath(), $"hardware-{Guid.NewGuid():N}");
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

        [TestMethod]
        public void Write_ModulesInAnyOrder_ProducesTheSameFile()
        {
            var modules = new[] { Module("PLC_1/DI_1", 2), Module("PLC_1/CPU", 1), Module("PLC_2/CPU", 1) };

            HardwareLayoutWriter.Write(_root, modules);
            var written = Contents();

            HardwareLayoutWriter.Write(_root, modules.Reverse().ToArray());

            Assert.AreEqual(written, Contents(), "the row order followed the input order");
        }

        /// <remarks>
        /// A rack is read slot by slot, so the rows follow the slots. Sorting by the module's own
        /// path instead — which is what the network table does, because an interface has no slot —
        /// would interleave DI_1, DI_10 and DI_2, and moving a module to another slot would not
        /// move its row at all.
        /// </remarks>
        [TestMethod]
        public void Write_TwoModulesInOneStation_OrdersThemBySlotAndNotByName()
        {
            HardwareLayoutWriter.Write(_root, new[] { Module("PLC_1/DI_1", 3), Module("PLC_1/DI_2", 2) });

            CollectionAssert.AreEqual(
                new[] { "PLC_1/DI_2", "PLC_1/DI_1" },
                Rows().Select(row => row.Split('|')[0].Trim()).ToArray());
        }

        [TestMethod]
        public void Write_ModulesOfTwoStations_KeepsEachStationTogether()
        {
            HardwareLayoutWriter.Write(_root, new[] { Module("PLC_2/DI_1", 2), Module("PLC_1/DI_1", 2), Module("PLC_1/CPU", 1) });

            CollectionAssert.AreEqual(
                new[] { "PLC_1/CPU", "PLC_1/DI_1", "PLC_2/DI_1" },
                Rows().Select(row => row.Split('|')[0].Trim()).ToArray());
        }

        [TestMethod]
        public void Write_AModule_RecordsEveryColumnItWasGiven()
        {
            HardwareLayoutWriter.Write(_root, new[] { new ModuleInfo("PLC_1/DI_1", 2, "OrderNumber:6ES7 521-1BL00-0AB0/V2.1", false) });

            Assert.AreEqual("PLC_1/DI_1 | 2 | OrderNumber:6ES7 521-1BL00-0AB0/V2.1 | plugged", Rows().Single());
        }

        /// <remarks>
        /// A built-in item is not a module somebody chose, and it cannot be unplugged. Saying which
        /// is which is what stops a reader from concluding that a station was assembled from parts
        /// that were never plugged into anything.
        /// </remarks>
        [TestMethod]
        public void Write_ABuiltInItem_SaysItIsNotPlugged()
        {
            HardwareLayoutWriter.Write(_root, new[] { new ModuleInfo("PLC_1/PROFINET interface_1", 1, "System:Device.Interface", true) });

            StringAssert.Contains(Rows().Single(), "built-in", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Write_NoModulesAtAll_StillWritesAFileSayingThereAreNone()
        {
            var relativePath = HardwareLayoutWriter.Write(_root, Array.Empty<ModuleInfo>());

            Assert.AreEqual("hardware/modules.txt", relativePath, "the path is reported with forward slashes");
            Assert.AreEqual(0, Rows().Count);
            StringAssert.Contains(Contents(), "# 0 module(s)", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Write_AModule_ReturnsThePathRelativeToTheSnapshotRoot()
        {
            var relativePath = HardwareLayoutWriter.Write(_root, new[] { Module("PLC_1/CPU", 1) });

            Assert.AreEqual("hardware/modules.txt", relativePath);
            Assert.IsTrue(File.Exists(Path.Combine(_root, "hardware", "modules.txt")), "nothing was written there");
        }

        private static ModuleInfo Module(string devicePath, int positionNumber)
        {
            return new ModuleInfo(devicePath, positionNumber, "OrderNumber:6ES7 511-1AK02-0AB0/V3.1", false);
        }

        private string Contents()
        {
            return File.ReadAllText(Path.Combine(_root, "hardware", "modules.txt"));
        }

        private List<string> Rows()
        {
            return Contents()
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
                .ToList();
        }
    }
}
