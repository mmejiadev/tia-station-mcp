using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.ScreenGroup;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified screens, drawn through the object model Openness gives Unified and classic WinCC
    /// does not: every item is an object whose properties are set one by one. See
    /// <see cref="UnifiedScreenDescriptionReader"/> for the description and <see cref="UnifiedScreenReader"/>
    /// for the read, which is also the backup.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Draws a WinCC Unified screen from its description, replacing every item a screen of that name has.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>HMI_MTP700/HMI_RT_1</c>.</param>
        /// <param name="screen">The screen and its items.</param>
        /// <param name="backupDirectory">The directory the backup registry allocated; the screen as it was is read there first.</param>
        /// <returns>The screen as drawn, read back.</returns>
        /// <exception cref="PortalException">
        /// The path does not resolve; a type, property, value or tag in the description is wrong, so
        /// nothing was changed; or TIA Portal refused an item, which the message names.
        /// </exception>
        /// <remarks>
        /// The whole description is checked against the Openness types and the HMI's tags, and its scripts
        /// and animations rehearsed on a throw-away screen, before anything changes. What can still fail
        /// afterwards is TIA Portal refusing a value, and then the items before it are drawn and the backup
        /// holds the screen as it was.
        /// </remarks>
        public JsonObject DrawUnifiedScreen(string hmiPath, UnifiedScreenDescription screen, string backupDirectory)
        {
            _logger?.LogInformation("Drawing Unified screen {ScreenName} on {HmiPath}...", screen?.Name, hmiPath);

            try
            {
                var requested = screen ?? throw new PortalException(PortalErrorCode.InvalidParams, "A screen description is required");
                var software = RequireUnifiedSoftware(hmiPath);
                RequireDrawable(software, requested);

                // Before the rehearsal, which holds its screen under the requested name when no screen
                // outside a group has it: one inside a group must be refused, not shadowed.
                RequireNotInScreenGroup(software, requested.Name);
                UnifiedRehearsal.Run(software, requested.Name, requested.Items);

                var language = CurrentProject!.LanguageSettings.EditingLanguage;
                var existing = software.Screens.Find(requested.Name);
                var backup = BackUpUnifiedScreen(existing, software, language, backupDirectory);

                var target = existing ?? software.Screens.Create(requested.Name);
                Redraw(target, requested, language, backup);

                if (requested.IsStartScreen)
                {
                    software.RuntimeSettings.StartScreen = requested.Name;
                }

                return new UnifiedScreenReader(language).Read(target, software.RuntimeSettings.StartScreen);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "DrawUnifiedScreen"), "DrawUnifiedScreen", hmiPath, screen?.Name ?? string.Empty);
            }
        }

        /// <summary>Reads a WinCC Unified screen in the shape <see cref="DrawUnifiedScreen"/> takes.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="screenName">The screen.</param>
        /// <returns>The screen as a description.</returns>
        /// <exception cref="PortalException">The path or the screen does not exist; the refusal names the screens there are.</exception>
        public JsonObject GetUnifiedScreen(string hmiPath, string screenName)
        {
            try
            {
                var software = RequireUnifiedSoftware(hmiPath);
                var screen = software.Screens.Find(screenName ?? string.Empty)
                    ?? FindInScreenGroups(software.ScreenGroups, screenName ?? string.Empty)?.Screen
                    ?? throw new PortalException(
                        PortalErrorCode.NotFound,
                        $"'{hmiPath}' has no screen '{screenName}'. Screens: {string.Join(", ", software.Screens.Select(candidate => candidate.Name))}");

                return new UnifiedScreenReader(CurrentProject!.LanguageSettings.EditingLanguage).Read(screen, software.RuntimeSettings.StartScreen);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedScreen", hmiPath, screenName ?? string.Empty);
            }
        }

        /// <summary>Describes what a description may set on one WinCC Unified item type.</summary>
        /// <param name="itemType">The item type, e.g. <c>rectangle</c>.</param>
        /// <returns>Every property path with its value type, its enum values and whether it can be set.</returns>
        /// <exception cref="PortalException">The name is not a drawable item.</exception>
        /// <remarks>Read from the Openness types: needs neither a project nor a connection to TIA Portal.</remarks>
        public static JsonObject DescribeUnifiedItemType(string itemType)
        {
            return UnifiedItemCatalog.Describe(itemType);
        }

        private UnifiedHmiSoftware RequireUnifiedSoftware(string hmiPath)
        {
            if (string.IsNullOrWhiteSpace(hmiPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "hmiPath is required");
            }

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before working with an HMI");
            }

            return GetSoftwareContainer(hmiPath)?.Software as UnifiedHmiSoftware
                ?? throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"'{hmiPath}' is not a WinCC Unified HMI. Unified HMIs in this project: "
                    + string.Join(", ", SoftwareEntries().Where(entry => entry.Software is UnifiedHmiSoftware).Select(entry => entry.Path)));
        }

        private static void RequireDrawable(UnifiedHmiSoftware software, UnifiedScreenDescription screen)
        {
            foreach (var setting in screen.Properties)
            {
                UnifiedPropertyWriter.Check(typeof(HmiScreen), setting);
            }

            foreach (var item in screen.Items)
            {
                UnifiedScreenItemWriter.Check(item);
            }

            // A link to a tag that does not exist is refused here, the way every classic HMI write
            // refuses one: in classic WinCC such a link once ended the project session.
            var missing = screen.LinkedTags.Where(tag => software.Tags.Find(tag) == null).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was drawn: these HMI tags do not exist on the HMI: {string.Join(", ", missing)}");
            }

            // Openness takes a list that does not exist without a word, and only the compile reports it.
            var missingLists = screen.LinkedTextLists.Where(list => software.HmiTextLists.Find(list) == null).ToList();
            if (missingLists.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Nothing was drawn: these text lists do not exist on the HMI: {string.Join(", ", missingLists)}. Create them first with CreateUnifiedTextList");
            }
        }

        /// <summary>Saves the screen as it is, and refuses when that copy would not put it back.</summary>
        private static string BackUpUnifiedScreen(HmiScreen? existing, UnifiedHmiSoftware software, Language language, string backupDirectory)
        {
            Directory.CreateDirectory(backupDirectory);
            var file = Path.Combine(backupDirectory, SnapshotFileName.For(existing?.Name ?? "new-screen") + ".json");

            var snapshot = existing == null
                ? new JsonObject { ["existed"] = false }
                : new UnifiedScreenReader(language).Read(existing, software.RuntimeSettings.StartScreen);

            File.WriteAllText(file, snapshot.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            if (existing != null)
            {
                UnifiedBackupCheck.RequireRestorable(existing, snapshot);
            }

            return file;
        }

        /// <remarks>
        /// <c>Screens</c> holds only the screens outside any group. A screen of the same name inside a group
        /// would not be found there, and drawing would create a second one instead of backing that one up.
        /// </remarks>
        private static void RequireNotInScreenGroup(UnifiedHmiSoftware software, string screenName)
        {
            if (FindInScreenGroups(software.ScreenGroups, screenName) is (HmiScreen _, string group))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Nothing was drawn: the screen '{screenName}' is in the screen group '{group}', and this tool draws only screens outside a group");
            }
        }

        private static (HmiScreen Screen, string Group)? FindInScreenGroups(HmiScreenGroupComposition groups, string screenName)
        {
            foreach (var group in groups)
            {
                if (group.Screens.Find(screenName) is HmiScreen screen)
                {
                    return (screen, group.Name);
                }

                if (FindInScreenGroups(group.Groups, screenName) is (HmiScreen nested, string inner))
                {
                    return (nested, $"{group.Name}/{inner}");
                }
            }

            return null;
        }

        private static void Redraw(HmiScreen target, UnifiedScreenDescription screen, Language language, string backup)
        {
            var properties = new UnifiedPropertyWriter(language);
            var items = new UnifiedScreenItemWriter(properties);

            foreach (var existing in target.ScreenItems.ToList())
            {
                existing.Delete();
            }

            foreach (var setting in screen.Properties)
            {
                properties.Apply(target, setting);
            }

            foreach (var item in screen.Items)
            {
                DrawOrExplain(items, target, item, backup);
            }
        }

        /// <remarks>
        /// An ended session passes through untouched, so the caller still learns the project must be
        /// opened again; anything else is reported with the item it stopped at and where the screen as
        /// it was is kept.
        /// </remarks>
        private static void DrawOrExplain(UnifiedScreenItemWriter items, HmiScreen target, UnifiedItemDescription item, string backup)
        {
            try
            {
                items.Draw(target, item);
            }
            catch (Exception failure) when (!(failure is NonRecoverableException || failure is EngineeringObjectDisposedException))
            {
                // A refusal of the description itself (a broken event script) keeps its own code: it is
                // a mistake to fix in the call, not an environment failure to retry.
                throw new PortalException(
                    (failure as PortalException)?.Code ?? PortalErrorCode.WriteFailed,
                    $"TIA Portal refused item '{item.Name}': {failure.Message}. The items before it were drawn; "
                    + $"the screen as it was is in '{backup}', and DrawUnifiedScreen with that file's content draws it back.",
                    null,
                    failure);
            }
        }
    }
}
