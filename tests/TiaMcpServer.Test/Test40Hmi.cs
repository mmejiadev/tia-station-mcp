using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Classic WinCC panels: reading one, exporting it, importing into it and generating for it.
    /// </summary>
    /// <remarks>
    /// <c>TestProject1</c>'s own panel has no connection to a PLC, so every test adds the class panel
    /// to a copy of the project retrieved for it. Measured on 2026-10-14 with that panel:
    /// <list type="bullet">
    /// <item>its type identifier is accepted;</item>
    /// <item>its HMI target does <b>not</b> resolve by the device's name: it sits on a device item TIA
    /// Portal names itself (<c>HMI_1/HMI_RT_2</c>), so the path is read with <c>GetSoftwarePaths</c>;</item>
    /// <item>a new panel has no screen at all;</item>
    /// <item>it has no connection to the PLC, and importing a tag table naming a missing connection
    /// threw <c>NonRecoverableException</c> and disposed of the project — what the link check exists
    /// to prevent.</item>
    /// </list>
    /// So tags generated from the PLC cannot be tested here, and the screens use internal HMI tags,
    /// which need no connection. Generated PLC tags were measured on the teacher's project instead.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test40Hmi
    {
        private const string PanelTypeIdentifier = "OrderNumber:6AV2 124-0JC01-0AX0/17.0.0.0";
        private const string PanelName = "HMI_1";

        private static readonly string[] OnlyTheStartScreenMissing = { "A start screen has not been configured." };

        private string _testDirectory = string.Empty;
        private string _backupDirectory = string.Empty;
        private string _hmiPath = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            _backupDirectory = Path.Combine(_testDirectory, "backup");

            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
            AssemblyHooks.SharedPortal.AddDevice(PanelTypeIdentifier, PanelName);
            _hmiPath = AssemblyHooks.SharedPortal.GetSoftwarePaths(PanelName).Single();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>
        /// The device's name is what anybody tries first, and it finds nothing: the refusal has to be
        /// where the right path is learnt.
        /// </remarks>
        [TestMethod]
        public void GetHmiInfo_DeviceNameAlone_ThrowsNotFoundNamingTheRealPath()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetHmiInfo(PanelName));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
            StringAssert.Contains(failure.Message, _hmiPath, System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void GetHmiInfo_PlcPath_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetHmiInfo(Settings.Project1PlcSoftwarePath0));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        [TestMethod]
        public void GenerateHmiScreen_TextOnly_PutsTheScreenOnThePanel()
        {
            GenerateTextScreen("Title");

            CollectionAssert.Contains(AssemblyHooks.SharedPortal.GetHmiInfo(_hmiPath).Screens.ToArray(), "Title");
        }

        [TestMethod]
        public void ExportHmi_PanelWithAScreen_WritesItWithoutFailures()
        {
            GenerateTextScreen("Title");
            var exportDirectory = Path.Combine(_testDirectory, "export");

            var report = AssemblyHooks.SharedPortal.ExportHmi(_hmiPath, exportDirectory);

            Assert.AreEqual(0, report.Failures.Count, string.Join("\n", report.Failures));
            Assert.IsTrue(File.Exists(Path.Combine(exportDirectory, "Screens", "Title.xml")), "the screen was not exported");
        }

        [TestMethod]
        public void ExportHmi_DirectoryNotEmpty_ThrowsInvalidState()
        {
            Directory.CreateDirectory(_testDirectory);
            File.WriteAllText(Path.Combine(_testDirectory, "already-here.txt"), "x");

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ExportHmi(_hmiPath, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>The round trip the import tools exist for: what ExportHmi wrote goes back in.</remarks>
        [TestMethod]
        public void ImportHmiScreen_AnExportedScreen_ImportsItBack()
        {
            var screenFile = ExportGeneratedScreen();

            var imported = AssemblyHooks.SharedPortal.ImportHmiScreen(_hmiPath, string.Empty, screenFile, _backupDirectory);

            Assert.AreEqual("Title", imported.Single());
        }

        [TestMethod]
        public void ImportHmiScreen_AnExportedScreen_SavesThePanelFirst()
        {
            var screenFile = ExportGeneratedScreen();

            AssemblyHooks.SharedPortal.ImportHmiScreen(_hmiPath, string.Empty, screenFile, _backupDirectory);

            Assert.IsTrue(File.Exists(Path.Combine(_backupDirectory, "Screens", "Title.xml")), "the screen was replaced and its previous state is not in the backup");
        }

        [TestMethod]
        public void ImportHmiScreen_MissingFolder_ThrowsNotFound()
        {
            var screenFile = ExportGeneratedScreen();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ImportHmiScreen(_hmiPath, "NoSuchFolder", screenFile, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        /// <remarks>
        /// A screen regenerated without a number keeps the one it had: a free one would break every
        /// button and screen-number tag that opens it by number.
        /// </remarks>
        [TestMethod]
        public void GenerateHmiScreen_RegeneratedWithoutNumber_KeepsItsNumber()
        {
            var numbered = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""Title"", ""number"": 7, ""items"": [ { ""type"": ""text"", ""left"": 20, ""top"": 20, ""text"": ""First"" } ] }");
            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, numbered, Path.Combine(_testDirectory, "first"));

            GenerateTextScreen("Title");

            var exportDirectory = Path.Combine(_testDirectory, "export");
            AssemblyHooks.SharedPortal.ExportHmi(_hmiPath, exportDirectory);
            StringAssert.Contains(File.ReadAllText(Path.Combine(exportDirectory, "Screens", "Title.xml")), "<Number>7</Number>", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void GenerateHmiScreen_NumberAnotherScreenHas_ThrowsInvalidStateNamingIt()
        {
            var first = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""First"", ""number"": 7, ""items"": [] }");
            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, first, Path.Combine(_testDirectory, "first"));
            var second = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""Second"", ""number"": 7, ""items"": [] }");

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, second, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "First", System.StringComparison.Ordinal);
        }

        /// <remarks>An imported screen is held to the same number rule as a generated one.</remarks>
        [TestMethod]
        public void ImportHmiScreen_NumberAnotherScreenHas_ThrowsInvalidState()
        {
            var screenFile = ExportGeneratedScreen();
            var first = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""First"", ""number"": 7, ""items"": [] }");
            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, first, Path.Combine(_testDirectory, "first"));
            var text = File.ReadAllText(screenFile);
            File.WriteAllText(screenFile, System.Text.RegularExpressions.Regex.Replace(text, "<Number>[0-9]+</Number>", "<Number>7</Number>"));

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ImportHmiScreen(_hmiPath, string.Empty, screenFile, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>A PLC tag a hand-written table names must exist too: no link is let through on faith.</remarks>
        [TestMethod]
        public void ImportHmiTagTable_PlcTagThatDoesNotExist_IsRefused()
        {
            var file = WriteTagTable("Linked", ("TiaMcpLinked", @"<ControllerTag TargetID=""@OpenLink""><Name>NoSuchPlcTag</Name></ControllerTag>"));

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ImportHmiTagTable(_hmiPath, file, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "NoSuchPlcTag", System.StringComparison.Ordinal);
        }

        /// <remarks>Fail closed: a kind of link nothing here knows how to check is not let through.</remarks>
        [TestMethod]
        public void ImportHmiScreen_LinkOfAKindNotChecked_IsRefused()
        {
            var screenFile = ExportGeneratedScreen();
            var text = File.ReadAllText(screenFile);
            var at = text.IndexOf("</AttributeList>", System.StringComparison.Ordinal) + "</AttributeList>".Length;
            File.WriteAllText(screenFile, text.Insert(at, @"<LinkList><Mystery TargetID=""@OpenLink""><Name>Anything</Name></Mystery></LinkList>"));

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ImportHmiScreen(_hmiPath, string.Empty, screenFile, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "Mystery", System.StringComparison.Ordinal);
        }

        /// <remarks>
        /// The regression this class found. The same import ended the project session on 2026-10-14;
        /// now it is refused before TIA Portal sees it, and the project is still there afterwards.
        /// </remarks>
        [TestMethod]
        public void ImportHmiTagTable_ConnectionNotOnThePanel_IsRefusedAndTheProjectStaysOpen()
        {
            var file = WriteTagTable("Linked", ("TiaMcpLinked", @"<Connection TargetID=""@OpenLink""><Name>NoSuchConnection</Name></Connection><ControllerTag TargetID=""@OpenLink""><Name>Marxa</Name></ControllerTag>"));

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ImportHmiTagTable(_hmiPath, file, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            Assert.IsNotNull(AssemblyHooks.SharedPortal.GetHmiInfo(_hmiPath), "the project did not survive the refused import");
        }

        [TestMethod]
        public void GenerateHmiTags_NoConnectionOnThePanel_IsRefusedAndTheProjectStaysOpen()
        {
            CreateMemoryTag("TiaMcpHmiBit", "%M100.0");

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GenerateHmiTags(
                    new HmiTagGenerationRequest(Settings.Project1PlcSoftwarePath0, _hmiPath, "Generated", HmiTagTableDocument.DefaultConnection),
                    _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            Assert.IsNotNull(AssemblyHooks.SharedPortal.GetHmiInfo(_hmiPath), "the project did not survive the refused generation");
        }

        [TestMethod]
        public void GenerateHmiScreen_TagNotOnThePanel_ThrowsInvalidStateNamingIt()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, ControlScreen(), _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "TiaMcpBit", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void GenerateHmiScreen_ButtonOpeningAMissingScreen_ThrowsInvalidState()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""Main"", ""items"": [
                { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Go"", ""press"": ""activateScreen"", ""screen"": ""Nowhere"" } ] }");

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, screen, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>
        /// Buttons, a pilot light, an animation by ranges and a visibility animation, on internal tags
        /// imported first: what TIA Portal makes of every shape the generator writes.
        /// </remarks>
        [TestMethod]
        public void GenerateHmiScreen_InternalTagsImportedFirst_PutsTheScreenOnThePanel()
        {
            ImportInternalTags();

            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, ControlScreen(), _backupDirectory);

            CollectionAssert.Contains(AssemblyHooks.SharedPortal.GetHmiInfo(_hmiPath).Screens.ToArray(), "Control");
        }

        /// <remarks>
        /// The claim that matters: not that TIA Portal took the XML, but that the panel compiles with
        /// every shape the generator writes on it. Internal tags need no connection, so it can.
        ///
        /// Measured 2026-10-14: the one error is "A start screen has not been configured", from the
        /// runtime settings of a panel created from nothing — a class project has one — and there is no
        /// warning. That error is the only one allowed; any other, on any generated item, fails.
        /// </remarks>
        [TestMethod]
        public void GenerateHmiScreen_EveryKindOfItem_CompilesWithNoErrorButTheMissingStartScreen()
        {
            ImportInternalTags();
            var list = HmiTextListDocument.Build(@"{ ""name"": ""TiaMcpModes"", ""entries"": [ { ""value"": 0, ""text"": ""STOPPED"" }, { ""value"": 1, ""text"": ""AUTO"" } ] }");
            AssemblyHooks.SharedPortal.GenerateHmiTextList(_hmiPath, list.Key, list.Value, Path.Combine(_testDirectory, "list"));
            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, ControlScreen(), Path.Combine(_testDirectory, "control"));
            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, SymbolicScreen(), _backupDirectory);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(_hmiPath);

            var described = string.Join("\n", report.Messages.Select(message => $"[{message.Severity}] {message.Path} — {message.Description}"));
            var errors = report.Messages
                .Where(message => message.Severity == CompilationSeverity.Error && message.Description.Length > 0 && !message.Description.StartsWith("Compiling aborted", System.StringComparison.Ordinal))
                .Select(message => message.Description)
                .ToArray();
            CollectionAssert.AreEqual(OnlyTheStartScreenMissing, errors, described);
            Assert.AreEqual(0, report.WarningCount, described);
        }

        /// <remarks>
        /// The shapes of 2026-10-21 — rectangle, plain circle, line, a group and a second layer — need no
        /// tag, so they compile on a panel created from nothing. The start-screen error is the only one
        /// allowed, as above.
        /// </remarks>
        [TestMethod]
        public void GenerateHmiScreen_ShapesGroupAndLayer_CompileWithNoErrorButTheMissingStartScreen()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""Shapes"", ""items"": [
                { ""type"": ""rectangle"", ""left"": 20, ""top"": 20, ""width"": 120, ""height"": 200, ""backColor"": ""blue"" },
                { ""type"": ""line"", ""left"": 140, ""top"": 120, ""endLeft"": 300, ""endTop"": 120, ""lineWidth"": 4 },
                { ""type"": ""group"", ""name"": ""Tank"", ""layer"": 2, ""items"": [
                    { ""type"": ""circle"", ""left"": 300, ""top"": 100, ""width"": 40 },
                    { ""type"": ""text"", ""left"": 300, ""top"": 150, ""text"": ""T1"" } ] } ] }");

            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, screen, _backupDirectory);
            var report = AssemblyHooks.SharedPortal.CompileSoftware(_hmiPath);

            var described = string.Join("\n", report.Messages.Select(message => $"[{message.Severity}] {message.Path} — {message.Description}"));
            var errors = report.Messages
                .Where(message => message.Severity == CompilationSeverity.Error && message.Description.Length > 0 && !message.Description.StartsWith("Compiling aborted", System.StringComparison.Ordinal))
                .Select(message => message.Description)
                .ToArray();
            CollectionAssert.AreEqual(OnlyTheStartScreenMissing, errors, described);
        }

        [TestMethod]
        public void GenerateHmiScreen_SymbolicFieldAfterItsTextList_PutsTheScreenOnThePanel()
        {
            ImportInternalTags();
            var list = HmiTextListDocument.Build(@"{ ""name"": ""TiaMcpModes"", ""entries"": [ { ""value"": 0, ""text"": ""STOPPED"" }, { ""value"": 1, ""text"": ""AUTO"" } ] }");
            AssemblyHooks.SharedPortal.GenerateHmiTextList(_hmiPath, list.Key, list.Value, Path.Combine(_testDirectory, "list"));

            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, SymbolicScreen(), _backupDirectory);

            CollectionAssert.Contains(AssemblyHooks.SharedPortal.GetHmiInfo(_hmiPath).Screens.ToArray(), "Modes");
        }

        [TestMethod]
        public void GenerateHmiScreen_TextListNotOnThePanel_ThrowsInvalidStateNamingIt()
        {
            ImportInternalTags();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, SymbolicScreen(), _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "TiaMcpModes", System.StringComparison.Ordinal);
        }

        private void GenerateTextScreen(string name)
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": """ + name + @""", ""items"": [ { ""type"": ""text"", ""left"": 20, ""top"": 20, ""text"": ""Generated"" } ] }");

            AssemblyHooks.SharedPortal.GenerateHmiScreen(_hmiPath, string.Empty, screen, Path.Combine(_testDirectory, "screen-" + name));
        }

        private string ExportGeneratedScreen()
        {
            GenerateTextScreen("Title");
            var exportDirectory = Path.Combine(_testDirectory, "export");
            AssemblyHooks.SharedPortal.ExportHmi(_hmiPath, exportDirectory);

            return Path.Combine(exportDirectory, "Screens", "Title.xml");
        }

        /// <remarks>Internal tags: no connection, a data type instead, as the teacher's screen-number tag is written.</remarks>
        private void ImportInternalTags()
        {
            var bit = @"<DataType TargetID=""@OpenLink""><Name>Bool</Name></DataType><HmiDataType TargetID=""@OpenLink""><Name>Bool</Name></HmiDataType>";
            var word = @"<DataType TargetID=""@OpenLink""><Name>UInt</Name></DataType><HmiDataType TargetID=""@OpenLink""><Name>UInt</Name></HmiDataType>";

            var file = WriteTagTable("Internal", ("TiaMcpBit", bit), ("TiaMcpMode", word));
            AssemblyHooks.SharedPortal.ImportHmiTagTable(_hmiPath, file, Path.Combine(_testDirectory, "tags"));
        }

        private string WriteTagTable(string tableName, params (string Name, string Links)[] tagsAndLinks)
        {
            var tags = tagsAndLinks
                .Select((tag, index) =>
                    $@"<Hmi.Tag.Tag ID=""{index + 1:X}"" CompositionName=""Tags""><AttributeList><Name>{tag.Name}</Name></AttributeList>" +
                    $@"<LinkList><AcquisitionCycle TargetID=""@OpenLink""><Name>1 s</Name></AcquisitionCycle>{tag.Links}</LinkList></Hmi.Tag.Tag>");

            var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?><Document><Engineering version=""V20"" /><Hmi.Tag.TagTable ID=""0""><AttributeList><Name>{tableName}</Name></AttributeList><ObjectList>{string.Concat(tags)}</ObjectList></Hmi.Tag.TagTable></Document>";
            var file = Path.Combine(_testDirectory, tableName + ".xml");

            Directory.CreateDirectory(_testDirectory);
            File.WriteAllText(file, xml);

            return file;
        }

        private void CreateMemoryTag(string name, string address)
        {
            var table = AssemblyHooks.SharedPortal.GetTagTables(Settings.Project1PlcSoftwarePath0).First(candidate => candidate.IsDefault);

            AssemblyHooks.SharedPortal.CreateTag(
                Settings.Project1PlcSoftwarePath0, new TagDefinition($"{table.Path}/{name}", "Bool", address), Path.Combine(_testDirectory, "tag-backup"));
        }

        private static HmiScreenDescription ControlScreen()
        {
            return HmiScreenDescriptionReader.Parse(@"{ ""name"": ""Control"", ""items"": [
                { ""type"": ""button"", ""left"": 40, ""top"": 40, ""text"": ""Marxa"", ""press"": ""setBitWhilePressed"", ""tag"": ""TiaMcpBit"" },
                { ""type"": ""pilotLight"", ""left"": 200, ""top"": 40, ""tag"": ""TiaMcpBit"", ""flashing"": true, ""visibleTag"": ""TiaMcpBit"" },
                { ""type"": ""ioField"", ""left"": 40, ""top"": 120, ""tag"": ""TiaMcpMode"", ""animations"": [
                    { ""type"": ""appearance"", ""tag"": ""TiaMcpMode"", ""ranges"": [ { ""from"": 0, ""to"": 4, ""backColor"": ""green"" }, { ""from"": 5, ""to"": 9, ""backColor"": ""red"", ""flashing"": true } ] },
                    { ""type"": ""visibility"", ""tag"": ""TiaMcpBit"", ""bit"": 0, ""visible"": false } ] } ] }");
        }

        private static HmiScreenDescription SymbolicScreen()
        {
            return HmiScreenDescriptionReader.Parse(@"{ ""name"": ""Modes"", ""items"": [
                { ""type"": ""symbolicField"", ""left"": 40, ""top"": 40, ""width"": 200, ""tag"": ""TiaMcpMode"", ""textList"": ""TiaMcpModes"", ""mode"": ""Output"",
                  ""animations"": [ { ""type"": ""appearance"", ""tag"": ""TiaMcpMode"", ""ranges"": [ { ""from"": 0, ""backColor"": ""grey"" }, { ""from"": 1, ""backColor"": ""green"" } ] } ] },
                { ""type"": ""dateTime"", ""left"": 600, ""top"": 0, ""show"": ""both"" } ] }");
        }
    }
}
