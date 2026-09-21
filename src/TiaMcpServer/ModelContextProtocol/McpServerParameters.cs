using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// What a device is set to, as opposed to what it is made of. The write that goes with this
    /// read, <c>SetDeviceParameter</c>, lives in the file that changes things, like every write.
    /// </remarks>
    public static partial class McpServer
    {
        /// <remarks>
        /// Values are printed invariant. This list is what a caller copies into SetDeviceParameter,
        /// and a half printed as 0,5 on a Spanish machine would have gone back in as five.
        /// </remarks>
        [McpServerTool(Name = "GetDeviceParameters"), Description("List the parameters of a device item that can actually be changed - cycle, start-up behaviour, protection level and whatever else it exposes - with the value each holds. This is the subset GetDeviceItemInfo prints that SetDeviceParameter can aim at; most of what a device reports describes it rather than configures it. The names here are the exact spellings SetDeviceParameter takes.")]
        public static ResponseNetworkTopology GetDeviceParameters(
            [Description("deviceItemPath: the device item, e.g. 'PLC_0'")] string deviceItemPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var parameters = Portal.GetDeviceParameters(deviceItemPath);

                var lines = parameters
                    .Select(parameter => $"{parameter.Name} | {ParameterValueFormatter.Format(parameter.Value)} | {parameter.AccessMode}")
                    .ToList();

                return new ResponseNetworkTopology(lines)
                {
                    Message = parameters.Count == 0
                        ? $"'{deviceItemPath}' has no parameter that can be changed"
                        : $"{parameters.Count} parameter(s) of '{deviceItemPath}' can be changed",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the parameters of '{deviceItemPath}'");
            }
        }
    }
}
