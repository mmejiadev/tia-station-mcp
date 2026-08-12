using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// The closed loop the project exists for: write SCL, compile, read what the compiler said.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test9WriteScl
    {
        private const string ValidScl = @"FUNCTION ""FC_GeneratedByTest"" : Void
VERSION : 0.1
BEGIN
    ; // deliberately empty body
END_FUNCTION
";

        private const string InvalidScl = @"FUNCTION ""FC_BrokenByTest"" : Void
VERSION : 0.1
BEGIN
    #NoSuchVariable := 1;
END_FUNCTION
";

        private string _backupDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _backupDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void WriteScl_ValidSource_GeneratesTheBlock()
        {
            var generated = AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, ValidScl, _backupDirectory);

            CollectionAssert.Contains(generated.ToList(), "FC_GeneratedByTest");
        }

        [TestMethod]
        public void WriteScl_ValidSource_TakesABackupFirst()
        {
            AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, ValidScl, _backupDirectory);

            // The repository rule is that nothing is overwritten without exporting the previous
            // state. A backup that is not on disk is not a backup.
            Assert.IsTrue(
                Directory.EnumerateFiles(_backupDirectory, "*.xml", SearchOption.AllDirectories).Any(),
                $"No backup was written to {_backupDirectory}");
        }

        [TestMethod]
        public void WriteScl_GeneratedBlock_IsFoundInTheProgram()
        {
            AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, ValidScl, _backupDirectory);

            var block = AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, "FC_GeneratedByTest");

            Assert.IsNotNull(block, "The generated block is not in the program");
        }

        [TestMethod]
        public void WriteScl_ThenCompile_ReportsSuccess()
        {
            AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, ValidScl, _backupDirectory);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(
                report.IsSuccessful,
                $"Generated SCL did not compile:\n{string.Join("\n", report.Errors)}");
        }

        [TestMethod]
        public void WriteScl_BrokenSource_ProducesActionableCompilerErrors()
        {
            // Either TIA Portal refuses to generate at all, or it generates a block that does not
            // compile. Both are acceptable; what matters is that the caller is told what is wrong
            // rather than being handed an object whose ToString is its type name.
            try
            {
                AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, InvalidScl, _backupDirectory);
            }
            catch (PortalException exception)
            {
                Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
                StringAssert.Contains(exception.Message, "SCL");

                return;
            }

            var report = AssemblyHooks.SharedPortal.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            Assert.IsFalse(report.IsSuccessful, "Broken SCL compiled cleanly");
            Assert.IsTrue(report.Errors.Count > 0, "The compile failed but reported no error messages");
            Assert.IsTrue(
                report.Errors.Any(error => !string.IsNullOrWhiteSpace(error.Description)),
                "The compiler errors carry no description, so nothing can be fixed from them");
        }

        [TestMethod]
        public void WriteScl_EmptySource_ThrowsInvalidParams()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, "   ", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void WriteScl_NoBackupDirectory_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, ValidScl, string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }
    }
}
