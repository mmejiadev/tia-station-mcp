using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>WinCC Unified faceplate instances: reading them and placing new ones.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedFaceplates"), Description("Read every faceplate instance on a WinCC Unified HMI's screens, screen groups included: its screen, name, library type and version, frame, and each interface property's value as text (a tag's name, or a fixed text as plain text). Openness does not say which a value is, so it is not read back as {\"tag\": ...}: to place it again with PlaceUnifiedFaceplates, write tags as {\"tag\": \"M1\"} and give the screen once for the list. The HMI path is the Unified software's, e.g. 'PC-System_1/HMI_RT_1'.")]
        public static ResponseUnifiedFaceplates GetUnifiedFaceplates(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetUnifiedFaceplates(hmiPath);

                return new ResponseUnifiedFaceplates(content)
                {
                    Message = $"'{hmiPath}': {Count(content, "faceplates")} faceplate(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the faceplates of '{hmiPath}'");
            }
        }

        [McpServerTool(Name = "PlaceUnifiedFaceplates"), Description("Place faceplate instances on an existing WinCC Unified screen, from JSON: {\"screen\": \"Principal\", \"faceplates\": [{\"name\": \"FP_M1\", \"type\": \"FP_Motor\", \"version\": \"0.0.1\", \"left\": 20, \"top\": 40, \"width\": 250, \"height\": 250, \"interface\": {\"Motor\": {\"tag\": \"M1\"}, \"Nom\": \"Motor 1\"}}]}. The type is a project library type with a released version (GetLibraryTypes; CopyLibraryTypes brings one from a global library, ReleaseLibraryType releases one); version defaults to the newest released, and width and height to the type's own. Each interface property is given an HMI tag as {\"tag\": \"M1\"} (a member as \"M1.Ft\"), or a fixed text as a string. Every tag is checked to exist first, as TIA Portal takes any name and only the compile refuses it; whether a tag's type suits the property is not, as Openness does not say a property's type, so compile the HMI afterwards. An interface property the type lacks is refused, naming the ones it has. Nothing on the screen changes: the faceplates are only added, all of them or none, and the screen's items before are listed in the backup registry. A screen holding a faceplate can no longer be redrawn by DrawUnifiedScreen, which keeps only what it can draw back; draw the screen first, place the faceplates last.")]
        public static ResponseUnifiedFaceplates PlaceUnifiedFaceplates(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("faceplates: the screen and the faceplates as JSON, see the tool description")] string faceplates)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a list that does not parse is a mistake in the call, not a
                // change anyone needs to approve or audit.
                var list = UnifiedFaceplateListReader.Parse(faceplates);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("PlaceUnifiedFaceplates", target);
                var request = new Governance.ChangeRequest("PlaceUnifiedFaceplates", target, UnifiedFaceplateSummary.Of(list)).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribePlacedFaceplates(Portal.PlaceUnifiedFaceplates(hmiPath, list, backupDirectory), hmiPath),
                    () => new ResponseUnifiedFaceplates(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"placing Unified faceplates on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedFaceplates DescribePlacedFaceplates(JsonObject placed, string hmiPath)
        {
            var count = Count(placed, "faceplates");

            return new ResponseUnifiedFaceplates(placed)
            {
                Message = $"{count} faceplate(s) placed on '{hmiPath}'. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["faceplates"] = count
                }
            };
        }
    }
}
