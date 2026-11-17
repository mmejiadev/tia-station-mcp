using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Keeping the project library tidy: discard, default version, delete a version or a type, clean up. Part of
    /// McpServerWrites.
    /// </remarks>
    public static partial class McpServer
    {
        private const string SavedProjectNote = "A write policy names this 'library-delete/<type>', apart from 'library/<type>', which edits and releases. The project must be saved first (SaveProject): it is archived to the backup registry before anything is touched, as a faceplate's content has no other copy, and TIA Portal archives only a saved project.";

        [McpServerTool(Name = "DiscardLibraryTypeEdit"), Description("Throw away a project library type's version in work - the one EditLibraryType started or a hand edit left open - leaving its released versions as they were. Whatever was changed in that version is lost; a type whose only version is the one in work (a faceplate never released) is removed with it, and the answer says so. " + SavedProjectNote)]
        public static ResponseLibraryTypes DiscardLibraryTypeEdit(
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName)
        {
            var name = (typeName ?? string.Empty).Trim();

            return RunLibraryTypeChange(("DiscardLibraryTypeEdit", ChangeTarget.LibraryTypeDeletion(name), "discard the version in work", $"The version in work of '{name}' was discarded (if it was the only one, the type went with it; see the answer). Save the project to keep it."), name, backup => Portal.DiscardLibraryTypeEdit(name, backup));
        }

        [McpServerTool(Name = "SetDefaultLibraryTypeVersion"), Description("Make a released version of a project library type its default: the version a new instance takes when the type is dragged onto a screen or into a program. Existing instances keep the version they show (UpdateUnifiedFaceplates moves faceplates). Undone by choosing the previous default again; the type is described in the backup registry first.")]
        public static ResponseLibraryTypes SetDefaultLibraryTypeVersion(
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: a released version, e.g. 0.0.3")] string version)
        {
            var name = (typeName ?? string.Empty).Trim();
            var number = ParseLibraryVersion(version);

            return RunLibraryTypeChange(("SetDefaultLibraryTypeVersion", ChangeTarget.LibraryType(name), $"default {LibraryVersionNumber.Format(number)}", $"{LibraryVersionNumber.Format(number)} is now the default of '{name}'. Save the project to keep it."), name, backup => Portal.SetDefaultLibraryTypeVersion(name, number, backup));
        }

        [McpServerTool(Name = "DeleteLibraryTypeVersion"), Description("Delete a released version of a project library type. Refused for the default version (SetDefaultLibraryTypeVersion first), a version in work (DiscardLibraryTypeEdit) and a type's only version (DeleteLibraryType); TIA Portal itself refuses a version other types depend on. GetLibraryTypeUsage shows which versions instances use. " + SavedProjectNote)]
        public static ResponseLibraryTypes DeleteLibraryTypeVersion(
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: the version to delete, e.g. 0.0.1")] string version)
        {
            var name = (typeName ?? string.Empty).Trim();
            var number = ParseLibraryVersion(version);

            return RunLibraryTypeChange(("DeleteLibraryTypeVersion", ChangeTarget.LibraryTypeDeletion(name), $"delete {LibraryVersionNumber.Format(number)}", $"Version {LibraryVersionNumber.Format(number)} of '{name}' was deleted. Save the project to keep it."), name, backup => Portal.DeleteLibraryTypeVersion(name, number, backup));
        }

        [McpServerTool(Name = "DeleteLibraryType"), Description("Delete a project library type with all its versions. A type with a version in work is refused (DiscardLibraryTypeEdit first); TIA Portal refuses a type that still has instances in the project (GetLibraryTypeUsage lists them) or that other types depend on. " + SavedProjectNote)]
        public static ResponseLibraryTypes DeleteLibraryType(
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName)
        {
            var name = (typeName ?? string.Empty).Trim();

            return RunLibraryTypeChange(("DeleteLibraryType", ChangeTarget.LibraryTypeDeletion(name), "delete the type", $"Type '{name}' was deleted from the project library. Save the project to keep it."), name, backup => Portal.DeleteLibraryType(name, backup));
        }

        [McpServerTool(Name = "CleanUpProjectLibrary"), Description("Delete every project library version nothing uses - no instance in the project and no other type depending on it - keeping each type's default version and any version in work; with deleteUnusedTypes, types nothing uses at all go too. Answers with every version removed. A write policy names this 'library-cleanup', and 'library-cleanup/types' with deleteUnusedTypes, apart from 'library', which only lets types be added. " + SavedProjectNote)]
        public static ResponseLibraryTypes CleanUpProjectLibrary(
            [Description("deleteUnusedTypes: true to delete types nothing uses as well; false (the default) keeps one version of each")] bool deleteUnusedTypes = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var target = deleteUnusedTypes ? ChangeTarget.ProjectLibraryCleanUpTypes : ChangeTarget.ProjectLibraryCleanUp;
                var backupDirectory = Backups.Allocate("CleanUpProjectLibrary", target);
                var request = new Governance.ChangeRequest("CleanUpProjectLibrary", target, deleteUnusedTypes ? "unused versions and types" : "unused versions").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeCleanUp(Portal.CleanUpProjectLibrary(deleteUnusedTypes, backupDirectory)), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, "cleaning up the project library");
            }
        }

        /// <param name="change">The tool's name, the target the policy decides on, what the plan says it does, and the answer when it is done.</param>
        /// <param name="typeName">The type, named in a failure.</param>
        /// <param name="apply">The portal call, given the backup folder.</param>
        private static ResponseLibraryTypes RunLibraryTypeChange((string Tool, string Target, string What, string Message) change, string typeName, Func<string, JsonObject> apply)
        {
            var (tool, target, what, message) = change;

            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var backupDirectory = Backups.Allocate(tool, target);
                var request = new Governance.ChangeRequest(tool, target, what).WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(apply(backupDirectory), message), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"{what} of library type '{typeName}'");
            }
        }

        private static Version ParseLibraryVersion(string version)
        {
            try
            {
                return LibraryVersionNumber.Parse(version, "version");
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"'{version}' is not a version number");
            }
        }

        private static ResponseLibraryTypes DescribeCleanUp(JsonObject content)
        {
            var removed = content["removed"] is JsonArray list ? list.Count : 0;

            return DescribeLibraryChange(content, removed == 0
                ? "Nothing in the project library was unused; nothing was removed."
                : $"{removed} version(s) removed from the project library. Save the project to keep it.");
        }
    }
}
