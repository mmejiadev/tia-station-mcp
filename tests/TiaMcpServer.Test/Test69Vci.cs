using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.Vci;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// TIA Portal's version control on V21: a workspace, a program exported into it, and synchronising both ways.
    /// </summary>
    /// <remarks>Measured 2026-11-13 by probes in the same shape on a new CPU 1512C.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test69Vci
    {
        private const string PlcName = "PLC_1";
        private const string Workspace = "Cell";
        private const string MainFile = "PLC_1/Blocks/Main.xml";
        private const string OriginalTitle = "\"Main Program Sweep (Cycle)\"";

        private string _testDirectory = string.Empty;
        private string _root = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            _root = Path.Combine(_testDirectory, "ws");
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "Vci");
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 512-1CK01-0AB0/V2.9", PlcName);
            AssemblyHooks.SharedPortal.CompileSoftware(PlcName);
            AssemblyHooks.SharedPortal.CreateVciWorkspace(Workspace, _root);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void ExportToVciWorkspace_ACompiledProgram_WritesItsBlocksAndTagTablesAsFiles()
        {
            var report = AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);

            CollectionAssert.Contains(report.Exported.ToList(), MainFile, string.Join(", ", report.Exported));
            Assert.IsTrue(report.Exported.Any(file => file.StartsWith("PLC_1/PLC tags/", StringComparison.Ordinal)), string.Join(", ", report.Exported));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "PLC_1", "Blocks", "Main.xml")));
        }

        [TestMethod]
        public void ExportToVciWorkspace_Twice_LeavesWhatIsMappedAlone()
        {
            AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);

            var again = AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);

            Assert.AreEqual(0, again.Exported.Count);
            CollectionAssert.Contains(again.AlreadyMapped.ToList(), MainFile);
        }

        [TestMethod]
        public void GetVciStatus_JustExported_IsAllEqual()
        {
            AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);

            var states = AssemblyHooks.SharedPortal.GetVciStatus(Workspace);

            Assert.IsTrue(states.Count > 0 && states.All(state => state.CompareState == "Equal"), string.Join("; ", states.Select(state => state.Line)));
        }

        [TestMethod]
        public void SyncVciWorkspace_AFileEditedInTheWorkspace_ReachesTheProject()
        {
            AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);
            EditMainTitle("\"Edited in the workspace\"");

            var states = AssemblyHooks.SharedPortal.SyncVciWorkspace(Workspace, VciDirection.WorkspaceToProject, Path.Combine(_testDirectory, "backup"));

            Assert.AreEqual("Equal", states.Single(state => state.File == MainFile).CompareState);
        }

        [TestMethod]
        public void GetVciStatus_AFileEditedInTheWorkspace_SaysWhichSideChanged()
        {
            AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);
            EditMainTitle("\"Edited in the workspace\"");

            var main = AssemblyHooks.SharedPortal.GetVciStatus(Workspace).Single(state => state.File == MainFile);

            Assert.AreEqual("WorkspaceFileChanged", main.ChangedSide, main.Line);
        }

        /// <remarks>Copying the project out would overwrite the edited file, so nothing is copied and the file keeps the edit.</remarks>
        [TestMethod]
        public void SyncVciWorkspace_ProjectToWorkspaceOverAnEditedFile_ThrowsAndKeepsTheEdit()
        {
            AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);
            EditMainTitle("\"Edited in the workspace\"");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.SyncVciWorkspace(Workspace, VciDirection.ProjectToWorkspace, Path.Combine(_testDirectory, "backup")));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(File.ReadAllText(MainPath()), "Edited in the workspace", StringComparison.Ordinal);
        }

        [TestMethod]
        public void SyncVciWorkspace_IntoTheProject_ExportsTheProgramFirst()
        {
            var backup = Path.Combine(_testDirectory, "backup");
            AssemblyHooks.SharedPortal.ExportToVciWorkspace(Workspace, PlcName);
            EditMainTitle("\"Edited in the workspace\"");

            AssemblyHooks.SharedPortal.SyncVciWorkspace(Workspace, VciDirection.WorkspaceToProject, backup);

            Assert.IsTrue(Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Any(), "nothing was exported to " + backup);
        }

        [TestMethod]
        public void CreateVciWorkspace_ANameTaken_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateVciWorkspace(Workspace, Path.Combine(_testDirectory, "other")));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void GetVciWorkspaces_AfterCreating_ListsItWithItsFolder()
        {
            var workspace = AssemblyHooks.SharedPortal.GetVciWorkspaces().Single(item => item.Name == Workspace);

            Assert.AreEqual(Path.GetFullPath(_root).TrimEnd('\\'), workspace.RootPath.TrimEnd('\\'));
        }

        private void EditMainTitle(string title)
        {
            var path = MainPath();

            File.WriteAllText(path, File.ReadAllText(path).Replace(OriginalTitle, title));
        }

        private string MainPath()
        {
            return Path.Combine(_root, "PLC_1", "Blocks", "Main.xml");
        }
    }
}
