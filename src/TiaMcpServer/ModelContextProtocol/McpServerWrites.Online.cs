using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Searching a network for devices, and going offline with a PLC. Part of McpServerWrites: a search changes
    /// nothing in the project and still reaches every device on a network, so it is planned, decided by the policy
    /// and audited like a write. There is no GoOnline: measured 2026-11-14 on TIA Portal V21, OnlineProvider.GoOnline
    /// through the PLCSIM Virtual Ethernet Adapter to a downloaded CPU 1511 crashed TIA Portal, twice, with no
    /// confirmation or notification raised first (docs/STATUS.md).
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ScanAccessibleDevices"), Description("Search which devices answer through one PC network interface, as TIA Portal's 'Accessible devices' does: each device's name, IP address, MAC address and family. It sends a discovery request to the network behind the interface and changes nothing on any device, and it still reaches other people's machines, so the interface must be listed in the policy as network/<interface name>, e.g. 'network/RZ616 Wi-Fi 6E 160MHz'. Take the interface's name from GetPcInterfaces. While a PLCSIM Advanced instance exists TIA Portal offers only PLCSIM, which finds the simulated controllers.")]
        public static ResponseMessage ScanAccessibleDevices(
            [Description("plcPath: a PLC of the open project whose connection settings offer the interface, e.g. 'PLC_1'")] string plcPath,
            [Description("pcInterfaceName: the interface, exactly as GetPcInterfaces names it")] string pcInterfaceName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("ScanAccessibleDevices", ChangeTarget.NetworkInterface(pcInterfaceName), "search for accessible devices; changes nothing on them");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeAccessibleDevices(Portal.ScanAccessibleDevices(plcPath, pcInterfaceName), pcInterfaceName),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"searching accessible devices through '{pcInterfaceName}'");
            }
        }

        [McpServerTool(Name = "GoOffline"), Description("Go offline with a PLC, as TIA Portal's Go offline button does. Exports and imports of that PLC work again afterwards.")]
        public static ResponseMessage GoOffline(
            [Description("plcPath: the PLC's software, e.g. 'PLC_1'")] string plcPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("GoOffline", ChangeTarget.Program(plcPath), "offline");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeOnlineState(plcPath, Portal.GoOffline(plcPath)),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"going offline with '{plcPath}'");
            }
        }

        private static ResponseMessage DescribeAccessibleDevices(System.Collections.Generic.IReadOnlyList<AccessibleDeviceInfo> devices, string pcInterfaceName)
        {
            var each = devices.Select(device => $"'{device.Name}' at {device.Address} (MAC {device.MacAddress}, {device.DeviceSeries})");

            return new ResponseMessage
            {
                Message = devices.Count == 0
                    ? $"No device answered through '{pcInterfaceName}'"
                    : $"{devices.Count} device(s) answered through '{pcInterfaceName}': {string.Join(", ", each)}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["devices"] = new JsonArray(devices.Select(device => (JsonNode)new JsonObject
                    {
                        ["name"] = device.Name,
                        ["address"] = device.Address,
                        ["macAddress"] = device.MacAddress,
                        ["deviceSeries"] = device.DeviceSeries
                    }).ToArray())
                }
            };
        }

        private static ResponseMessage DescribeOnlineState(string plcPath, string state)
        {
            return new ResponseMessage
            {
                Message = $"'{plcPath}' is {state}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["state"] = state
                }
            };
        }
    }
}
