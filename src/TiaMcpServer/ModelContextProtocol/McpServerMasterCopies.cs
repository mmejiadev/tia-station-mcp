using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Reading master copies. Making them and creating from them is <c>CreateMasterCopy</c> and <c>CreateFromMasterCopy</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetMasterCopies"), Description("Read the master copies of the open project's library, or of a global library file (opened read-only, never changed): every master copy folder by path, and each master copy's name, path, folder, author, creation date and contents. Each content item gives its name, its Openness type and its kind: 'block', 'tagTable' or 'plcType' for what CreateFromMasterCopy can create, null for anything else a master copy can hold (a watch table, an alarm text list, classic HMI screens or hardware). A master copy's path is what CreateFromMasterCopy takes.")]
        public static ResponseMasterCopies GetMasterCopies(
            [Description("globalLibraryPath: a global library file, e.g. 'C:\\Libraries\\Kit\\Kit.al21'; empty for the open project's library")] string globalLibraryPath = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetMasterCopies(globalLibraryPath);

                return new ResponseMasterCopies(content)
                {
                    Message = $"{Count(content, "masterCopies")} master copy(ies) in {Count(content, "folders")} folder(s) of the {(string.IsNullOrWhiteSpace(globalLibraryPath) ? "project library" : $"global library '{globalLibraryPath}'")}{(content["warning"] is JsonNode warning ? ". " + warning : string.Empty)}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read the master copies");
            }
        }
    }
}
