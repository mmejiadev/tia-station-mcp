using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <remarks>Changing some items of a WinCC Unified screen; see <see cref="UnifiedScreenEditor"/>.</remarks>
    public partial class Portal
    {
        /// <summary>Changes some items of a WinCC Unified screen, leaving the others as they are.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <param name="edit">The screen's properties to set, and the items to remove, set and draw.</param>
        /// <param name="backupDirectory">Where the items the edit touches are read first.</param>
        /// <returns><c>{ screen, removed, set, drawn, backup, items }</c>: what was done, and the screen's items after it.</returns>
        /// <exception cref="PortalException">
        /// The screen or an item is missing; a property, tag, text list or script does not fit; an item touched could not
        /// be put back from its backup - nothing changed then; or TIA Portal refused part of the edit, which was undone.
        /// </exception>
        public JsonObject EditUnifiedScreen(string hmiPath, UnifiedScreenEdit edit, string backupDirectory)
        {
            _logger?.LogInformation("Editing Unified screen {ScreenName} on {HmiPath}...", edit?.Screen, hmiPath);

            try
            {
                var requested = edit ?? throw new PortalException(PortalErrorCode.InvalidParams, "An edit is required");
                var software = RequireUnifiedSoftware(hmiPath);

                // As for a drawing: the rehearsal holds its screen under this name when none outside a group has it.
                RequireNotInScreenGroup(software, requested.Screen);
                var screen = FindUnifiedScreen(software, hmiPath, requested.Screen);
                RequireDrawable(software, requested.Drawing);

                var editor = new UnifiedScreenEditor(screen, requested, CurrentProject!.LanguageSettings.EditingLanguage);
                editor.Check(software);
                var (file, redrawn) = editor.BackUp(backupDirectory);
                editor.Rehearse(software);
                editor.Apply(redrawn);

                return new JsonObject
                {
                    ["screen"] = screen.Name,
                    ["removed"] = NameList(requested.Remove),
                    ["set"] = NameList(requested.Set.Select(item => item.Name)),
                    ["drawn"] = NameList(requested.Draw.Select(item => item.Name)),
                    ["backup"] = file,
                    ["items"] = NameList(screen.ScreenItems.Select(item => item.Name))
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "EditUnifiedScreen"), "EditUnifiedScreen", hmiPath, edit?.Screen ?? string.Empty);
            }
        }

        private static JsonArray NameList(IEnumerable<string> names)
        {
            return new JsonArray(names.Select(name => (JsonNode)name).ToArray());
        }
    }
}
