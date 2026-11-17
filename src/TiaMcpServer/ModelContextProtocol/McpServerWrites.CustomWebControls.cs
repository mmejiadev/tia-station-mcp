using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.CustomWebControls;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Installing a Custom Web Control in the project and placing it on screens. Part of McpServerWrites.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "InstallCustomWebControl"), Description("Install a WinCC Unified Custom Web Control in the open project: a ready-made one by catalogName (GetCustomWebControlCatalog), or one of your own by definition, JSON: {\"name\": \"Plant.Pump\", \"displayName\": \"Pump\", \"description\": \"...\", \"width\": 160, \"height\": 160, \"properties\": [{\"name\": \"Running\", \"type\": \"boolean\", \"default\": false}, {\"name\": \"Color\", \"type\": \"color\", \"default\": \"0, 200, 83\"}], \"events\": [{\"name\": \"Clicked\", \"arguments\": [{\"name\": \"running\", \"type\": \"boolean\"}]}], \"html\": \"<div id='root'>...</div>\", \"css\": \"...\", \"script\": \"function render(p) { ... }\"}. Types: boolean, string, number, integer, color. The script defines render(p), called with the properties on start and on every change, and may define setup(); helpers: TiaStation.color(argb) for CSS, TiaStation.bool(v), TiaStation.number(v), TiaStation.fire('Clicked', ...). The control name is letters and digits with dots; names starting with 'TiaStation.' are the catalogue's. Installing the same name again replaces it, and items already placed of it then fail the compile until \"My controls\" is refreshed in TIA Portal's toolbox. WebCC comes from the local TIA Portal. In the server's own TIA Portal the project must be saved: it is closed and opened again so TIA Portal sees the control. In a TIA Portal with a window, the package is written and the project must be opened again there by hand. A package replaced is copied to the backup registry first; a control another package already declares under the same name is refused. The page may load and reach nothing outside its own package. A write policy names this 'custom-web-controls/<control name>' (e.g. 'custom-web-controls/TiaStation.*' for the catalogue), apart from 'project'; the plan says whether it is the catalogue's or a written definition, with a fingerprint of its content.")]
        public static ResponseCustomWebControls InstallCustomWebControl(
            [Description("catalogName: a ready-made control, e.g. 'Motor'; leave empty to give a definition")] string catalogName = "",
            [Description("definition: your own control as JSON, see the tool description; leave empty to take one from the catalogue")] string definition = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a definition that does not parse is a mistake in the call, not a change to approve.
                var control = ChooseDefinition(catalogName, definition);
                var target = ChangeTarget.CustomWebControl(control.Identity.Name);
                var backupDirectory = Backups.Allocate("InstallCustomWebControl", target);
                var request = new Governance.ChangeRequest("InstallCustomWebControl", target, CustomWebControlFingerprint.Describe(control, string.IsNullOrWhiteSpace(definition))).WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeInstalled(Portal.InstallCustomWebControl(control, backupDirectory)), () => new ResponseCustomWebControls(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, "installing a Custom Web Control");
            }
        }

        [McpServerTool(Name = "PlaceCustomWebControls"), Description("Place installed Custom Web Controls on an existing WinCC Unified screen, from JSON: {\"screen\": \"Main\", \"controls\": [{\"name\": \"Pump_1\", \"control\": \"TiaStation.Motor\", \"left\": 20, \"top\": 40, \"width\": 170, \"height\": 160, \"properties\": {\"Label\": \"P1\", \"RunColor\": \"0, 200, 83\"}, \"tags\": {\"Running\": \"M1.KM\", \"Speed\": \"M1.Speed\"}, \"events\": {\"Clicked\": \"let tag = Tags('M1.Start'); tag.Write(!tag.Read());\"}}]}. control is the control name GetCustomWebControls lists; width and height default to the control's own, and the window frame is always removed. properties gives fixed values, tags binds a property to an HMI tag (a Bool tag for a boolean property, a number for a number), events gives each event a script. A property is given a value or a tag, not both. Every tag is checked to exist first (its root: a member such as M1.KM is checked by the compile). Every name is checked against the control's manifest first; the controls are only added, all of them or none, and the screen's items before are listed in the backup registry. Compile the HMI afterwards.")]
        public static ResponseCustomWebControls PlaceCustomWebControls(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("controls: the screen and the controls as JSON, see the tool description")] string controls)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = CustomWebControlPlacementReader.Read(controls);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("PlaceCustomWebControls", target);
                var request = new Governance.ChangeRequest("PlaceCustomWebControls", target, $"{list.Placements.Count} control(s) on {list.Screen}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribePlacedControls(Portal.PlaceCustomWebControls(hmiPath, list, backupDirectory), hmiPath), () => new ResponseCustomWebControls(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"placing Custom Web Controls on '{hmiPath}'");
            }
        }

        private static CustomWebControlDefinition ChooseDefinition(string catalogName, string definition)
        {
            var fromCatalogue = !string.IsNullOrWhiteSpace(catalogName);
            var fromDefinition = !string.IsNullOrWhiteSpace(definition);

            if (fromCatalogue == fromDefinition)
            {
                throw new McpException("Give either catalogName (a ready-made control) or definition (your own), not both and not neither.", McpErrorCode.InvalidParams);
            }

            return fromCatalogue ? CustomWebControlCatalog.Get(catalogName) : CustomWebControlCatalog.RequireWritten(CustomWebControlReader.Read(definition));
        }

        private static ResponseCustomWebControls DescribeInstalled(JsonObject installed)
        {
            return new ResponseCustomWebControls(installed)
            {
                Message = $"'{installed["installed"]}' is installed. {installed["note"]}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["reopened"] = installed["reopened"]?.DeepClone()
                }
            };
        }

        private static ResponseCustomWebControls DescribePlacedControls(JsonObject placed, string hmiPath)
        {
            var count = Count(placed, "placed");

            return new ResponseCustomWebControls(placed)
            {
                Message = $"{count} Custom Web Control(s) placed on '{hmiPath}'. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["controls"] = count
                }
            };
        }
    }
}
