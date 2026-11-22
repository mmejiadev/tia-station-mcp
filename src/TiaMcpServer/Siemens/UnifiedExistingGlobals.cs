using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads the global definitions a Unified screen already holds, for what is drawn on it to be written with them.</summary>
    /// <remarks>
    /// The screen's one area per context (see <c>UnifiedScreenGlobals</c>), as any item holding a script reads it: an item
    /// drawn writes the whole area, so what is there already goes in with it.
    /// </remarks>
    internal static class UnifiedExistingGlobals
    {
        /// <summary>The definitions of the screen's events and of its script animations.</summary>
        /// <param name="screen">The screen.</param>
        /// <returns>Each area; null for one that holds nothing.</returns>
        public static (string? Events, string? Animations) Of(HmiScreen screen)
        {
            var events = UnifiedEventWriter.ScreenGlobals(screen, withItems: true);
            var animations = screen.ScreenItems
                .SelectMany(item => UnifiedScreenReader.Dynamizations(item).OfType<ScriptDynamization>())
                .Select(UnifiedAnimationReader.GlobalsOf)
                .FirstOrDefault(globals => globals != null);

            return (events, animations);
        }
    }
}
