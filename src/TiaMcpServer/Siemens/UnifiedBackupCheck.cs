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
    /// what <see cref="UnifiedBackupCoverage"/> names, an item of a type this tool does not draw, a
    /// property that could not be read, a tag link or animation of the screen itself (the reader reads
    /// only its values), and anything in the backup the description reader or the item checks would
    /// refuse — a range table with overlapping ranges, say, which TIA Portal takes and a description does
    /// not. The rule that every write is preceded by a backup of the previous state means such a screen
    /// is not redrawn.
    /// </remarks>
    internal static class UnifiedBackupCheck
    {
        private const int MaxListed = 20;

        /// <summary>Why the backup, given back to <c>DrawUnifiedScreen</c>, would be refused before drawing; null when it would not.</summary>
        /// <remarks>
        /// The same reader and type checks a description goes through, rather than a second list of rules
        /// that could drift from them: whatever the backup holds, it is only a backup if it draws.
        /// </remarks>
        private static string? Undrawable(JsonObject backup)
        {
            try
            {
                foreach (var item in UnifiedScreenDescriptionReader.Parse(backup.ToJsonString()).Items)
                {
                    UnifiedScreenItemWriter.Check(item);
                }

                return null;
            }
            catch (PortalException refused) when (refused.Code == PortalErrorCode.InvalidParams)
            {
                return refused.Message;
            }
        }

        /// <summary>Throws when the backup of a screen would not restore it.</summary>
        /// <param name="screen">The screen about to be redrawn.</param>
        /// <param name="backup">Its backup, as <see cref="UnifiedScreenReader"/> read it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming what would be lost.</exception>
        public static void RequireRestorable(HmiScreen screen, JsonObject backup)
        {
            var lost = new List<string>(UnifiedBackupCoverage.Lost(screen, $"the screen '{screen.Name}'"));

            if (UnifiedScreenReader.Dynamizations(screen).Count > 0)
            {
                lost.Add($"the screen '{screen.Name}' has tag links or animations of its own, which its backup does not keep");
            }

            foreach (var item in screen.ScreenItems)
            {
                if (!UnifiedItemTypes.IsDrawable(item.GetType().FullName!))
                {
                    lost.Add($"'{item.Name}' is a {item.GetType().Name}, which this tool does not draw back");
                }

                lost.AddRange(UnifiedBackupCoverage.Lost(item, $"'{item.Name}'", eventsKept: true));
                lost.AddRange(UnifiedEventWriter.Problems(item, item.Name));
                lost.AddRange(UnifiedAnimationReader.Problems(UnifiedScreenReader.Dynamizations(item), item.Name));
            }

            if (backup["unreadable"] is JsonArray unreadable)
            {
                lost.AddRange(unreadable.Select(entry => $"a property that could not be read ({entry})"));
            }

            if (Undrawable(backup) is string refusal)
            {
                lost.Add($"its backup would be refused when drawn back ({refusal})");
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
