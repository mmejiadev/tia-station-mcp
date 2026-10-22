using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Screens and text lists generated from a description: see <see cref="HmiScreenDescriptionReader"/>
    /// and <see cref="HmiTextListDocument"/> for their shapes. Both go through the same check every HMI
    /// import does, <c>RequireKnownLinks</c>.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Generates a screen on a classic WinCC panel from its description, replacing a screen of the
        /// same name.
        /// </summary>
        /// <param name="hmiPath">Full path to the HMI target.</param>
        /// <param name="folderPath">Screen folder to put it in, or empty for the root.</param>
        /// <param name="screen">The screen and its items.</param>
        /// <param name="backupDirectory">
        /// The directory the backup registry allocated for this change. The panel is exported there
        /// first, and the generated screen is kept in its <c>generated</c> folder.
        /// </param>
        /// <exception cref="PortalException">
        /// A path does not resolve; the screen links to an HMI tag, a screen or a text list the panel
        /// does not have; the backup was incomplete so nothing was written; or TIA Portal rejected it.
        /// </exception>
        public void GenerateHmiScreen(string hmiPath, string folderPath, HmiScreenDescription screen, string backupDirectory)
        {
            _logger?.LogInformation("Generating screen {ScreenName} on {HmiPath}...", screen?.Name, hmiPath);

            try
            {
                var requested = screen ?? throw new PortalException(PortalErrorCode.InvalidParams, "A screen description is required");
                var hmi = RequireHmiTarget(hmiPath);
                var folder = RequireScreenFolder(hmi, folderPath);

                var exports = SaveHmiBackup(hmi, backupDirectory);
                var description = Numbered(requested, exports);

                var file = WriteGenerated(description.Name, HmiScreenDocument.Build(description), backupDirectory);
                RequireKnownLinks(hmi, File.ReadAllText(file.FullName), exports);
                RequireImported(folder.Screens.Import(file, ImportOptions.Override).Select(imported => imported.Name), description.Name);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "GenerateHmiScreen"), "GenerateHmiScreen", hmiPath, screen?.Name ?? string.Empty);
            }
        }

        /// <summary>Imports a generated text list into a classic WinCC panel, replacing one of the same name.</summary>
        /// <param name="hmiPath">Full path to the HMI target.</param>
        /// <param name="listName">The text list's name.</param>
        /// <param name="listXml">The text list as <see cref="HmiTextListDocument"/> writes it.</param>
        /// <param name="backupDirectory">
        /// The directory the backup registry allocated for this change. The panel is exported there
        /// first, and the generated list is kept in its <c>generated</c> folder.
        /// </param>
        /// <exception cref="PortalException">
        /// The path does not resolve, the backup was incomplete so nothing was written, or TIA Portal
        /// rejected the list.
        /// </exception>
        public void GenerateHmiTextList(string hmiPath, string listName, string listXml, string backupDirectory)
        {
            _logger?.LogInformation("Generating text list {ListName} on {HmiPath}...", listName, hmiPath);

            try
            {
                var hmi = RequireHmiTarget(hmiPath);

                var exports = SaveHmiBackup(hmi, backupDirectory);

                var file = WriteGenerated(listName, listXml, backupDirectory);
                RequireKnownLinks(hmi, listXml, exports);
                RequireImported(hmi.TextLists.Import(file, ImportOptions.Override).Select(list => list.Name), listName);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "GenerateHmiTextList"), "GenerateHmiTextList", hmiPath, listName);
            }
        }

        /// <summary>The description with the number it must be imported with.</summary>
        /// <remarks>
        /// Two rules from the Openness help: a screen imported without a number gets a free one, and an
        /// import whose number another screen has is cancelled. So a screen regenerated without a number
        /// keeps the one it had — a free one would quietly break the buttons and screen-number tags
        /// that open it by number — and a number another screen has is refused here, with that
        /// screen's name, rather than by the import. The numbers come from the panel's export, which
        /// the backup has just written: the API does not give them (see <see cref="HmiScreenNumbers"/>).
        /// </remarks>
        private static HmiScreenDescription Numbered(HmiScreenDescription screen, IReadOnlyList<string> panelExports)
        {
            var numbers = HmiScreenNumbers.Read(panelExports);

            if (screen.Number.HasValue)
            {
                RequireFreeNumber(screen.Name, screen.Number.Value, numbers);
                return screen;
            }

            var current = numbers.FirstOrDefault(entry => entry.Key.Equals(screen.Name, StringComparison.OrdinalIgnoreCase));

            return current.Key == null ? screen : new HmiScreenDescription(screen.Name, current.Value, screen.BackColor, screen.Layers);
        }

        /// <summary>Refuses a screen document whose number another screen of the panel has.</summary>
        /// <param name="screenXml">The screen about to be imported.</param>
        /// <param name="panelExports">The panel's export.</param>
        private static void RequireFreeNumbers(string screenXml, IReadOnlyList<string> panelExports)
        {
            var numbers = HmiScreenNumbers.Read(panelExports);

            foreach (var screen in HmiScreenNumbers.Read(new[] { screenXml }))
            {
                RequireFreeNumber(screen.Key, screen.Value, numbers);
            }
        }

        private static void RequireFreeNumber(string name, int number, IReadOnlyList<KeyValuePair<string, int>> numbers)
        {
            var holder = numbers.FirstOrDefault(entry => entry.Value == number && !entry.Key.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (holder.Key != null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Screen number {number} is already '{holder.Key}''s; give another, or none for a free one");
            }
        }

        /// <summary>Keeps a generated document in the backup's <c>generated</c> folder, where it is imported from.</summary>
        private static FileInfo WriteGenerated(string name, string xml, string backupDirectory)
        {
            var directory = Directory.CreateDirectory(Path.Combine(backupDirectory, GeneratedDirectoryName));
            var file = new FileInfo(Path.Combine(directory.FullName, SnapshotFileName.For(name) + ".xml"));

            File.WriteAllText(file.FullName, xml);

            return file;
        }
    }
}
