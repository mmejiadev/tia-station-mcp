using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Reading PLCSIM Advanced: which instances exist, what tags they have and what those hold.
    ///
    /// Reads only. Starting, stopping and writing a tag change the state of a running
    /// simulation, so they live in McpServerWrites behind the guard, and the split between
    /// these two files is what makes that visible.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ListSimulationInstances"), Description("List the PLCSIM Advanced virtual controllers and the runtime's network mode. Call this before downloading: a controller with no address, or a runtime in the wrong network mode, is why a download cannot connect.")]
        public static ResponseSimulationInstances ListSimulationInstances()
        {
            try
            {
                var instances = Simulation.ListInstances();

                return new ResponseSimulationInstances(instances.Select(ToResponse).ToList(), TiaMcpServer.Siemens.SimulationRuntime.NetworkMode)
                {
                    Message = $"{instances.Count} simulation instance(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to list simulation instances");
            }
        }

        [McpServerTool(Name = "ListSimulationTags"), Description("List the tags of the program a virtual controller holds: the names ReadSimulationTags and WriteSimulationTag take. The list is read from the controller and not from the project, so it is empty until something has been downloaded. Filter by name — a CPU has thousands of tags.")]
        public static ResponseSimulationTags ListSimulationTags(
            [Description("instanceName: the virtual controller to ask")] string instanceName,
            [Description("nameFilter: case-insensitive substring the tag name must contain, e.g. 'PieceId'. Omit for all of them.")] string? nameFilter = null,
            [Description("limit: how many tags to return at most")] int limit = TiaMcpServer.Siemens.SimulationRuntime.DefaultTagLimit)
        {
            // No Openness gate: this reads the controller through the PLCSIM API and never touches
            // TIA Portal, so queueing it behind a running compile would cost time and buy nothing.
            try
            {
                var tags = Simulation.ListTags(instanceName, nameFilter, limit);

                return new ResponseSimulationTags(tags.Items.Select(ToResponse).ToList(), tags.MatchCount, tags.TotalCount)
                {
                    Message = DescribeTagList(tags),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to list the tags of '{instanceName}'");
            }
        }

        [McpServerTool(Name = "ReadSimulationTags"), Description("Read tags of a running virtual controller, several in one call. This is how a downloaded program is observed: call ListSimulationTags first to get the exact names. A struct or an array has no value of its own — read its members.")]
        public static ResponseSimulationTagValues ReadSimulationTags(
            [Description("instanceName: the virtual controller to read from")] string instanceName,
            [Description("tagNames: the tag names, spelled exactly as ListSimulationTags reports them. A data block member has no quotes: DB_Cell.Feeder.Step")] string[] tagNames)
        {
            try
            {
                var values = Simulation.ReadTags(instanceName, tagNames);

                return new ResponseSimulationTagValues(values.Select(ToResponse).ToList())
                {
                    Message = $"{values.Count} tag(s) read from '{instanceName}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read tags of '{instanceName}'");
            }
        }

        [McpServerTool(Name = "DescribeSimulationConnection"), Description("Explain the connection a download to PLCSIM would use: the PC interface, its subnets and addresses, whether the connection could be applied, and which devices answered. Call this when a download fails with 'Connect to module failed', which names neither the cause nor the layer it happened in. It establishes the connection in order to report it, so call it after a failed download rather than before one: within a single open project, only the first download to an address succeeds.")]
        public static ResponseMessage DescribeSimulationConnection(
            [Description("softwarePath: full path to the CPU in the project, e.g. 'PLC_0'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var report = Portal.DescribeSimulationConnection(softwarePath);

                return new ResponseMessage
                {
                    Message = report,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to describe the download connection for '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "GetSimulationTargetName"), Description("Report which PC interface a download would go through, without downloading. It is always a PLCSIM interface: this server refuses to download through a real network adapter.")]
        public static ResponseMessage GetSimulationTargetName(
            [Description("softwarePath: full path to the CPU in the project, e.g. 'PLC_0'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = Portal.GetSimulationTargetName(softwarePath);

                return new ResponseMessage
                {
                    Message = $"A download of '{softwarePath}' would go through: {target}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to resolve a download target for '{softwarePath}'");
            }
        }

        private static ResponseSimulationInstance ToResponse(TiaMcpServer.Siemens.SimulationInstanceInfo instance)
        {
            return new ResponseSimulationInstance(instance.Name, instance.OperatingState, instance.CpuType, instance.IpAddresses);
        }

        private static ResponseSimulationTag ToResponse(TiaMcpServer.Siemens.SimulationTagInfo tag)
        {
            return new ResponseSimulationTag(tag.Name, tag.Area, tag.DataType, tag.IsReadable);
        }

        private static ResponseSimulationTagValue ToResponse(TiaMcpServer.Siemens.SimulationTagValue value)
        {
            return new ResponseSimulationTagValue(value.Name, value.DataType, value.Value);
        }

        /// <summary>Says how much of the tag list the caller is looking at.</summary>
        private static string DescribeTagList(TiaMcpServer.Siemens.SimulationTagList tags)
        {
            if (tags.TotalCount == 0)
            {
                return "This controller holds no program, so it has no tags. Download one first.";
            }

            if (tags.IsTruncated)
            {
                return $"{tags.Items.Count} of {tags.MatchCount} matching tag(s), out of {tags.TotalCount}. Narrow the filter or raise the limit.";
            }

            return $"{tags.Items.Count} matching tag(s), out of {tags.TotalCount}";
        }

        private static ResponseSimulationInstance Describe(TiaMcpServer.Siemens.SimulationInstanceInfo instance, string message)
        {
            var response = ToResponse(instance);

            response.Message = message;
            response.Meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = true
            };

            return response;
        }
    }
}
