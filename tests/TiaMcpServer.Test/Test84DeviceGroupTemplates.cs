using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// A PLC, a WinCC Unified PC station and their integrated connection, built through the server alone in a device group,
    /// made a template and placed: nothing done by hand, on TIA Portal V21.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-24 by a probe in the same shape: the group and its devices are created through Openness, the copy
    /// placed keeps its connection, and a second copy is connected to its own PLC. The passwords here are made up.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test84DeviceGroupTemplates
    {
        private const string Cpu1512 = "OrderNumber:6ES7 512-1CK01-0AB0/V2.9";
        private const string UnknownType = "OrderNumber:0000 000-0000-0000/V0.0";
        private const string Group = "Cell";
        private const string PlcName = "PLC_1";
        private const string StationName = "PC-System_1";
        private const string SubnetName = "PN/IE_1";
        private const string Template = "Template_Cell";
        private const string Secret = "Plc_Cell";
        private const string Password = "Made-Up-For-Tests-1";

        private static string _library = string.Empty;

        private string _testDirectory = string.Empty;

        [ClassInitialize]
        public static void MakeTheTemplate(TestContext context)
        {
            var directory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(directory, "CellSource");
            BuildTheCell(Path.Combine(directory, "backup"));
            _library = AssemblyHooks.SharedPortal.CreateGlobalLibrary(Path.Combine(directory, "libraries"), "Kit")["library"]!.GetValue<string>();
            AssemblyHooks.SharedPortal.CreateDeviceGroupTemplate(Group, _library, Template, Path.Combine(directory, "backup-library"));
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "Cells");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>The bug of 2026-11-24: the topology gave <c>PLC_1/PLC_1/...</c>, which no network tool resolved.</remarks>
        [TestMethod]
        public void AddDevice_InAGroup_TopologyPathsStartWithTheGroup()
        {
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName, Group);

            var node = NodeOf(PlcName);

            StringAssert.StartsWith(node.DevicePath, Group + "/" + PlcName + "/", StringComparison.Ordinal);
        }

        [TestMethod]
        public void CreateSubnet_OnATopologyPathInAGroup_ConnectsTheNode()
        {
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName, Group);
            var node = NodeOf(PlcName);

            AssemblyHooks.SharedPortal.CreateSubnet(node.DevicePath, node.InterfaceName, SubnetName, Backup());

            Assert.AreEqual(SubnetName, NodeOf(PlcName).SubnetName);
        }

        [TestMethod]
        public void AddPcStation_InAGroup_ItsHmiIsUnderTheGroup()
        {
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPcV21, Group);

            CollectionAssert.Contains(AssemblyHooks.SharedPortal.GetSoftwarePaths(StationName).ToList(), Group + "/" + StationName + "/HMI_RT_1");
        }

        [TestMethod]
        public void SetFullAccessOfAddedDevice_AStationInAGroup_OpensItsPlc()
        {
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName, Group);

            var report = AssemblyHooks.SharedPortal.SetFullAccessOfAddedDevice(Group + "/" + PlcName, Backup());

            Assert.AreEqual(1, report.Plcs.Count);
        }

        [TestMethod]
        public void AddDevice_AGroupPath_ThrowsInvalidParamsAndAddsNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName, "Cell/Line"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetSoftwarePaths().Count);
        }

        /// <remarks>A group the call made is taken back with the device it could not create.</remarks>
        [TestMethod]
        public void AddDevice_AnUnknownTypeInANewGroup_LeavesNoGroup()
        {
            const string NewGroup = "Line_Refused";

            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.AddDevice(UnknownType, PlcName, NewGroup));

            Assert.IsFalse(AssemblyHooks.SharedPortal.GetProjectTree().Contains(NewGroup), AssemblyHooks.SharedPortal.GetProjectTree());
        }

        /// <remarks>
        /// Review of 2026-11-25: the rollback wrapped the creation's failure as WriteFailed, so an unknown order number was
        /// an operation failure in a new group and invalid input at the top of the project.
        /// </remarks>
        [TestMethod]
        public void AddDevice_AnUnknownTypeInANewGroup_ThrowsInvalidParamsAsAtTheTop()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.AddDevice(UnknownType, PlcName, "Line_Refused"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        /// <remarks>Only a group the call made is taken back: one already holding a station stays, with it.</remarks>
        [TestMethod]
        public void AddDevice_AnUnknownTypeInAnExistingGroup_KeepsTheGroupAndItsStation()
        {
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName, Group);

            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.AddDevice(UnknownType, "PLC_Refused", Group));

            CollectionAssert.Contains(AssemblyHooks.SharedPortal.GetSoftwarePaths(PlcName).ToList(), Group + "/" + PlcName + "/" + PlcName);
        }

        /// <remarks>
        /// Review of 2026-11-25: a station named <c>Cell/PLC_9</c> at the top is the policy target of <c>PLC_9</c> in group
        /// <c>Cell</c>, so a rule meant for the group would have let it be created outside it.
        /// </remarks>
        [TestMethod]
        public void AddDevice_ANameWithTheSeparator_ThrowsInvalidParamsAndAddsNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.AddDevice(Cpu1512, Group + "/PLC_9"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetSoftwarePaths().Count);
        }

        [TestMethod]
        public void CreateDevicesFromTemplate_BuiltThroughTheServer_ThePlcCompilesAndTheHmiHasItsConnection()
        {
            var placed = Place("PN_Cell");

            Assert.AreEqual(2, placed["joined"]!.AsArray().Count, placed.ToJsonString());
            Assert.AreEqual(0, ErrorsOf(placed, PlcName), placed.ToJsonString());
            Assert.AreEqual(PlcName, PartnersOf(Group).Single());
        }

        /// <remarks>Measured 2026-11-24: the second copy's HMI is connected to the second copy's PLC, not to the first.</remarks>
        [TestMethod]
        public void CreateDevicesFromTemplate_Twice_EachHmiIsConnectedToItsOwnPlc()
        {
            Place("PN_One");

            var second = Place("PN_Two");

            var group = second["group"]!.GetValue<string>();
            var plc = second["devices"]!.AsArray().Select(device => device!.GetValue<string>()).Single(name => name.StartsWith("PLC_", StringComparison.Ordinal));
            Assert.AreNotEqual(PlcName, plc);
            Assert.AreEqual(plc, PartnersOf(group).Single());
        }

        /// <remarks>Whatever else the new runtime still lacks (a password, a start screen: Test43), nothing is about the connection.</remarks>
        [TestMethod]
        public void CreateDevicesFromTemplate_WithASecret_NoHmiErrorIsAboutTheConnection()
        {
            var placed = Place("PN_Cell");

            var hmi = placed["compile"]!.AsArray().Single(device => device!["device"]!.GetValue<string>() == StationName)!;
            var messages = hmi["messages"]!.AsArray().Select(message => message!.GetValue<string>()).ToList();
            Assert.IsFalse(messages.Any(message => message.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0), string.Join(" | ", messages));
        }

        private static void BuildTheCell(string backup)
        {
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName, Group);
            AssemblyHooks.SharedPortal.SetFullAccessOfAddedDevice(Group + "/" + PlcName, backup + "-access");
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPcV21, Group);
            var plc = NodeOf(PlcName);
            var pc = NodeOf(StationName);
            AssemblyHooks.SharedPortal.CreateSubnet(plc.DevicePath, plc.InterfaceName, SubnetName, backup + "-subnet");
            AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(pc.DevicePath, pc.InterfaceName, SubnetName, backup + "-connect");
            AssemblyHooks.SharedPortal.SetNodeAddress(pc.DevicePath, pc.InterfaceName, "192.168.0.2", backup + "-address");
            AssemblyHooks.SharedPortal.CreateHmiConnection(Group + "/" + StationName + "/HMI_RT_1", Group + "/" + PlcName + "/" + PlcName, backup + "-connection");
        }

        private static JsonObject Place(string subnet)
        {
            var secrets = new MadeUpSecrets(new Dictionary<string, string> { [Secret] = Password });

            return AssemblyHooks.SharedPortal.CreateDevicesFromTemplate(new DeviceTemplatePlacement(_library, Template, subnet, Secret), secrets, AssemblyHooks.CreateTestDirectory());
        }

        private static int ErrorsOf(JsonObject placed, string device)
        {
            return placed["compile"]!.AsArray().Single(entry => entry!["device"]!.GetValue<string>() == device)!["errors"]!.GetValue<int>();
        }

        /// <returns>The partner PLC of each integrated connection of the one HMI in the group.</returns>
        private static List<string> PartnersOf(string group)
        {
            var hmiPath = AssemblyHooks.SharedPortal.GetSoftwarePaths().Single(path => path.StartsWith(group + "/", StringComparison.Ordinal) && path.Contains("/HMI_RT_"));

            return AssemblyHooks.SharedPortal.GetUnifiedTags(hmiPath)["connections"]!.AsArray()
                .Where(connection => connection!["integrated"]!.GetValue<bool>())
                .Select(connection => connection!["partner"]!.GetValue<string>())
                .ToList();
        }

        private static NetworkNodeInfo NodeOf(string device)
        {
            return AssemblyHooks.SharedPortal.GetNetworkTopology().Single(node => node.DevicePath.StartsWith(Group + "/" + device + "/", StringComparison.Ordinal));
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
