using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class ProjectFilePathTests
    {
        [TestMethod]
        [DataRow(@"C:\Projects\Hopper\Hopper.ap20", @"c:\projects\hopper\HOPPER.AP20")]
        [DataRow(@"C:\Projects\Hopper\Hopper.ap20", "C:/Projects/Hopper/Hopper.ap20")]
        [DataRow(@"C:\Projects\Hopper\Hopper.ap20", @"  C:\Projects\Hopper\Hopper.ap20  ")]
        public void AreSame_OneFileWrittenTwoWays_IsTheSame(string first, string second)
        {
            Assert.IsTrue(ProjectFilePath.AreSame(first, second));
        }

        [TestMethod]
        public void AreSame_OneNameInTwoFolders_IsNotTheSame()
        {
            Assert.IsFalse(ProjectFilePath.AreSame(@"C:\A\Hopper\Hopper.ap20", @"C:\B\Hopper\Hopper.ap20"));
        }

        /// <remarks>An empty path is "no project open", and two of those are not one project.</remarks>
        [TestMethod]
        [DataRow("", "")]
        [DataRow("", @"C:\Projects\Hopper\Hopper.ap20")]
        public void AreSame_AnEmptyPath_IsNeverTheSame(string first, string second)
        {
            Assert.IsFalse(ProjectFilePath.AreSame(first, second));
        }
    }
}
