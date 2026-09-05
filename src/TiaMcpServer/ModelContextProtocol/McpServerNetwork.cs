using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The network as configured: PROFINET topology and the OPC UA interfaces a CPU publishes.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetNetworkTopology"), Description("List every device interface in the project with its address and subnet. Read this before writing code that addresses remote IO, or before configuring a PROFINET or PROFIBUS network: an interface with no subnet is wired to nothing.")]
        public static ResponseNetworkTopology GetNetworkTopology()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var nodes = Portal.GetNetworkTopology();

                var lines = nodes
                    .Select(node => $"{node.DevicePath} | {node.InterfaceName} | {node.NetworkType} | {node.Address} | {(node.IsConnected ? node.SubnetName : "<not connected>")}")
                    .ToList();

                var unconnected = nodes.Count(node => !node.IsConnected);

                return new ResponseNetworkTopology(lines)
                {
                    Message = unconnected == 0
                        ? $"{nodes.Count} interface(s), all connected to a subnet"
                        : $"{nodes.Count} interface(s), {unconnected} not connected to any subnet",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read the network topology");
            }
        }

        [McpServerTool(Name = "GetOpcUaInterfaces"), Description("List the OPC UA server interfaces a CPU publishes, with whether each is enabled. An empty list means the CPU publishes nothing over OPC UA, which is the usual reason a client cannot connect.")]
        public static ResponseNetworkTopology GetOpcUaInterfaces(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var interfaces = Portal.GetOpcUaInterfaces(softwarePath);

                var lines = interfaces
                    .Select(item => $"{item.Name} | {(item.IsEnabled ? "enabled" : "disabled")} | {item.Author} | {item.LastModified:yyyy-MM-dd}")
                    .ToList();

                return new ResponseNetworkTopology(lines)
                {
                    Message = interfaces.Count == 0
                        ? $"'{softwarePath}' publishes no OPC UA server interface"
                        : $"{interfaces.Count} OPC UA interface(s), {interfaces.Count(item => item.IsEnabled)} enabled",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to list the OPC UA interfaces of '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "ExportOpcUaInterface"), Description("Export one OPC UA server interface to a file. The interface is the contract between the PLC and every OPC UA client, so it belongs under version control alongside the code.")]
        public static ResponseMessage ExportOpcUaInterface(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("interfaceName: the server interface to export; call GetOpcUaInterfaces to see the names")] string interfaceName,
            [Description("exportPath: file to write")] string exportPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var written = Portal.ExportOpcUaInterface(softwarePath, interfaceName, exportPath);

                return new ResponseMessage
                {
                    Message = $"OPC UA interface '{interfaceName}' exported to '{written}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to export the OPC UA interface '{interfaceName}'");
            }
        }
    }
}
