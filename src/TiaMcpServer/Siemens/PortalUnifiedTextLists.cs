using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTextLists;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified text lists, which Openness V20 offers only as files to export and import. See
    /// <see cref="UnifiedTextListStore"/> for why every write is read back. Graphic lists are not in
    /// Openness V20 for Unified at all: the HMI software has <c>HmiTextLists</c> and nothing for graphics.
    /// </remarks>
    public partial class Portal
    {
        private const string BeforeDirectory = "before";
        private const string RequestDirectory = "request";
        private const string AfterDirectory = "after";
        private const string RestoredDirectory = "restored";

        /// <summary>Reads every text list of a WinCC Unified HMI.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>HMI_MTP700/HMI_RT_1</c>.</param>
        /// <returns><c>{ textLists: [{ name, entries }] }</c>, texts in the project's editing language.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI, or the export is not in the shape measured on V20.</exception>
        public JsonObject GetUnifiedTextLists(string hmiPath)
        {
            var directory = Path.Combine(Path.GetTempPath(), "tia-mcp-textlists-" + Guid.NewGuid().ToString("N"));

            try
            {
                var hmi = RequireUnifiedSoftware(hmiPath);
                var lists = UnifiedTextListStore.ReadAll(hmi, UnifiedTextListStore.LanguageOf(CurrentProject!), directory);

                return new JsonObject { ["textLists"] = new JsonArray(lists.Select(list => (JsonNode)UnifiedTextListStore.Describe(list)).ToArray()) };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedTextLists", hmiPath, string.Empty);
            }
            finally
            {
                DeleteScratchDirectory(directory);
            }
        }

        /// <summary>Creates a WinCC Unified text list, or replaces the one of that name, and checks TIA Portal kept it as asked.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="list">The list.</param>
        /// <param name="backupDirectory">The directory the backup registry allocated: every list as it was is exported there first.</param>
        /// <returns>The list read back.</returns>
        /// <exception cref="PortalException">
        /// The path is not a WinCC Unified HMI; a list of that name is spelt otherwise; a replacement could
        /// not be undone safely; or TIA Portal did not keep the list as asked, and it was undone — deleted
        /// when it was new, every list imported back from the backup when it replaced one.
        /// </exception>
        public JsonObject CreateUnifiedTextList(string hmiPath, UnifiedTextList list, string backupDirectory)
        {
            _logger?.LogInformation("Writing Unified text list {ListName} on {HmiPath}...", list?.Name, hmiPath);

            try
            {
                var requested = list ?? throw new PortalException(PortalErrorCode.InvalidParams, "A text list is required");
                var hmi = RequireUnifiedSoftware(hmiPath);
                var write = new TextListWrite(hmi, requested, UnifiedTextListStore.LanguageOf(CurrentProject!), backupDirectory);
                RequireSameSpelling(hmi, requested.Name);
                BackUpTextLists(write);

                var readBack = ImportAndReadBack(write);

                return UnifiedTextListStore.Describe(readBack);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "CreateUnifiedTextList"), "CreateUnifiedTextList", hmiPath, list?.Name ?? string.Empty);
            }
        }

        /// <summary>What one write of a text list works with, and what it found before writing.</summary>
        private sealed class TextListWrite
        {
            public TextListWrite(UnifiedHmiSoftware hmi, UnifiedTextList requested, string language, string backupDirectory)
            {
                Hmi = hmi;
                Requested = requested;
                Language = language;
                BackupDirectory = backupDirectory;
            }

            public UnifiedHmiSoftware Hmi { get; }

            public UnifiedTextList Requested { get; }

            public string Language { get; }

            public string BackupDirectory { get; }

            /// <summary>Every list as it was, read from the backup; empty when the HMI had none.</summary>
            public IReadOnlyList<UnifiedTextList> Before { get; set; } = Array.Empty<UnifiedTextList>();

            /// <summary>The name the backup's files took, or null when the HMI had no list.</summary>
            public string? SavedUnder { get; set; }

            public bool IsReplacement => Before.Any(list => list.Name == Requested.Name);

            public string Directory(string name) => Path.Combine(BackupDirectory, name);
        }

        /// <remarks>
        /// Openness finds a list by name case aside, while the export spells it as it was created; a
        /// write under another spelling would read back as "no list" and be undone for nothing.
        /// </remarks>
        private static void RequireSameSpelling(UnifiedHmiSoftware hmi, string name)
        {
            var existing = hmi.HmiTextLists.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null && existing.Name != name)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The HMI has a text list '{existing.Name}': write it under that spelling to replace it");
            }
        }

        /// <summary>
        /// Exports every list as it is, and refuses a replacement whose undo would lose a list: the undo
        /// imports the whole backup again, and a list holding a range comes back empty.
        /// </summary>
        private static void BackUpTextLists(TextListWrite write)
        {
            Directory.CreateDirectory(write.BackupDirectory);
            var first = write.Hmi.HmiTextLists.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            write.Before = UnifiedTextListStore.ReadAll(write.Hmi, write.Language, write.Directory(BeforeDirectory));
            write.SavedUnder = first.Name;

            var ranged = write.Before.Where(list => list.Entries.Any(entry => !entry.IsSingleValue)).Select(list => list.Name).ToList();
            if (write.IsReplacement && ranged.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Nothing was written: replacing '{write.Requested.Name}' could not be undone safely, because undoing imports every list again and TIA Portal empties a list holding a range of values ({string.Join(", ", ranged)}). Change those ranges in TIA Portal, or write a list of another name");
            }
        }

        /// <remarks>
        /// Anything that fails after the import — the import itself, the export, a file in a shape not
        /// measured — undoes it as a mismatch does: the list must not stay written while the call fails.
        /// </remarks>
        private static UnifiedTextList ImportAndReadBack(TextListWrite write)
        {
            UnifiedTextList? readBack;
            bool imported;

            try
            {
                imported = UnifiedTextListStore.Import(write.Hmi, write.Requested, write.Language, write.Directory(RequestDirectory));
                readBack = UnifiedTextListStore.ReadAll(write.Hmi, write.Language, write.Directory(AfterDirectory)).FirstOrDefault(candidate => candidate.Name == write.Requested.Name);
            }
            catch (Exception failure) when (failure is PortalException || failure is IOException || failure is EngineeringTargetInvocationException)
            {
                throw UndoTextList(write, $"reading it back failed ({failure.Message})", failure);
            }

            if (!imported || !UnifiedTextListStore.IsSame(write.Requested, readBack))
            {
                throw UndoTextList(write, $"it kept {Kept(readBack)}", null);
            }

            return readBack!;
        }

        /// <remarks>
        /// A new list is deleted; a replaced one is put back by importing every list from the backup, which
        /// is then read back and compared, list by list. Texts TIA Portal added to the shared library stay
        /// there: Openness V20 has no way to delete them. When the undo itself fails, the message says so
        /// and names where the backup is, rather than hiding what happened.
        /// </remarks>
        private static PortalException UndoTextList(TextListWrite write, string what, Exception? cause)
        {
            var asked = $"TIA Portal did not keep the text list '{write.Requested.Name}' as asked: {what}";

            try
            {
                var outcome = write.IsReplacement ? RestoreTextLists(write) : DeleteTextList(write);

                return new PortalException(PortalErrorCode.WriteFailed, $"{asked}; {outcome}", null, cause);
            }
            catch (Exception failure) when (failure is PortalException || failure is IOException || failure is EngineeringTargetInvocationException)
            {
                return new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"{asked}; undoing it failed too ({failure.Message}): every list as it was is in '{write.Directory(BeforeDirectory)}'",
                    null,
                    failure);
            }
        }

        private static string DeleteTextList(TextListWrite write)
        {
            write.Hmi.HmiTextLists.Find(write.Requested.Name)?.Delete();

            return "the new list was deleted again";
        }

        private static string RestoreTextLists(TextListWrite write)
        {
            var imported = UnifiedTextListStore.Restore(write.Hmi, write.Directory(BeforeDirectory), write.SavedUnder!);
            var now = UnifiedTextListStore.ReadAll(write.Hmi, write.Language, write.Directory(RestoredDirectory));
            var differ = write.Before.Where(list => !UnifiedTextListStore.IsSame(list, now.FirstOrDefault(candidate => candidate.Name == list.Name))).Select(list => list.Name).ToList();

            return imported && differ.Count == 0
                ? "every list was imported back from the backup and reads as it did"
                : $"importing the backup back left {(differ.Count == 0 ? "an unknown state" : string.Join(", ", differ))} unlike before: every list as it was is in '{write.Directory(BeforeDirectory)}'";
        }

        private static string Kept(UnifiedTextList? readBack)
        {
            return readBack == null
                ? "no list"
                : $"{readBack.Entries.Count} entr{(readBack.Entries.Count == 1 ? "y" : "ies")}{(readBack.Entries.Count == 0 ? string.Empty : ": ")}{string.Join(", ", readBack.Entries.Select(entry => $"{entry.From}..{entry.To} '{entry.Text}'"))}";
        }

        /// <remarks>
        /// A scratch export of a read; failing to delete it — a file still held by TIA Portal or a virus
        /// scanner — must not replace the answer or the real failure, so it is logged instead.
        /// </remarks>
        private void DeleteScratchDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _logger?.LogWarning(failure, "Could not delete the scratch export {Directory}", directory);
            }
        }
    }
}
