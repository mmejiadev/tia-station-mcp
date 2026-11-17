using Siemens.Engineering.HmiUnified.UI.Screens;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TiaMcpServer.Siemens
{
    /// <summary>Lists a WinCC Unified screen's items in the backup registry before items are added to it.</summary>
    /// <remarks>
    /// What adding faceplates or Custom Web Controls changes is the list of items: the list before is enough to tell what
    /// to delete to undo it. One format for both writers (review of 2026-11-16).
    /// </remarks>
    internal static class UnifiedScreenBackup
    {
        private const string ScreenItemsFile = "screen-items.json";

        /// <summary>Writes <c>screen-items.json</c>: <c>{ screen, items: [ names ] }</c>.</summary>
        /// <param name="screen">The screen about to change.</param>
        /// <param name="backupDirectory">The change's backup folder.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when no folder is given.</exception>
        public static void SaveItems(HmiScreen screen, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the screen's items are listed before any is added");
            }

            var items = new JsonObject
            {
                ["screen"] = screen.Name,
                ["items"] = new JsonArray(screen.ScreenItems.Select(item => (JsonNode)item.Name).ToArray())
            };

            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(Path.Combine(backupDirectory, ScreenItemsFile), items.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
