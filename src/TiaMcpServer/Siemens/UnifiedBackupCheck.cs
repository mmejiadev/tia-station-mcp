using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Refuses to redraw a WinCC Unified screen whose backup could not put it back.</summary>
    /// <remarks>
    /// A Unified screen has no export, so its backup is what <see cref="UnifiedScreenReader"/> reads.
    /// Redrawing deletes every item first, so anything the backup does not hold would be gone for good:
    /// what <see cref="UnifiedBackupCoverage"/> names, an item of a type this tool does not draw, and a
    /// property that could not be read. The rule that every write is preceded by a backup of the previous
    /// state means such a screen is not redrawn.
    /// </remarks>
    internal static class UnifiedBackupCheck
    {
        private const int MaxListed = 20;

        /// <summary>Throws when the backup of a screen would not restore it.</summary>
        /// <param name="screen">The screen about to be redrawn.</param>
        /// <param name="backup">Its backup, as <see cref="UnifiedScreenReader"/> read it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming what would be lost.</exception>
        public static void RequireRestorable(HmiScreen screen, JsonObject backup)
        {
            var lost = new List<string>(UnifiedBackupCoverage.Lost(screen, $"the screen '{screen.Name}'"));

            foreach (var item in screen.ScreenItems)
            {
                if (!UnifiedItemTypes.IsDrawable(item.GetType().FullName!))
                {
                    lost.Add($"'{item.Name}' is a {item.GetType().Name}, which this tool does not draw back");
                }

                lost.AddRange(UnifiedBackupCoverage.Lost(item, $"'{item.Name}'"));
            }

            if (backup["unreadable"] is JsonArray unreadable)
            {
                lost.AddRange(unreadable.Select(entry => $"a property that could not be read ({entry})"));
            }

            if (lost.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Nothing was drawn: '{screen.Name}' holds what its backup cannot keep, so redrawing it would lose it for good: "
                    + $"{string.Join("; ", lost.Take(MaxListed))}{(lost.Count > MaxListed ? "; …" : string.Empty)}. "
                    + "Change those in TIA Portal first, or draw a screen of another name.");
            }
        }
    }
}
