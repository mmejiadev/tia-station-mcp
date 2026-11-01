using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <summary>Removes what a failed copy into the project library added: its types, then the folders it made.</summary>
    /// <remarks>
    /// Types are removed in passes, see <see cref="RepeatedRemoval"/>; whatever is left is named, never
    /// hidden. Folders go last, deepest first, and only when empty, so a folder that held something before
    /// the copy is never touched.
    /// </remarks>
    internal static class LibraryCopyUndo
    {
        /// <summary>Removes what the project library gained since <paramref name="before"/>, and builds the failure to throw.</summary>
        /// <param name="project">The project library's type folder.</param>
        /// <param name="before">The type names and folder paths it held before the copy.</param>
        /// <param name="failure">The failure that stopped the copy.</param>
        /// <returns>The failure, its code kept, saying whether anything is left.</returns>
        public static PortalException Undo(LibraryTypeFolder project, (IReadOnlyCollection<string> Types, IReadOnlyCollection<string> Folders) before, Exception failure)
        {
            var left = RemoveTypes(LibraryTypeReader.All(project).Select(entry => entry.Type).Where(type => !before.Types.Contains(type.Name, StringComparer.OrdinalIgnoreCase)).ToList());
            left.AddRange(RemoveEmptyFolders(LibraryTypeReader.Folders(project).Where(entry => !before.Folders.Contains(entry.Path, StringComparer.OrdinalIgnoreCase)).ToList()));

            var code = (failure as PortalException)?.Code ?? PortalErrorCode.WriteFailed;
            var outcome = left.Count == 0
                ? "Nothing was copied: what the copy had added was removed again."
                : $"Removing what the copy had added failed for {string.Join(", ", left)}; remove those by hand in TIA Portal before trying again.";

            return new PortalException(code, $"{failure.Message.Trim()} {outcome}", null, failure);
        }

        private static List<string> RemoveTypes(List<LibraryType> added)
        {
            return RepeatedRemoval.Run(added, type => UnifiedRollback.TryDelete(type.Delete)).Select(type => $"type '{type.Name}'").ToList();
        }

        private static IEnumerable<string> RemoveEmptyFolders(List<(string Path, LibraryTypeUserFolder Folder)> added)
        {
            foreach (var entry in added.OrderByDescending(found => found.Path.Length))
            {
                var isEmpty = entry.Folder.Types.Count == 0 && entry.Folder.Folders.Count == 0;

                if (!isEmpty || !UnifiedRollback.TryDelete(entry.Folder.Delete))
                {
                    yield return $"folder '{entry.Path}'";
                }
            }
        }
    }
}
