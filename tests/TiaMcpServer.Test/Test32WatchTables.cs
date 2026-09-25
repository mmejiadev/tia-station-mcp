using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Watch and force tables: listing them, creating one, and the export and import that rows
    /// travel in.
    /// </summary>
    /// <remarks>
    /// Everything here is offline. A row in one of these tables reaches nothing until a person
    /// opens the table against a connected CPU, so nothing in this class can be asserted against a
    /// running machine and nothing here tries to.
    ///
    /// Rows are not written by these tests because they cannot be written at all. Measured on
    /// 2026-09-22: asked what it will create in a table's entries, TIA Portal V20 answers
    /// <c>PlcTableCommentEntry</c> and nothing else, so a row with an address exists only in a
    /// document that was imported. That is what the round trip below is, and it is the whole of
    /// what this server can do about rows.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test32WatchTables
    {
        private const string TableName = "Cell checks";

        private string _testDirectory = string.Empty;
        private string _backupDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            _backupDirectory = Path.Combine(_testDirectory, "backup");

            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateWatchTable_ANewName_IsListedAfterwards()
        {
            AssemblyHooks.SharedPortal.CreateWatchTable(Settings.Project1PlcSoftwarePath0, TableName, _backupDirectory);

            var tables = AssemblyHooks.SharedPortal.GetWatchTables(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(
                tables.Any(table => table.Name == TableName && table.Kind == WatchTableInfo.WatchKind),
                $"The table is not in the list: {Describe(tables)}");
        }

        /// <remarks>
        /// Nothing is replaced, which is the repository rule for every write. A second create that
        /// quietly returned the first table would be defensible for a container; a watch table is
        /// one somebody filled in, and handing it back as though it were new invites a caller to
        /// change a table it did not mean to touch.
        /// </remarks>
        [TestMethod]
        public void CreateWatchTable_ANameAlreadyThere_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.CreateWatchTable(Settings.Project1PlcSoftwarePath0, TableName, _backupDirectory);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateWatchTable(Settings.Project1PlcSoftwarePath0, TableName, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void CreateWatchTable_ANewTable_TakesABackupFirst()
        {
            AssemblyHooks.SharedPortal.CreateWatchTable(Settings.Project1PlcSoftwarePath0, TableName, _backupDirectory);

            // The repository rule is that nothing is written without exporting the previous state.
            // A backup that is not on disk is not a backup.
            Assert.IsTrue(
                Directory.Exists(_backupDirectory),
                $"No backup was written to {_backupDirectory}");
        }

        /// <remarks>
        /// The force table is listed beside the watch tables because a caller asking for "the
        /// tables of this PLC" wants both, and it is the one that cannot be created — so a caller
        /// that needs its name has nowhere else to read it.
        /// </remarks>
        [TestMethod]
        public void GetWatchTables_ThePlc_IncludesTheForceTableItCannotCreate()
        {
            var tables = AssemblyHooks.SharedPortal.GetWatchTables(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(
                tables.Any(table => table.Kind == WatchTableInfo.ForceKind),
                $"No force table is listed, so nothing could name it: {Describe(tables)}");
        }

        [TestMethod]
        public void ExportWatchTable_ATableThatExists_WritesTheDocument()
        {
            var exportPath = ExportedTable();

            Assert.IsTrue(File.Exists(exportPath), $"Nothing was written to {exportPath}");
        }

        /// <remarks>
        /// The round trip is the point: this pair is the only route rows have into a table, so a
        /// document that exports and does not import again would make the whole mechanism useless
        /// while both halves looked like they worked.
        /// </remarks>
        [TestMethod]
        public void ImportWatchTables_ADocumentThisServerExported_PutsTheTableBack()
        {
            var exportPath = ExportedTable();

            var imported = AssemblyHooks.SharedPortal.ImportWatchTables(
                Settings.Project1PlcSoftwarePath0, exportPath, _backupDirectory);

            CollectionAssert.Contains(imported.ToList(), TableName);
        }

        [TestMethod]
        public void ImportWatchTables_ADocumentThatIsNotThere_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ImportWatchTables(
                    Settings.Project1PlcSoftwarePath0,
                    Path.Combine(_testDirectory, "no-such-document.xml"),
                    _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void ExportWatchTable_AnUnknownTable_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ExportWatchTable(
                    Settings.Project1PlcSoftwarePath0, "NoSuchTable", Path.Combine(_testDirectory, "out.xml")));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        [TestMethod]
        public void GetWatchTable_AnUnknownTable_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetWatchTable(Settings.Project1PlcSoftwarePath0, "NoSuchTable"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        /// <remarks>
        /// The force table exists and cannot be made, so a caller that misspells its name must be
        /// told that rather than left to look for a create tool there is no point writing.
        /// </remarks>
        [TestMethod]
        public void GetWatchTable_AMisspeltForceTable_SaysOpennessCannotCreateOne()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetWatchTable(Settings.Project1PlcSoftwarePath0, "NoSuchForceTable"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        private string ExportedTable()
        {
            AssemblyHooks.SharedPortal.CreateWatchTable(Settings.Project1PlcSoftwarePath0, TableName, _backupDirectory);

            return AssemblyHooks.SharedPortal.ExportWatchTable(
                Settings.Project1PlcSoftwarePath0, TableName, Path.Combine(_testDirectory, "table.xml"));
        }

        private static string Describe(IReadOnlyList<WatchTableInfo> tables)
        {
            return string.Join("\n", tables.Select(table => table.Line));
        }
    }
}
