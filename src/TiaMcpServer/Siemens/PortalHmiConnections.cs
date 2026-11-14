using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using System;
using System.Linq;
#if TIA_V21
using Siemens.Engineering.HW.CommunicationConnections;
#endif
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The integrated connection between a WinCC Unified HMI and a PLC, which links the HMI's tags to the PLC's.
    ///
    /// Measured 2026-11-11 on TIA Portal V21: <c>CommunicationManagement</c>, a service new in V21's Openness, lists
    /// the connection on the HMI runtime's device item (<c>HMI_RT_1</c>) as an <c>HmiConnection</c>, and
    /// <c>Connections.Create&lt;HmiConnection&gt;(hmiNode, plcItem, plcNode)</c> makes one: in a new project with a
    /// PLC and a PC station on one subnet it made <c>HMI_Connection_1</c>, 192.168.0.2 to 192.168.0.1, which the HMI
    /// then listed as integrated, driver <c>SIMATIC S7 1200/1500</c>, partner the PLC. On V20 there was no way: the
    /// HMI's own connections have <c>Partner</c>, <c>Station</c> and <c>Node</c> read-only, and it was made by hand.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Whether this build can make an integrated HMI connection: only one built for TIA Portal V21.</summary>
        /// <remarks>Asked before a plan is made, so a call that can never run leaves no backup and no audit entry.</remarks>
#if TIA_V21
        public static bool CanCreateHmiConnections => true;
#else
        public static bool CanCreateHmiConnections => false;
#endif

        /// <summary>Makes the integrated connection between a WinCC Unified HMI and a PLC on its subnet.</summary>
        /// <param name="hmiPath">The HMI, for example <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <param name="plcPath">The PLC's software, for example <c>PLC_1</c>.</param>
        /// <param name="backupDirectory">Where the network is recorded first. Required.</param>
        /// <returns>The connection TIA Portal made.</returns>
        /// <exception cref="PortalException">
        /// Not TIA Portal V21, no project, a path that is not a Unified HMI or a PLC, the two not on one subnet, a
        /// connection between them already there, or TIA Portal refused.
        /// </exception>
        public HmiConnectionSummary CreateHmiConnection(string hmiPath, string plcPath, string backupDirectory)
        {
            _logger?.LogInformation("Connecting {HmiPath} to {PlcPath}...", hmiPath, plcPath);

            try
            {
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes the network's connections");
                }

                return CreateIntegratedConnection(hmiPath, plcPath, backupDirectory);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Making the HMI connection failed: {ex.Message}", null, ex);

                pex.Data["hmiPath"] = hmiPath;
                pex.Data["plcPath"] = plcPath;
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "CreateHmiConnection failed for {HmiPath} to {PlcPath}", hmiPath, plcPath);
                throw pex;
            }
        }

#if TIA_V21
        private HmiConnectionSummary CreateIntegratedConnection(string hmiPath, string plcPath, string backupDirectory)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before connecting an HMI");
            }

            var runtime = RequireUnifiedRuntimeItem(hmiPath);
            var plc = RequirePlcItem(plcPath);
            var (hmiNode, plcNode) = RequireSharedSubnet(runtime, plc, hmiPath, plcPath);
            var connections = runtime.GetService<CommunicationManagement>()?.Connections
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{hmiPath}' offers no connections to make");

            RequireNotConnected(connections, plc, hmiPath, plcPath);
            RecordNetworkBefore(backupDirectory);

            var created = (HmiConnection)connections.Create<HmiConnection>(hmiNode, plc, plcNode);

            return new HmiConnectionSummary(created.LocalConnectionName, created.LocalAddress, created.PartnerAddress);
        }

        /// <remarks>The connection hangs from the runtime's own device item, the one holding the HMI software.</remarks>
        private DeviceItem RequireUnifiedRuntimeItem(string hmiPath)
        {
            var container = GetSoftwareContainer(hmiPath);

            return container?.Software is UnifiedHmiSoftware && container.Parent is DeviceItem runtime
                ? runtime
                : throw new PortalException(PortalErrorCode.NotFound, $"'{hmiPath}' is not a WinCC Unified HMI");
        }

        private DeviceItem RequirePlcItem(string plcPath)
        {
            var container = GetSoftwareContainer(plcPath);

            return container?.Software is PlcSoftware && container.Parent is DeviceItem plc
                ? plc
                : throw new PortalException(PortalErrorCode.NotFound, $"'{plcPath}' is not a PLC's software");
        }

        /// <remarks>
        /// The HMI's nodes are looked for in its whole station, as a PC station's interface is on its network card and
        /// not on the runtime; the PLC's in its whole station too, as it may reach the subnet through a CP or CM in its
        /// rack, which is the CPU's sibling and not its child. The first pair on one subnet is
        /// taken, so a PC with a second card on another network, or a CPU with X1 and X2 on two, still finds the one
        /// they share.
        /// </remarks>
        private static (Node HmiNode, Node PlcNode) RequireSharedSubnet(DeviceItem runtime, DeviceItem plc, string hmiPath, string plcPath)
        {
            var station = FindOwningDevice(runtime)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{hmiPath}' is not inside a station");
            var plcStation = FindOwningDevice(plc)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{plcPath}' is not inside a station");
            var plcNodes = EthernetNodes(AllDeviceItems(plcStation.DeviceItems)).Where(node => node.ConnectedSubnet != null).ToList();
            var pair = EthernetNodes(AllDeviceItems(station.DeviceItems))
                .Where(node => node.ConnectedSubnet != null)
                .Select(hmiNode => (HmiNode: hmiNode, PlcNode: plcNodes.FirstOrDefault(plcNode => plcNode.ConnectedSubnet.Name == hmiNode.ConnectedSubnet.Name)))
                .FirstOrDefault(candidate => candidate.PlcNode != null);

            if (pair.PlcNode == null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{hmiPath}' and '{plcPath}' are not on one Ethernet subnet. Put both on the same one first "
                    + "(CreateSubnet, ConnectDeviceToSubnet; GetNetworkTopology shows where each is).");
            }

            return (pair.HmiNode, pair.PlcNode);
        }

        /// <remarks>
        /// By name and station: whether Openness hands out one proxy per item is not promised, so the objects are not
        /// compared. A second connection to the same PLC would be a second HMI_Connection_N doing what the first does.
        /// </remarks>
        private static void RequireNotConnected(ConnectionComposition connections, DeviceItem plc, string hmiPath, string plcPath)
        {
            var plcStation = FindOwningDevice(plc)?.Name;
            var existing = connections.OfType<HmiConnection>().FirstOrDefault(connection =>
                connection.PartnerTarget != null
                && connection.PartnerTarget.Name == plc.Name
                && FindOwningDevice(connection.PartnerTarget)?.Name == plcStation);

            if (existing != null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{hmiPath}' is already connected to '{plcPath}' by '{existing.LocalConnectionName}'. Nothing was changed.");
            }
        }
#else
        /// <remarks>V20's Openness has no way to make one: its connections' partner is read-only (see the class remarks).</remarks>
        private HmiConnectionSummary CreateIntegratedConnection(string hmiPath, string plcPath, string backupDirectory)
        {
            throw new PortalException(
                PortalErrorCode.InvalidState,
                "Making an integrated HMI connection needs TIA Portal V21; on V20 it is made by hand in the Devices & networks view.");
        }
#endif
    }
}
