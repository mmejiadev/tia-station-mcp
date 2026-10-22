using System.IO;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Compiling a WinCC Unified HMI, and reading every message the compiler gives back.
    /// </summary>
    /// <remarks>
    /// The fixture is a class project, archived 2026-10-17, whose screen holds a script dynamization
    /// of <c>OffsetLeft</c> with an automatic trigger and no tag. TIA Portal's Info tab reported it as
    /// "Compiling aborted (errors: 1)" with no text; the compiler result carries the text, three levels
    /// down. These tests say the server returns it.
    ///
    /// The assertions use names from the project — the screen, an interface of the station — and not
    /// the compiler's wording, which follows the language TIA Portal runs in.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test41UnifiedCompile
    {
        private const string UnifiedHmiPath = "PC-System_1/HMI_RT_1";
        private const string StationName = "PC-System_1";
        private const string ScreenName = "Screen_1";
        private const string StationInterface = "IE general_1";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.UnifiedArchivePath, Path.Combine(_testDirectory, "project"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CompileSoftware_UnifiedHmi_ReturnsTheErrorTheInfoTabLeftBlank()
        {
            var report = AssemblyHooks.SharedPortal.CompileSoftware(UnifiedHmiPath);

            var screenError = report.Messages.FirstOrDefault(message =>
                message.Severity == CompilationSeverity.Error
                && message.Path.Contains(ScreenName)
                && !string.IsNullOrWhiteSpace(message.Description));
            Assert.IsNotNull(screenError, "the screen's error, with its text, is missing: " + Describe(report));
        }

        /// <remarks>
        /// Unified compiles only through its station, so the hardware is compiled with it: a message
        /// about one of the station's interfaces is how a caller can tell.
        /// </remarks>
        [TestMethod]
        public void CompileSoftware_UnifiedHmi_CompilesTheWholeStation()
        {
            var report = AssemblyHooks.SharedPortal.CompileSoftware(UnifiedHmiPath);

            Assert.IsTrue(
                report.Messages.Any(message => message.Path.Contains(StationInterface)),
                "no message about the station's hardware, so the station was not compiled: " + Describe(report));
        }

        /// <remarks>
        /// The policy must be asked about what is really compiled. Asking it about the HMI's path alone
        /// let a policy that named only the HMI make the station's hardware downloadable.
        /// </remarks>
        [TestMethod]
        public void ResolveCompileTarget_UnifiedHmi_IsItsStation()
        {
            Assert.AreEqual(StationName, AssemblyHooks.SharedPortal.ResolveCompileTarget(UnifiedHmiPath));
        }

        /// <remarks>
        /// What the tool uses to keep an HMI compile out of the PLC compilation history. This asserts the
        /// classification only; that the tool skips the recording is not asserted here.
        /// </remarks>
        [TestMethod]
        public void IsHmi_UnifiedHmi_IsTrue()
        {
            Assert.IsTrue(AssemblyHooks.SharedPortal.IsHmi(UnifiedHmiPath));
        }

        [TestMethod]
        public void CompileSoftwareTool_UnifiedHmiThePolicyDoesNotName_IsRefusedForItsStation()
        {
            var response = McpServer.CompileSoftware(UnifiedHmiPath);

            Assert.AreEqual("Refused", response.Meta?["outcome"]?.GetValue<string>(), response.Message);
            StringAssert.StartsWith(response.Message, $"'{StationName}' is on no allow list");
            Assert.AreEqual(0, response.ErrorCount);
        }

        private static string Describe(CompilationReport report)
        {
            return string.Join(" | ", report.Messages.Select(message => $"[{message.Severity}] {message.Path}: {message.Description}"));
        }
    }
}
