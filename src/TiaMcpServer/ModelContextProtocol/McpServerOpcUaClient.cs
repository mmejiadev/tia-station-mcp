using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Governance;
using TiaMcpServer.OpcUa;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Reading a running controller over OPC UA. Phase 9.
    ///
    /// These tools read and nothing else, which is why they live here and not in McpServerWrites.
    /// They take no Openness gate either: nothing here touches TIA Portal, and queueing a network
    /// read behind a compile would cost something and protect nothing.
    ///
    /// They are still governed. The endpoint must be listed in the policy, because contacting a
    /// machine is not something a session should be able to do to an address nobody wrote down.
    /// A refusal comes back as a response with the reason, the same shape a refused write takes.
    /// </remarks>
    public static partial class McpServer
    {
        private const string RefusedOutcome = "Refused";

        /// <summary>Reads from OPC UA servers.</summary>
        /// <remarks>
        /// Shared, because it holds the client's certificate and configuration, which are built
        /// once. A per-call reader would rebuild them on every read.
        /// </remarks>
        public static IOpcUaReader OpcUaReader =>
            _services != null
                ? _services.GetRequiredService<IOpcUaReader>()
                : Fallback.OpcUaReader;

        /// <summary>Decides which OPC UA servers this session may contact.</summary>
        public static OpcUaAccessPolicy OpcUaAccess =>
            _services != null
                ? _services.GetRequiredService<OpcUaAccessPolicy>()
                : Fallback.OpcUaAccess;

        [McpServerTool(Name = "BrowseOpcUaServer"), Description("List the objects and variables directly under a node of a running controller's OPC UA server. Start with no nodeId to see the top level, then browse into what you find: node ids are copied from here into ReadOpcUaValues, exactly, quotes included. Reads only. The endpoint must be listed in the policy as opcua/<host>:<port>, and the server must offer a secured endpoint. The first connection records the server's certificate; a different one later is refused until a person removes the old record.")]
        public static async Task<ResponseOpcUaNodes> BrowseOpcUaServer(
            [Description("endpoint: the server's URL, e.g. 'opc.tcp://192.168.0.1:4840'")] string endpoint,
            [Description("nodeId: the node to list, e.g. 'ns=3;s=\"DB_Cell\"'; empty for the Objects folder")] string nodeId = "",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var server = OpcUaEndpoint.Parse(endpoint);
                var decision = OpcUaAccess.Decide(server);

                if (!decision.IsAllowed)
                {
                    return Refused(new ResponseOpcUaNodes(Array.Empty<string>()), server, decision);
                }

                var nodes = await OpcUaReader.BrowseAsync(server, nodeId, cancellationToken).ConfigureAwait(false);

                return new ResponseOpcUaNodes(nodes.Select(node => $"{node.NodeId} | {node.DisplayName} | {node.NodeClass}").ToList())
                {
                    Message = $"{nodes.Count} node(s) under {(string.IsNullOrWhiteSpace(nodeId) ? "Objects" : nodeId)} on {server}",
                    Meta = Succeeded()
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to browse the OPC UA server");
            }
        }

        [McpServerTool(Name = "ReadOpcUaValues"), Description("Read the current value of variables on a running controller's OPC UA server. Reads only: nothing on the machine changes. Take node ids from BrowseOpcUaServer. A node the server does not know comes back with a bad status and no value, and the others are still read. Numbers are written with a decimal point whatever the machine's language. The endpoint must be listed in the policy as opcua/<host>:<port>.")]
        public static async Task<ResponseOpcUaReadings> ReadOpcUaValues(
            [Description("endpoint: the server's URL, e.g. 'opc.tcp://192.168.0.1:4840'")] string endpoint,
            [Description("nodeIds: the variables to read, e.g. ['ns=3;s=\"DB_Cell\".\"Running\"']")] string[] nodeIds,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var server = OpcUaEndpoint.Parse(endpoint);
                var decision = OpcUaAccess.Decide(server);

                if (!decision.IsAllowed)
                {
                    return Refused(new ResponseOpcUaReadings(Array.Empty<string>()), server, decision);
                }

                var readings = await OpcUaReader.ReadAsync(server, nodeIds ?? Array.Empty<string>(), cancellationToken).ConfigureAwait(false);

                return new ResponseOpcUaReadings(readings.Select(DescribeReading).ToList())
                {
                    Message = DescribeReadings(readings, server),
                    Meta = Succeeded()
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read from the OPC UA server");
            }
        }

        private static TResponse Refused<TResponse>(TResponse response, OpcUaEndpoint server, PolicyDecision decision)
            where TResponse : ResponseMessage
        {
            response.Message =
                $"Not contacting {server}: {decision.Reason}. To allow it, list '{server.PolicyTarget}' in the policy for this mode.";
            response.Meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = false,
                [GuardedTool.OutcomeKey] = RefusedOutcome
            };

            return response;
        }

        private static JsonObject Succeeded()
        {
            return new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = true
            };
        }

        private static string DescribeReading(OpcUaReading reading)
        {
            return $"{reading.NodeId} | {reading.Value} | {reading.DataType} | {reading.Status} | {reading.SourceTimestamp}";
        }

        private static string DescribeReadings(IReadOnlyList<OpcUaReading> readings, OpcUaEndpoint server)
        {
            var bad = readings.Count(reading => !reading.IsGood);

            return bad == 0
                ? $"{readings.Count} value(s) read from {server}"
                : $"{readings.Count} value(s) asked of {server}, {bad} with a bad status: read the status column";
        }
    }
}
