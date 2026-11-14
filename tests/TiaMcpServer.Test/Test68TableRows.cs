using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Rows written into the force table and a watch table, and read back from TIA Portal.
    /// </summary>
    /// <remarks>Measured 2026-11-13 on TIA Portal V21 by probes in the same shape, on a new CPU 1512C.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test68TableRows
    {
        private const string PlcName = "PLC_1";

        private static readonly string[] ForcedAddresses = { "%Q0.0:P", "%IW4:P" };

        private static readonly string[] WatchedAddresses = { "%M0.0", "%MW10", "%MD4", "%QW2" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "TableRows");
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 512-1CK01-0AB0/V2.9", PlcName);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>TIA Portal adds ':P' to what a force table holds: forcing acts on the periphery.</remarks>
        [TestMethod]
        public void WriteForceTable_InputsAndOutputs_ReadBackOnTheirPeripheryWithTheirValues()
        {
            var rows = AssemblyHooks.SharedPortal.WriteForceTable(PlcName, "{\"rows\": [{\"address\": \"%Q0.0\", \"format\": \"Bool\", \"value\": \"TRUE\"}, {\"address\": \"%IW4\", \"format\": \"DEC_signed\", \"value\": \"123\"}]}", Backup());

            CollectionAssert.AreEqual(ForcedAddresses, rows.Select(row => row.Address).ToArray(), string.Join("; ", rows.Select(row => row.Line)));
            StringAssert.Contains(rows[0].Line, "TRUE");
            StringAssert.Contains(rows[1].Line, "123");
        }

        [TestMethod]
        public void WriteForceTable_Twice_HoldsOnlyTheSecondRows()
        {
            AssemblyHooks.SharedPortal.WriteForceTable(PlcName, "{\"rows\": [{\"address\": \"%Q0.0\", \"format\": \"Bool\", \"value\": \"TRUE\"}, {\"address\": \"%Q0.1\", \"format\": \"Bool\", \"value\": \"TRUE\"}]}", Backup());

            var rows = AssemblyHooks.SharedPortal.WriteForceTable(PlcName, "{\"rows\": [{\"address\": \"%Q0.2\", \"format\": \"Bool\", \"value\": \"FALSE\"}]}", Backup());

            Assert.AreEqual("%Q0.2:P", rows.Single().Address);
        }

        [TestMethod]
        public void WriteForceTable_AMemoryBit_ThrowsInvalidParamsAndWritesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.WriteForceTable(PlcName, "{\"rows\": [{\"address\": \"%M5.0\", \"format\": \"Bool\", \"value\": \"TRUE\"}]}", Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetWatchTable(PlcName, "Force table").Count);
        }

        [TestMethod]
        public void WriteWatchTable_EveryMeasuredShape_ReadsBackAsWritten()
        {
            var rows = AssemblyHooks.SharedPortal.WriteWatchTable(
                PlcName,
                "Checks",
                "{\"rows\": [{\"address\": \"%M0.0\", \"format\": \"Bool\", \"value\": \"TRUE\"}, {\"address\": \"%MW10\", \"format\": \"DEC_signed\"}, {\"address\": \"%MD4\", \"format\": \"Float\", \"value\": \"1.5\"}, {\"address\": \"%QW2\", \"format\": \"Hex\", \"value\": \"16#0C\"}]}",
                Backup());

            CollectionAssert.AreEqual(WatchedAddresses, rows.Select(row => row.Address).ToArray(), string.Join("; ", rows.Select(row => row.Line)));
            StringAssert.Contains(rows[2].Line, "1.5");
            StringAssert.Contains(rows[3].Line, "16#000C");
        }

        [TestMethod]
        public void WriteWatchTable_ANewName_CreatesTheTable()
        {
            AssemblyHooks.SharedPortal.WriteWatchTable(PlcName, "Motor checks", "{\"rows\": [{\"address\": \"%MW10\", \"format\": \"DEC_signed\"}]}", Backup());

            var tables = AssemblyHooks.SharedPortal.GetWatchTables(PlcName).Select(table => table.Name).ToList();

            CollectionAssert.Contains(tables, "Motor checks");
        }

        [TestMethod]
        public void WriteForceTable_ATable_ExportsTheTablesFirst()
        {
            var backup = Backup();

            AssemblyHooks.SharedPortal.WriteForceTable(PlcName, "{\"rows\": [{\"address\": \"%Q0.0\", \"format\": \"Bool\", \"value\": \"TRUE\"}]}", backup);

            Assert.IsTrue(Directory.EnumerateFiles(backup, "*.xml", SearchOption.AllDirectories).Count() >= 2, "the tables and the written document should both be in " + backup);
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
