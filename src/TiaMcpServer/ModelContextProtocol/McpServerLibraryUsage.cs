using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Where the project uses its library types. The writes that tidy the library live in McpServerWrites.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetLibraryTypeUsage"), Description("List every instance of a project library type in the project - PLC blocks and data types, and WinCC Unified faceplates on screens - with its device, its path, the version it shows, the library version it is checked against, and TIA Portal's status for it (up to date or out of date). A type with no instance is not listed. Read this before DeleteLibraryTypeVersion, DeleteLibraryType or CleanUpProjectLibrary, and after a release, to see which instances still show an old version.")]
        public static ResponseLibraryTypes GetLibraryTypeUsage()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var usage = Portal.GetLibraryTypeUsage();
                var count = usage["usages"] is JsonArray rows ? rows.Count : 0;

                return new ResponseLibraryTypes(usage)
                {
                    Message = count == 0 ? "No library type has an instance in the project" : $"{count} instance(s) of library types in the project",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to check where the project library's types are used");
            }
        }
    }
}
