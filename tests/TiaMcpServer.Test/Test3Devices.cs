namespace TiaMcpServer.Test
{
    /// <remarks>
    /// The device and device-item paths stay in <c>[DataRow]</c>: they describe the inside of the
    /// project, so they are part of the fixture and are the same on every machine. Only the
    /// project's location on disk had to move out, and it now comes from <see cref="AssemblyHooks"/>.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test3Devices
    {
        [TestInitialize]
        public void TestInit()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        [DataRow("HMI_0")]
        [DataRow("PC-System_0")]
        [DataRow("Group1/PC-System_1")]
        [DataRow("Group1/Group1.1/PC-System_1.1")]
        [DataRow("Group1/Group1.1/Group1.1.1/PC-System_1.1.1")]
        public void GetDevice_ExistingPath_ReturnsDevice(string devicePath)
        {
            var device = AssemblyHooks.SharedPortal.GetDevice(devicePath);

            Assert.IsNotNull(device, $"No device found at '{devicePath}'");
        }

        [TestMethod]
        [DataRow("PLC_0")]
        [DataRow("PC-System_0/Software PLC_0")]
        [DataRow("HMI_0/HMI_RT_1")]
        [DataRow("Group1/PLC_1")]
        [DataRow("Group1/PC-System_1/Software PLC_1")]
        [DataRow("Group1/Group1.1/PLC_1.1")]
        [DataRow("Group1/Group1.1/PC-System_1.1/Software PLC_1.1")]
        public void GetDeviceItem_ExistingPath_ReturnsDeviceItem(string deviceItemPath)
        {
            var deviceItem = AssemblyHooks.SharedPortal.GetDeviceItem(deviceItemPath);

            Assert.IsNotNull(deviceItem, $"No device item found at '{deviceItemPath}'");
        }

        /// <remarks>
        /// A description is read once and detached, so the attributes have to come with it. Before
        /// the split the MCP layer held the live <c>Device</c> and read them itself, which worked
        /// only while the project stayed open.
        /// </remarks>
        [TestMethod]
        public void GetDevice_ExistingPath_DescriptionCarriesItsAttributes()
        {
            var device = AssemblyHooks.SharedPortal.GetDevice("PC-System_0");

            Assert.IsNotNull(device, "No device found at 'PC-System_0'");
            Assert.AreEqual("PC-System_0", device.Name);
            Assert.IsTrue(device.Attributes.Count > 0, "No attribute was read");
        }

        [TestMethod]
        public void GetDevice_UnknownPath_ReturnsNull()
        {
            var device = AssemblyHooks.SharedPortal.GetDevice("NoSuchGroup/NoSuchDevice");

            Assert.IsNull(device, "An unknown path returned a device");
        }

        [TestMethod]
        public void GetDevices_OpenProject_ReturnsEveryDevice()
        {
            var devices = AssemblyHooks.SharedPortal.GetDevices();

            Assert.IsNotNull(devices);
            Assert.IsTrue(devices.Count > 0, "The project has devices but none were returned");
        }
    }
}
