using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// A node's subnet mask and router, next to SetDeviceAddress. Part of McpServerWrites.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "SetIpSettings"), Description("Set the subnet mask of one Ethernet node and the router it uses, the two IP settings besides the address (SetDeviceAddress). Take devicePath and nodeName from GetNetworkTopology. The mask must be ones followed by zeros (255.255.255.0); the router must be inside the node's subnet and not the node itself, or empty for no router - TIA Portal would store a router outside the subnet without complaint, and the PLC would never reach it. The current layout is recorded to the backup registry first, and what the node holds afterwards is read back.")]
        public static ResponseMessage SetIpSettings(
            [Description("devicePath: path of the device item owning the interface, as GetNetworkTopology prints it")] string devicePath,
            [Description("nodeName: the node on that interface, as GetNetworkTopology prints it")] string nodeName,
            [Description("subnetMask: e.g. 255.255.255.0")] string subnetMask,
            [Description("routerAddress: the router, e.g. 192.168.0.254; empty for no router")] string? routerAddress = null)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                // A bad mask is refused before a plan exists; the router is checked against the node's address later.
                IpSettingsRule.RequireSubnetMask(subnetMask);

                var wanted = new NodeIpSettings(subnetMask, routerAddress ?? string.Empty);
                var target = ChangeTarget.Program(devicePath);
                var backupDirectory = Backups.Allocate("SetIpSettings", target);
                var request = new Governance.ChangeRequest("SetIpSettings", target, $"mask {subnetMask}, router {(wanted.UsesRouter ? routerAddress : "none")}")
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeIpSettings(Portal.SetNodeIpSettings(devicePath, nodeName, wanted, backupDirectory), devicePath, nodeName),
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to set the mask and router of '{nodeName}' on '{devicePath}'");
            }
        }

        private static ResponseMessage DescribeIpSettings(NodeIpSettings applied, string devicePath, string nodeName)
        {
            return new ResponseMessage
            {
                Message = $"'{nodeName}' on '{devicePath}' now has mask {applied.SubnetMask} and "
                    + (applied.UsesRouter ? $"router {applied.RouterAddress}" : "no router"),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["subnetMask"] = applied.SubnetMask,
                    ["routerAddress"] = applied.RouterAddress
                }
            };
        }
    }
}
