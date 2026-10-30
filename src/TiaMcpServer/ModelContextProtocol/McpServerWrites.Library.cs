using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Copying and releasing library types. Reading them is <c>GetLibraryTypes</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CopyLibraryTypes"), Description("Copy types from a global library file into the open project's library, e.g. the faceplates a class keeps in a shared library; the types each one depends on (a faceplate's PLC data type) come with it. The global library is opened read-only and never changed. A type the project library already holds, asked for or only depended on, is refused rather than merged, as TIA Portal V20 would renumber its version in work without a word; a name the global library lacks is refused too. Nothing in the project changes but the library gaining these types, and its types and versions before are kept in the backup registry. Answers with every type the project library gained. Then ReleaseLibraryType for a version still in work, and PlaceUnifiedFaceplates to use a faceplate.")]
        public static ResponseLibraryTypes CopyLibraryTypes(
            [Description("globalLibraryPath: the global library file, e.g. 'C:\\Libraries\\Faceplates\\Faceplates.al20'")] string globalLibraryPath,
            [Description("typeNames: the types to copy, exactly as GetLibraryTypes names them")] string[] typeNames)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var names = (typeNames ?? Array.Empty<string>()).Select(name => (name ?? string.Empty).Trim()).ToList();
                // The backup lists the project library's types and versions before the copy.
                var backupDirectory = Backups.Allocate("CopyLibraryTypes", ChangeTarget.ProjectLibrary);
                var request = new Governance.ChangeRequest("CopyLibraryTypes", ChangeTarget.ProjectLibrary, $"from {globalLibraryPath}: {string.Join(", ", names)}").WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeCopiedTypes(Portal.CopyLibraryTypes(globalLibraryPath, names, backupDirectory)),
                    () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"copying library types from '{globalLibraryPath}'");
            }
        }

        [McpServerTool(Name = "ReleaseLibraryType"), Description("Release a project library type's version in work, e.g. a faceplate just drawn or edited by hand in TIA Portal; only a released version can be placed on a screen. The type must have exactly one version in work, and every type it depends on must be released already, so the release changes nothing else. The version number defaults to the one the version in work has; one given must be higher than every released version, as TIA Portal V20 would release a repeated number and keep two versions under it. A release cannot be undone; the type's versions before it are kept in the backup registry. Instances already placed keep the version they show.")]
        public static ResponseLibraryTypes ReleaseLibraryType(
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: the number to release under, e.g. '0.0.2'; empty to keep the version in work's own")] string version = "",
            [Description("comment: the release comment, e.g. what changed")] string comment = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a number that does not parse is a mistake in the call, not a change to approve.
                var number = string.IsNullOrWhiteSpace(version) ? null : LibraryVersionNumber.Parse(version, "version");
                // Trimmed as CopyLibraryTypes trims, so the policy is asked about the type's own name.
                var name = (typeName ?? string.Empty).Trim();
                // The backup holds the type's versions before the release, which Openness cannot undo.
                var target = ChangeTarget.LibraryType(name);
                var backupDirectory = Backups.Allocate("ReleaseLibraryType", target);
                var request = new Governance.ChangeRequest("ReleaseLibraryType", target, number == null ? "the version in work, under its own number" : LibraryVersionNumber.Format(number)).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeReleasedType(Portal.ReleaseLibraryType(name, number, comment, backupDirectory)),
                    () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"releasing library type '{typeName}'");
            }
        }

        private static ResponseLibraryTypes DescribeCopiedTypes(JsonObject copied)
        {
            var count = Count(copied, "copied");

            return new ResponseLibraryTypes(copied)
            {
                Message = $"{count} type(s) copied into the project library. Save the project to keep them.{(copied["warning"] is JsonNode warning ? " " + warning : string.Empty)}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["copied"] = count
                }
            };
        }

        private static ResponseLibraryTypes DescribeReleasedType(JsonObject type)
        {
            return new ResponseLibraryTypes(type)
            {
                Message = $"'{type["name"]}' released. Save the project to keep it.",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }
    }
}
