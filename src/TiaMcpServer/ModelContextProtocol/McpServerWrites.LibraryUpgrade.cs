using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Upgrading a global library from an older TIA Portal. Part of McpServerWrites: it writes a new
    /// library folder, so it goes through the guard although the original is left as it was.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "UpgradeGlobalLibrary"), Description("Upgrade a global library made with an older TIA Portal (.al20 on V21), which every other library tool refuses, and leave the upgraded copy closed. The original is not changed: TIA Portal writes the copy to a new folder beside the library's own, named after it with '_V21' added, and a '.backup' folder next to it; when that folder already exists the call is refused, as TIA Portal would write a second copy without a word. The answer gives the upgraded library's .alNN file, which GetLibraryTypes, CopyLibraryTypes and PublishLibraryTypes take. A library open in TIA Portal is refused. A write policy names it by the original library's full folder, e.g. 'global-library/C:/Libraries/Faceplates'. The upgrade can take a minute.")]
        public static ResponseMessage UpgradeGlobalLibrary(
            [Description("libraryPath: the library file of an older version, e.g. 'C:/Libraries/Faceplates/Faceplates.al20'")] string libraryPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("UpgradeGlobalLibrary", ChangeTarget.GlobalLibraryFile(libraryPath), libraryPath);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var (upgradedPath, warning) = Portal.UpgradeGlobalLibrary(libraryPath);
                        return DescribeWrittenFile($"'{libraryPath}' upgraded to", "libraryPath", upgradedPath, warning);
                    },
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"upgrading global library '{libraryPath}'");
            }
        }
    }
}
