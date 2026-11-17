using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Keeping the project library tidy, one type at a time: throwing a version in work away, choosing the default
    /// version, deleting a version or a whole type.
    ///
    /// Measured 2026-11-15 on TIA Portal V21, on a copy of a class project: <c>Discard</c>, <c>SetAsDefault</c> and
    /// <c>Delete</c> all work; TIA Portal itself refuses to delete the default version, a version other types depend on,
    /// and a type that has instances, each with a message naming why, which is passed on as an invalid state. A
    /// faceplate's content has no export, so what deletes or discards is preceded by an archive of the saved project
    /// (<see cref="ArchiveProjectToBackup"/>); choosing the default, which is undone by choosing again, keeps the type's
    /// description.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Throws away a project library type's version in work.</summary>
        /// <param name="typeName">The type, as GetLibraryTypes names it.</param>
        /// <param name="backupDirectory">Where the project is archived first.</param>
        /// <returns>The type, read back; or <c>{ deleted, folder, reason }</c> when its only version was the one in work, which takes the type with it.</returns>
        /// <exception cref="PortalException">No such type, no version in work, unsaved changes, or TIA Portal refused.</exception>
        public JsonObject DiscardLibraryTypeEdit(string typeName, string backupDirectory)
        {
            _logger?.LogInformation("Discarding the version in work of library type {Type}...", typeName);

            return RunLibraryMaintenance("DiscardLibraryTypeEdit", typeName, () =>
            {
                var found = RequireLibraryType(typeName);
                var name = found.Type.Name;
                var versions = LibraryTypeReader.Versions(found.Type);
                var number = LibraryVersionRules.RequireDiscardable(name, versions);

                ArchiveProjectToBackup(backupDirectory, name);
                FindVersion(found.Type, number).Discard();

                // A type whose only version was in work goes with it, and is not read again.
                return LibraryVersionRules.RemovesTheType(versions)
                    ? new JsonObject { ["deleted"] = name, ["folder"] = found.Folder, ["reason"] = "its only version was the one in work" }
                    : LibraryTypeReader.Describe(found.Folder, found.Type);
            });
        }

        /// <summary>Makes a released version of a project library type its default, the one new instances take.</summary>
        /// <param name="typeName">The type, as GetLibraryTypes names it.</param>
        /// <param name="version">The released version.</param>
        /// <param name="backupDirectory">Where the type is described first.</param>
        /// <returns>The type, read back.</returns>
        /// <exception cref="PortalException">No such type or version, the version is in work, or TIA Portal refused.</exception>
        public JsonObject SetDefaultLibraryTypeVersion(string typeName, Version version, string backupDirectory)
        {
            _logger?.LogInformation("Making {Version} the default of library type {Type}...", version, typeName);

            return RunLibraryMaintenance("SetDefaultLibraryTypeVersion", typeName, () =>
            {
                var found = RequireLibraryType(typeName);
                var number = LibraryVersionRules.RequireDefaultable(found.Type.Name, LibraryTypeReader.Versions(found.Type), version);

                SaveLibraryBackup(backupDirectory, $"{SnapshotFileName.For(found.Type.Name)}.json", LibraryTypeReader.Describe(found.Folder, found.Type));
                FindVersion(found.Type, number).SetAsDefault();

                return LibraryTypeReader.Describe(found.Folder, found.Type);
            });
        }

        /// <summary>Deletes a released version of a project library type that is not its default.</summary>
        /// <param name="typeName">The type, as GetLibraryTypes names it.</param>
        /// <param name="version">The version.</param>
        /// <param name="backupDirectory">Where the project is archived first.</param>
        /// <returns>The type, read back.</returns>
        /// <exception cref="PortalException">
        /// No such type or version, the version is in work, the default or the only one, unsaved changes, or TIA Portal
        /// refused (other types depend on it).
        /// </exception>
        public JsonObject DeleteLibraryTypeVersion(string typeName, Version version, string backupDirectory)
        {
            _logger?.LogInformation("Deleting version {Version} of library type {Type}...", version, typeName);

            return RunLibraryMaintenance("DeleteLibraryTypeVersion", typeName, () =>
            {
                var found = RequireLibraryType(typeName);
                var number = LibraryVersionRules.RequireDeletable(found.Type.Name, LibraryTypeReader.Versions(found.Type), version);

                ArchiveProjectToBackup(backupDirectory, $"{found.Type.Name} {LibraryVersionNumber.Format(number)}");
                RunRefusable(() => FindVersion(found.Type, number).Delete());

                return LibraryTypeReader.Describe(found.Folder, found.Type);
            });
        }

        /// <summary>Deletes a project library type with all its versions.</summary>
        /// <param name="typeName">The type, as GetLibraryTypes names it.</param>
        /// <param name="backupDirectory">Where the project is archived first.</param>
        /// <returns><c>{ deleted, folder, versions }</c>: what was deleted.</returns>
        /// <exception cref="PortalException">No such type, a version in work, unsaved changes, or TIA Portal refused (instances, dependents).</exception>
        public JsonObject DeleteLibraryType(string typeName, string backupDirectory)
        {
            _logger?.LogInformation("Deleting library type {Type}...", typeName);

            return RunLibraryMaintenance("DeleteLibraryType", typeName, () =>
            {
                var found = RequireLibraryType(typeName);
                var name = found.Type.Name;
                var described = LibraryTypeReader.Describe(found.Folder, found.Type);

                LibraryVersionRules.RequireTypeDeletable(name, LibraryTypeReader.Versions(found.Type));
                ArchiveProjectToBackup(backupDirectory, name);
                RunRefusable(() => found.Type.Delete());

                // Nothing is read from the type after Delete: the object is gone, and touching it would report a done delete as failed.
                return new JsonObject { ["deleted"] = name, ["folder"] = found.Folder, ["versions"] = described["versions"]?.DeepClone() };
            });
        }

        /// <remarks>
        /// Only around a delete. TIA Portal's refusals to delete - a default version, dependents, instances - arrive as a
        /// target invocation failure (measured 2026-11-15, its message saying the thing "cannot be deleted"); they are the
        /// caller's to act on, so they come back as an invalid state with TIA Portal's words. The message is not matched: it
        /// is in TIA Portal's user interface language (review of 2026-11-16). Discarding, choosing the default and cleaning
        /// up had no refusal measured, and any failure of theirs stays an operation failure.
        /// </remarks>
        private static void RunRefusable(Action change)
        {
            try
            {
                change();
            }
            catch (EngineeringTargetInvocationException refused)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal refused: {refused.Message.Trim()}", null, refused);
            }
        }


        private JsonObject RunLibraryMaintenance(string operation, string typeName, Func<JsonObject> change)
        {
            try
            {
                return change();
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, operation, PortalErrorCode.WriteFailed), operation, string.Empty, typeName ?? string.Empty);
            }
        }
    }
}
