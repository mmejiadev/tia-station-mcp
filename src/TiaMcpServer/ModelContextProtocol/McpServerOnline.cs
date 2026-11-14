using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Reading how a CPU can be reached: the PC interfaces TIA Portal offers and whether it is online. Reads only,
    /// and neither contacts a network. Searching a network and going online are in McpServerWrites.Online, behind
    /// the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetPcInterfaces"), Description("List the PC network interfaces TIA Portal offers for going online with a PLC (Ethernet, Wi-Fi, VMware adapters, PLCSIM), each with its connection mode. Reads TIA Portal's settings only: no network is contacted. Measured on TIA Portal V21: while any PLCSIM Advanced instance exists, even in STOP, TIA Portal offers only 'PLCSIM' — delete the instances (DeleteSimulationInstance) to see the real adapters; the answer says how many exist. Pass an interface's name to ScanAccessibleDevices.")]
        public static ResponseMessage GetPcInterfaces(
            [Description("plcPath: the PLC's software, e.g. 'PLC_1'")] string plcPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var interfaces = Portal.GetPcInterfaces(plcPath);
                // A machine without PLCSIM Advanced still has adapters to list: the count is left out there, not the answer.
                int? instances = SimulationRuntime.IsAvailable ? Simulation.ListInstances().Count : null;
                var note = instances.GetValueOrDefault() == 0
                    ? string.Empty
                    : $" {instances} PLCSIM Advanced instance(s) exist, so TIA Portal offers only PLCSIM; delete them to see the real adapters.";

                return new ResponseMessage
                {
                    Message = $"{interfaces.Count} PC interface(s) offered for '{plcPath}': "
                        + string.Join(", ", interfaces.Select(pcInterface => $"'{pcInterface.Name}' ({pcInterface.Mode}{(pcInterface.IsSimulation ? ", PLCSIM" : string.Empty)})"))
                        + "." + note,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["simulationInstances"] = instances,
                        ["interfaces"] = new JsonArray(interfaces.Select(pcInterface => (JsonNode)new JsonObject
                        {
                            ["mode"] = pcInterface.Mode,
                            ["name"] = pcInterface.Name,
                            ["number"] = pcInterface.Number,
                            ["isSimulation"] = pcInterface.IsSimulation
                        }).ToArray())
                    }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the PC interfaces of '{plcPath}'");
            }
        }

        [McpServerTool(Name = "GetOnlineState"), Description("Report whether TIA Portal is online with a PLC: 'Offline', 'Online', or a state in between. Reads TIA Portal's state only. While online, TIA Portal refuses exports and imports of that PLC; GoOffline ends it.")]
        public static ResponseMessage GetOnlineState(
            [Description("plcPath: the PLC's software, e.g. 'PLC_1'")] string plcPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var state = Portal.GetOnlineState(plcPath);

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
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the online state of '{plcPath}'");
            }
        }
    }
}
