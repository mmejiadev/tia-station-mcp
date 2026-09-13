using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tag tables: listing them, reading one, and creating tables, tags and constants.
    /// </summary>
    /// <remarks>
    /// Like Test13IoSystem and Test24Subnets these tests change the program, so each works on a
    /// copy retrieved per test rather than on the shared fixture. A leftover tag would surface as a
    /// compile failure in a class about something else.
    ///
    /// The assertion that matters most is that a path printed by <c>GetTagTables</c> is a path
    /// <c>CreateTag</c> accepts. That is the third time this repository has needed it written down,
    /// and the two previous times it was written down after the read and the write had already
    /// disagreed.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test25Tags
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;

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
        public void GetTagTables_OpenProject_FindsTheDefaultTable()
        {
            var tables = AssemblyHooks.SharedPortal.GetTagTables(Software);

            Assert.IsTrue(tables.Count > 0, "A PLC program with no tag table at all");
            Assert.IsTrue(
                tables.Any(table => table.IsDefault),
                $"No table is marked as the default one: {string.Join(", ", tables.Select(table => table.Path))}");
        }

        /// <remarks>
        /// The round trip: a path this read prints is a path the write takes. Nothing else keeps
        /// GetTagTables usable as CreateTag's manual.
        /// </remarks>
        [TestMethod]
        public void CreateTag_APathBuiltFromGetTagTables_IsListedByGetTags()
        {
            var table = DefaultTable();

            var created = AssemblyHooks.SharedPortal.CreateTag(
                Software, new TagDefinition($"{table.Path}/TiaMcpStart", "Bool", "%I0.0"), _backupDirectory);

            Assert.AreEqual("Bool", created.DataTypeName);
            Assert.IsTrue(
                AssemblyHooks.SharedPortal.GetTags(Software, table.Path).Any(entry => entry.Name == "TiaMcpStart"),
                "The tag was reported as created and GetTags does not list it");
        }

        /// <remarks>
        /// TIA normalises the data type, so a caller that trusted its own argument would write
        /// 'bool' into a document describing a program that holds 'Bool'.
        /// </remarks>
        [TestMethod]
        public void CreateTag_ALowercaseDataType_ComesBackAsTiaSpellsIt()
        {
            var created = AssemblyHooks.SharedPortal.CreateTag(
                Software, new TagDefinition($"{DefaultTable().Path}/TiaMcpLower", "bool", "%I0.1"), _backupDirectory);

            Assert.AreEqual("Bool", created.DataTypeName);
        }

        [TestMethod]
        public void CreateTag_TheSameTagTwice_ReportsTheOneThatIsThere()
        {
            var path = $"{DefaultTable().Path}/TiaMcpTwice";

            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition(path, "Bool", "%I0.2"), _backupDirectory);
            var again = AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition(path, "Bool", "%I0.2"), _backupDirectory);

            Assert.AreEqual("%I0.2", again.Assignment);
        }

        /// <remarks>
        /// Nothing here overwrites. Silently moving a tag the program already uses would compile
        /// and then behave differently, which is the worst outcome available.
        /// </remarks>
        [TestMethod]
        public void CreateTag_ANameTakenByADifferentTag_IsRefused()
        {
            var path = $"{DefaultTable().Path}/TiaMcpClash";

            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition(path, "Bool", "%I0.3"), _backupDirectory);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition(path, "Bool", "%I0.4"), _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "%I0.3", StringComparison.Ordinal);
        }

        /// <remarks>
        /// Tags and constants are one namespace to SCL and two compositions to Openness, which
        /// reports the collision as a generic failure. Saying which kind holds the name is what
        /// stops the caller looking at the data type.
        /// </remarks>
        [TestMethod]
        public void CreateTag_ANameTakenByAConstant_SaysWhatHoldsIt()
        {
            var table = DefaultTable().Path;

            AssemblyHooks.SharedPortal.CreateConstant(
                Software, new TagDefinition($"{table}/TiaMcpBoth", "Int", "7"), _backupDirectory);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateTag(
                    Software, new TagDefinition($"{table}/TiaMcpBoth", "Bool", "%I0.5"), _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "constant", StringComparison.Ordinal);
        }

        /// <remarks>
        /// **Openness accepts a data type that does not exist, and nothing downstream notices.**
        /// This test was written expecting a refusal, got none, was rewritten expecting the compiler
        /// to catch it, and got none either: <c>PlcTagComposition.Create</c> stored <c>NotAType</c>
        /// verbatim, and compiling the software reported success with the message "No block was
        /// compiled. All blocks are up-to-date". A software compile compiles blocks. It does not
        /// validate tag tables.
        ///
        /// So a misspelt data type produces a tag that exists, lists and exports, in a project that
        /// says it is fine. That is worth a test of its own precisely because it is silent, and the
        /// tool description says so: a caller that cannot spell the type has no check to fall back
        /// on, and should copy the spelling from GetTags of a tag that already works.
        ///
        /// The server does not refuse it, because it cannot tell an unknown type from a valid one:
        /// it has no list of what this CPU accepts, and the types a program may use include its own
        /// PLC data types and arrays. Inventing that list would refuse valid input, which is worse
        /// than accepting invalid input the caller can see in the tool's own read.
        /// </remarks>
        [TestMethod]
        public void CreateTag_ADataTypeTiaDoesNotKnow_IsStoredAndNothingReportsIt()
        {
            var created = AssemblyHooks.SharedPortal.CreateTag(
                Software, new TagDefinition($"{DefaultTable().Path}/TiaMcpBadType", "NotAType", "%I0.6"), _backupDirectory);

            Assert.AreEqual("NotAType", created.DataTypeName, "Openness silently changed the type it was given");

            var report = AssemblyHooks.SharedPortal.CompileSoftware(Software);

            Assert.IsTrue(report.IsSuccessful, "Compiling now catches an invalid tag type: the tool description can stop warning about it");
        }

        [TestMethod]
        public void CreateConstant_ANewConstant_IsListedAsAConstantWithItsValue()
        {
            var table = DefaultTable().Path;

            var created = AssemblyHooks.SharedPortal.CreateConstant(
                Software, new TagDefinition($"{table}/TiaMcpCycle", "Int", "500"), _backupDirectory);

            Assert.AreEqual("500", created.Assignment);
            Assert.IsTrue(created.IsConstant, "A user constant came back described as a tag");

            var listed = AssemblyHooks.SharedPortal.GetTags(Software, table)
                .FirstOrDefault(entry => entry.Name == "TiaMcpCycle");

            Assert.IsNotNull(listed, "The constant was created and GetTags does not list it");
            Assert.IsTrue(listed.IsConstant);
        }

        [TestMethod]
        public void CreateTagTable_ATableInsideAGroup_CreatesTheGroupAsWell()
        {
            var created = AssemblyHooks.SharedPortal.CreateTagTable(Software, "TiaMcpCell/IO", _backupDirectory);

            Assert.AreEqual("TiaMcpCell/IO", created.Path);
            Assert.IsTrue(
                AssemblyHooks.SharedPortal.GetTagTables(Software).Any(table => table.Path == "TiaMcpCell/IO"),
                "The table was created inside a group and GetTagTables does not list it there");
        }

        /// <remarks>
        /// A table is a container: asking for one that exists has got the caller what it wanted,
        /// which is the idempotence every write in this repository owes its callers.
        /// </remarks>
        [TestMethod]
        public void CreateTagTable_ATableThatAlreadyExists_ReportsIt()
        {
            var existing = DefaultTable();

            var again = AssemblyHooks.SharedPortal.CreateTagTable(Software, existing.Path, _backupDirectory);

            Assert.AreEqual(existing.Path, again.Path);
            Assert.AreEqual(existing.TagCount, again.TagCount);
        }

        [TestMethod]
        public void CreateTag_ATableThatDoesNotExist_SaysWhichTablesThereAre()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateTag(
                    Software, new TagDefinition("NoSuchTable/Start", "Bool", "%I0.7"), _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
            StringAssert.Contains(failure.Message, DefaultTable().Path, StringComparison.Ordinal);
        }

        /// <remarks>
        /// A bare name would create a tag in whichever table the code happened to pick. That is the
        /// ambiguity the full-path rule exists to prevent, so it is refused before anything opens.
        /// </remarks>
        [TestMethod]
        public void TagDefinition_ABareName_SaysItNamesNoTable()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => new TagDefinition("Start", "Bool", "%I0.0"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void CreateTag_NoBackupDirectory_IsRefusedBecauseThisChangesTheProgram()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateTag(
                    Software, new TagDefinition($"{DefaultTable().Path}/TiaMcpNoBackup", "Bool", "%I1.0"), string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void CreateTag_ExportsTheTagTablesBeforeChangingThem()
        {
            AssemblyHooks.SharedPortal.CreateTag(
                Software, new TagDefinition($"{DefaultTable().Path}/TiaMcpBackedUp", "Bool", "%I1.1"), _backupDirectory);

            Assert.IsTrue(
                Directory.Exists(_backupDirectory) && Directory.GetFiles(_backupDirectory, "*.xml").Length > 0,
                $"No tag table was exported to {_backupDirectory}");
        }

        [TestMethod]
        public void GetTags_ATableThatDoesNotExist_SaysWhichTablesThereAre()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetTags(Software, "NoSuchTable"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
            StringAssert.Contains(failure.Message, DefaultTable().Path, StringComparison.Ordinal);
        }

        [TestMethod]
        public void GetTagTables_NoProjectOpen_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.CloseProject();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetTagTables(Software));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <summary>The table TIA Portal writes into by default, found the way a caller would.</summary>
        private static TagTableInfo DefaultTable()
        {
            var tables = AssemblyHooks.SharedPortal.GetTagTables(Software);
            var table = tables.FirstOrDefault(one => one.IsDefault) ?? (tables.Count == 0 ? null : tables[0]);

            Assert.IsNotNull(table, "The PLC program has no tag table to write into");

            return table;
        }
    }
}
