using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// How a CPU can be reached: the PC interfaces TIA Portal offers and its online state.
    /// </summary>
    /// <remarks>
    /// None of these contacts a network: which interfaces exist depends on the machine (measured 2026-11-13: PLCSIM
    /// only while a PLCSIM Advanced instance exists, the real adapters otherwise), so the tests ask only what holds
    /// on any machine. Going online through PLCSIM is tested in <see cref="Test11Download"/>, which has a controller.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test67Online
    {
        private const string PlcName = "PLC_1";

        [TestInitialize]
        public void TestInit()
        {
            AssemblyHooks.SharedPortal.CreateProject(AssemblyHooks.CreateTestDirectory(), "Online");
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 512-1CK01-0AB0/V2.9", PlcName);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void GetPcInterfaces_NewS71500_OffersItsProfinetMode()
        {
            var interfaces = AssemblyHooks.SharedPortal.GetPcInterfaces(PlcName);

            Assert.IsTrue(interfaces.Any(pcInterface => pcInterface.Mode == "PN/IE"), string.Join(", ", interfaces.Select(pcInterface => pcInterface.Mode + ":" + pcInterface.Name)));
        }

        [TestMethod]
        public void GetOnlineState_NewS71500_IsOffline()
        {
            var state = AssemblyHooks.SharedPortal.GetOnlineState(PlcName);

            Assert.AreEqual("Offline", state);
        }

        /// <remarks>Refused before any search is sent: the interface is looked up among those offered first.</remarks>
        [TestMethod]
        public void ScanAccessibleDevices_InterfaceNotOffered_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.ScanAccessibleDevices(PlcName, "No such adapter"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void GetOnlineState_NotAPlc_Throws()
        {
            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetOnlineState("No_Such_Plc"));
        }

        [TestMethod]
        public void GoOffline_NewS71500_StaysOffline()
        {
            var state = AssemblyHooks.SharedPortal.GoOffline(PlcName);

            Assert.AreEqual("Offline", state);
        }
    }
}
