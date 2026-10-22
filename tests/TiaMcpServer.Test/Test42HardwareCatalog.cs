using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The hardware catalogue, and adding from it every kind of device the class uses.
    /// </summary>
    /// <remarks>
    /// The identifiers are the ones the class projects use (read from the 2025-26 class corpus on
    /// 2026-10-20) at the firmware those projects carry. Each test first finds its identifier in the
    /// catalogue and only then adds it, so a device is never added from a string nobody checked.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test42HardwareCatalog
    {
        private const string ComfortPanelArticle = "6AV2 124-0JC01-0AX0";
        private const string ComfortPanelTypeIdentifier = "OrderNumber:6AV2 124-0JC01-0AX0/17.0.0.0";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void FindCatalogEntries_ClassComfortPanel_ReturnsItsTypeIdentifier()
        {
            var result = AssemblyHooks.SharedPortal.FindCatalogEntries(ComfortPanelArticle);

            Assert.IsTrue(
                result.Entries.Any(entry => entry.TypeIdentifier == ComfortPanelTypeIdentifier),
                "found: " + string.Join(", ", result.Entries.Select(entry => entry.TypeIdentifier)));
        }

        [TestMethod]
        public void FindCatalogEntries_UnknownProduct_ReturnsNothing()
        {
            var result = AssemblyHooks.SharedPortal.FindCatalogEntries("no such product zzz");

            Assert.AreEqual(0, result.TotalFound);
        }

        [TestMethod]
        public void FindCatalogEntries_BlankFilter_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.FindCatalogEntries(" "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// Only <c>OrderNumber:6ES7 511-1AL03-0AB0/V4.0</c> had ever been added before 2026-10-20. These
        /// rows are the S7-1200 and S7-300 of the class, the Comfort panels and the Unified Basic panel.
        /// </remarks>
        [TestMethod]
        [DataRow("6ES7 215-1BG40-0XB0", "OrderNumber:6ES7 215-1BG40-0XB0/V4.7", "PLC_1200")]
        [DataRow("6ES7 313-6CF03-0AB0", "OrderNumber:6ES7 313-6CF03-0AB0/V2.6", "PLC_300")]
        [DataRow("6AV2 124-0JC01-0AX0", "OrderNumber:6AV2 124-0JC01-0AX0/17.0.0.0", "HMI_TP900")]
        [DataRow("6AV2 124-0MC01-0AX0", "OrderNumber:6AV2 124-0MC01-0AX0/17.0.0.0", "HMI_TP1200")]
        [DataRow("6AV2 123-3GB32-0AW0", "OrderNumber:6AV2 123-3GB32-0AW0/20.0.0.2", "HMI_MTP700")]
        public void AddDevice_ClassDeviceFromTheCatalogue_AddsIt(string article, string typeIdentifier, string deviceName)
        {
            var catalogued = AssemblyHooks.SharedPortal.FindCatalogEntries(article).Entries
                .Any(entry => entry.TypeIdentifier == typeIdentifier);
            Assert.IsTrue(catalogued, $"{typeIdentifier} is not in this installation's catalogue");
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "CatalogDevices");

            AssemblyHooks.SharedPortal.AddDevice(typeIdentifier, deviceName);

            Assert.IsTrue(
                AssemblyHooks.SharedPortal.GetDevices().Any(device => device.Name == deviceName),
                $"{deviceName} is not among the project's devices after adding {typeIdentifier}");
        }
    }
}
