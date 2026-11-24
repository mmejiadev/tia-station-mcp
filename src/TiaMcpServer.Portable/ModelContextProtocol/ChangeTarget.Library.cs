using System;
using System.IO;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The targets of what deletes from the project library, and of global libraries, which are files outside the project.
    /// Apart from the rest of <see cref="ChangeTarget"/> only to keep each file readable.
    /// </remarks>
    public static partial class ChangeTarget
    {
        private const string LibraryDeletionPrefix = "library-delete/";
        private const string MasterCopyDeletionPrefix = "master-copy-delete/";
        private const string GlobalLibraryPrefix = "global-library/";

        /// <summary>Deleting or discarding from one project library type: a version, the version in work, or the whole type.</summary>
        /// <param name="typeName">The type's name, e.g. <c>FP_Motor</c>.</param>
        /// <returns>The target name, for example <c>library-delete/FP_Motor</c>.</returns>
        /// <remarks>
        /// Apart from <see cref="LibraryType"/>: a rule that lets a session edit and release a faceplate drawn by hand must
        /// not thereby let it delete the faceplate, which has no export to bring it back from.
        /// </remarks>
        public static string LibraryTypeDeletion(string typeName)
        {
            return LibraryDeletionPrefix + (typeName ?? string.Empty).Trim('/');
        }

        /// <summary>Deleting a master copy, or a master copy folder, from the project library.</summary>
        /// <param name="masterCopyPath">The master copy or folder, e.g. <c>Motors/FB_Motor</c>.</param>
        /// <returns>The target name, for example <c>master-copy-delete/Motors/FB_Motor</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the path is not one.</exception>
        /// <remarks>
        /// Apart from <see cref="ProjectLibrary"/>: a rule letting a session add master copies must not thereby let it delete
        /// them, and apart from <see cref="LibraryTypeDeletion"/>, whose names are types. A master copy has no export to keep it
        /// by, so what deletes one archives the project first. Spelled as the deletion reads it, each name trimmed: the tools
        /// find <c>Protected / FB_Motor</c> as <c>Protected/FB_Motor</c>, and a deny rule on that must not be stepped around.
        /// </remarks>
        public static string MasterCopyDeletion(string masterCopyPath)
        {
            return MasterCopyDeletionPrefix + MasterCopyRules.Join(MasterCopyRules.FolderSegments(masterCopyPath), string.Empty);
        }

        /// <summary>A global library, by the folder that holds it: where one is created.</summary>
        /// <param name="libraryFolder">The library's own folder, e.g. <c>C:\Libraries\Faceplates</c>.</param>
        /// <returns>The target name, for example <c>global-library/C:/Libraries/Faceplates</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the path cannot be read as one.</exception>
        /// <remarks>
        /// A family of its own, apart from the project library: a global library is a file outside the project,
        /// often shared with a class, so a policy that lets a session change the project does not reach it. By its
        /// full path and not its name, as a rule for a scratch library named <c>Kit</c> must not reach a class's
        /// shared one that is also named <c>Kit</c>. Made full first, so <c>..</c> cannot walk out of a rule;
        /// compared case aside by <see cref="Governance.TargetPattern"/>, as Windows paths are. A device path and an
        /// administrative share are refused, being another spelling of a path a deny rule names. A junction, a short
        /// 8.3 name or an ordinary share of a local folder is not resolved, so name what may be written in the allow
        /// list rather than relying on a deny rule against those.
        /// </remarks>
        public static string GlobalLibraryFolder(string libraryFolder)
        {
            if (string.IsNullOrWhiteSpace(libraryFolder))
            {
                return GlobalLibraryPrefix;
            }

            return GlobalLibraryPrefix + FullPath(libraryFolder).TrimEnd('\\', '/').Replace('\\', '/');
        }

        /// <summary>A global library about to be created: the same target as its folder once it is.</summary>
        /// <param name="directory">The folder it is created in, e.g. <c>C:\Libraries</c>.</param>
        /// <param name="libraryName">Its name, which becomes its own folder there.</param>
        /// <returns>The target name, for example <c>global-library/C:/Libraries/Faceplates</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when they cannot be read as a path.</exception>
        public static string NewGlobalLibrary(string directory, string libraryName)
        {
            return GlobalLibraryFolder($"{directory}{Path.DirectorySeparatorChar}{libraryName}");
        }

        /// <summary>A global library, by its file: publishing types to it. The same target as its folder.</summary>
        /// <param name="libraryFile">The library file, e.g. <c>C:\Libraries\Faceplates\Faceplates.al20</c>.</param>
        /// <returns>The target name, for example <c>global-library/C:/Libraries/Faceplates</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the path cannot be read as one.</exception>
        /// <remarks>The file's extension says which TIA Portal made it, so a rule on the folder outlives an upgrade.</remarks>
        public static string GlobalLibraryFile(string libraryFile)
        {
            return string.IsNullOrWhiteSpace(libraryFile)
                ? GlobalLibraryPrefix
                : GlobalLibraryFolder(Path.GetDirectoryName(FullPath(libraryFile)));
        }

        private static string FullPath(string path)
        {
            var spelled = path.Trim().Replace('/', '\\');
            if (spelled.StartsWith(@"\\?\", StringComparison.Ordinal) || spelled.StartsWith(@"\\.\", StringComparison.Ordinal) || IsAdministrativeShare(spelled))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' is another spelling of a path, which a write policy could not recognise; give the path itself");
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException || failure is PathTooLongException)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' is not a path: {failure.Message}", null, failure);
            }
        }

        private static bool IsAdministrativeShare(string spelled)
        {
            if (!spelled.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = spelled.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            return parts.Length > 1 && parts[1].EndsWith("$", StringComparison.Ordinal);
        }
    }
}
