using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.CustomWebControls;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// WinCC Unified Custom Web Controls: the catalogue this server carries, and those installed in the project. Installing
    /// and placing live in McpServerWrites.CustomWebControls.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetCustomWebControlCatalog"), Description("List the ready-made WinCC Unified Custom Web Controls this server carries - animated controls drawn as web pages: PilotLight, Motor, Tank, Valve, Gauge, Conveyor, PushButton - with each one's control name, what it does, its default size, its properties (name, kind, default) and its events. Install one with InstallCustomWebControl (catalogName), then place it with PlaceCustomWebControls. Needs no TIA Portal.")]
        public static ResponseCustomWebControls GetCustomWebControlCatalog()
        {
            var controls = CustomWebControlCatalog.Names().Select(name => (JsonNode)DescribeDefinition(name, CustomWebControlCatalog.Get(name))).ToArray();

            return new ResponseCustomWebControls(new JsonObject { ["controls"] = new JsonArray(controls) })
            {
                Message = $"{controls.Length} control(s) in the catalogue",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }

        [McpServerTool(Name = "GetCustomWebControls"), Description("List the Custom Web Controls installed in the open project (its UserFiles\\CustomControls folder): each one's control name - what PlaceCustomWebControls takes - display name, package file, properties with their kinds, events, and default size. A package that cannot be read is listed apart with why.")]
        public static ResponseCustomWebControls GetCustomWebControls()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetCustomWebControls();

                return new ResponseCustomWebControls(content)
                {
                    Message = $"{Count(content, "controls")} Custom Web Control(s) installed in the project",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read the project's Custom Web Controls");
            }
        }

        private static JsonObject DescribeDefinition(string catalogName, CustomWebControlDefinition definition)
        {
            return new JsonObject
            {
                ["catalogName"] = catalogName,
                ["control"] = definition.Identity.Name,
                ["displayName"] = definition.Identity.DisplayName,
                ["description"] = definition.Identity.Description,
                ["width"] = definition.Content.Width,
                ["height"] = definition.Content.Height,
                ["properties"] = new JsonArray(definition.Properties.Select(property => (JsonNode)$"{property.Name}: {property.Kind} = {property.Default.GetRawText()}").ToArray()),
                ["events"] = new JsonArray(definition.Events.Select(fired => (JsonNode)$"{fired.Name}({string.Join(", ", fired.Arguments.Select(argument => argument.Name))})").ToArray())
            };
        }
    }
}
