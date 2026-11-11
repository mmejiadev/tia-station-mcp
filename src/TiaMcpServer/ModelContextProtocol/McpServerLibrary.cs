using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Reading library types. Copying and releasing them is <c>CopyLibraryTypes</c> and <c>ReleaseLibraryType</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetLibraryTypes"), Description("Read the types of the open project's library, or of a global library file (opened read-only, never changed): each type's name, folder, kind and versions, with each version's number, state (released or inWork), whether it is the default, its author and the types it depends on. A WinCC Unified faceplate type is kind 'LibraryType', as Openness V20 has no class of its own for it (a PLC data type is PlcTypeLibraryType, a block CodeBlockLibraryType); each released version of such a type gives its faceplateReference, e.g. 'V0.0.1\\FP_Motor'. Only a released version can be placed with PlaceUnifiedFaceplates. A faceplate type cannot be created or drawn through Openness V20: it is drawn once by hand in TIA Portal and kept in a global library, and CopyLibraryTypes brings it into a project.")]
        public static ResponseLibraryTypes GetLibraryTypes(
            [Description("globalLibraryPath: a global library file, e.g. 'C:\\Libraries\\Faceplates\\Faceplates.al21'; empty for the open project's library")] string globalLibraryPath = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetLibraryTypes(globalLibraryPath);

                return new ResponseLibraryTypes(content)
                {
                    Message = $"{Count(content, "types")} type(s) in the {(string.IsNullOrWhiteSpace(globalLibraryPath) ? "project library" : $"global library '{globalLibraryPath}'")}{(content["warning"] is JsonNode warning ? ". " + warning : string.Empty)}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read the library types");
            }
        }
    }
}
