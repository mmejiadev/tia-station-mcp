using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Reading WinCC Unified screens and the item types they are drawn with. Writing is
    /// <c>DrawUnifiedScreen</c>, in <c>McpServerWrites.UnifiedScreens.cs</c>.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedScreen"), Description("Read a WinCC Unified screen: its properties and every item with its type, properties, points and tag links, in exactly the JSON DrawUnifiedScreen takes, so a screen read can be changed and drawn again. Texts are in the project's editing language. The HMI path is the Unified software's, from GetProjectTree or GetDevices, e.g. 'HMI_MTP700/HMI_RT_1'.")]
        public static ResponseUnifiedScreen GetUnifiedScreen(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath,
            [Description("screenName: the screen to read; the refusal lists the screens there are")] string screenName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var screen = Portal.GetUnifiedScreen(hmiPath, screenName);

                return new ResponseUnifiedScreen(screen)
                {
                    Message = $"Screen '{screenName}' on '{hmiPath}': {(screen["items"] as JsonArray)?.Count ?? 0} item(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read screen '{screenName}' on '{hmiPath}'");
            }
        }

        [McpServerTool(Name = "GetUnifiedItemTypes"), Description("List the WinCC Unified screen items DrawUnifiedScreen can draw, or, given one, every property path it takes ('BackColor', 'Font.Size'), its value type, its enum values, and whether it can be set or why not. Read from this TIA Portal's Openness itself, so the list matches the installation. Needs no project.")]
        public static ResponseUnifiedScreen GetUnifiedItemTypes(
            [Description("itemType: optional; an item such as 'rectangle', 'ioField', 'button', 'polyline'. Empty lists the item types")] string itemType = "")
        {
            try
            {
                var content = string.IsNullOrWhiteSpace(itemType)
                    ? new JsonObject { ["types"] = new JsonArray(UnifiedItemTypes.All.Select(type => (JsonNode)JsonValue.Create(UnifiedItemTypes.ShortName(type))!).ToArray()) }
                    : TiaMcpServer.Siemens.Portal.DescribeUnifiedItemType(itemType);

                return new ResponseUnifiedScreen(content)
                {
                    Message = string.IsNullOrWhiteSpace(itemType) ? $"{UnifiedItemTypes.All.Count} drawable item types" : $"Properties of '{itemType}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to describe '{itemType}'");
            }
        }
    }
}
