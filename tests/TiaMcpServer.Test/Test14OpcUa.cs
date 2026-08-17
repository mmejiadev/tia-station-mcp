using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test14OpcUa
    {
        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void GetOpcUaInterfaces_AnyCpu_ReturnsAListRatherThanFailing()
        {
            // A CPU that publishes nothing is the normal case, not an error. Callers iterate the
            // result, so an empty list has to come back as a list.
            var interfaces = AssemblyHooks.SharedPortal.GetOpcUaInterfaces(Settings.Project1PlcSoftwarePath0);

            Assert.IsNotNull(interfaces);
        }

        [TestMethod]
        public void ExportOpcUaInterface_UnknownInterface_ThrowsNotFoundAndNamesWhatExists()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ExportOpcUaInterface(
                    Settings.Project1PlcSoftwarePath0,
                    "NoSuchInterface",
                    Path.Combine(_testDirectory, "iface.xml")));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
            // Naming the alternatives is the difference between an error a caller can act on and
            // one that sends them back to the GUI to look.
            StringAssert.Contains(exception.Message, "Available:");
        }

        [TestMethod]
        public void ExportOpcUaInterface_UnknownSoftwarePath_ThrowsNotFound()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ExportOpcUaInterface(
                    "NoSuchDevice/NoSuchPlc",
                    "Server interface_1",
                    Path.Combine(_testDirectory, "iface.xml")));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void ExportOpcUaInterface_EmptyArguments_ThrowsInvalidParams()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ExportOpcUaInterface(Settings.Project1PlcSoftwarePath0, "  ", "  "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }
    }
}
