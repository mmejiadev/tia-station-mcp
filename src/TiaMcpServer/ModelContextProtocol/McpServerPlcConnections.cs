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
    /// The configured connections of a PLC. The write that goes with this read, <c>CreatePlcConnection</c>, lives in the
    /// file that changes things, like every write.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetPlcConnections"), Description("List a PLC's configured connections (the Connections table of Devices & networks): S7, TCP, ISO-on-TCP, HMI and any other kind, each with its name, whether TIA Portal reports it valid, both ends' device, address, connection ID and TSAP or port, and which end establishes it. The local ID is the one this PLC's instructions take: PUT, GET, BSEND and BRCV on an S7 connection, TSEND and TRCV on TCP and ISO-on-TCP. Read from the partner, the same connection shows with the ends swapped. Needs TIA Portal V21.")]
        public static ResponsePlcConnections GetPlcConnections(
            [Description("plcPath: the PLC's software, e.g. 'PLC_1'")] string plcPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            if (!TiaMcpServer.Siemens.Portal.CanUsePlcConnections)
            {
                throw new McpException("Reading connections between PLCs needs TIA Portal V21.", McpErrorCode.InvalidParams);
            }

            try
            {
                var connections = Portal.GetPlcConnections(plcPath);

                return new ResponsePlcConnections(connections.Select(DescribePlcConnectionLine).ToList())
                {
                    Message = connections.Count == 0
                        ? $"'{plcPath}' has no configured connection"
                        : $"'{plcPath}' has {connections.Count} configured connection(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the connections of '{plcPath}'");
            }
        }

        private static string DescribePlcConnectionLine(PlcConnectionSummary connection)
        {
            var establishes = connection.Ends.IsLocalActive ? "local establishes" : "partner establishes or none";

            return $"{connection.Name} | {connection.Kind} | {(connection.IsValid ? "valid" : "NOT valid")} | "
                + $"{DescribePlcConnectionEnd(connection.Ends.Local)} | {DescribePlcConnectionEnd(connection.Ends.Partner)} | {establishes}";
        }

        private static string DescribePlcConnectionEnd(PlcConnectionEnd end)
        {
            return $"{end.Device} {end.Address} ID {(end.ConnectionId.Length == 0 ? "-" : end.ConnectionId)} {end.Addressing}".Trim();
        }
    }
}
