using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedScreens;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Tries a description's scripts and animations on a throw-away screen before the real one is touched.</summary>
    /// <remarks>
    /// Some refusals only TIA Portal can give: a script it finds broken, a property that takes no
    /// animation. Drawing deletes a screen's items first, so finding those out while drawing would leave
    /// half a screen; finding them out here leaves nothing changed. The screen is made for the purpose and
    /// deleted again whatever happens. Scripts resolve the screens they name (measured 2026-10-25: "The
    /// object HmiScreen with the name 'Drawn' was not found"), so when the screen being drawn does not
    /// exist yet, the rehearsal is held under that very name, and a script that changes to it checks as it
    /// will run.
    /// </remarks>
    internal static class UnifiedRehearsal
    {
        private const string ScratchScreenPrefix = "TiaMcp_SyntaxCheck_";

        /// <summary>Rehearses a description, and refuses it when TIA Portal would refuse any part of it.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="screenName">The screen about to be drawn.</param>
        /// <param name="items">The items, already passed through the checks on their types.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming each refusal; nothing was drawn.</exception>
        public static void Run(UnifiedHmiSoftware hmi, string screenName, IReadOnlyList<UnifiedItemDescription> items)
        {
            if (!items.Any(item => item.Events.Count > 0 || item.Animations.Count > 0))
            {
                return;
            }

            var scratch = hmi.Screens.Create(hmi.Screens.Find(screenName) == null ? screenName : ScratchScreenPrefix + Guid.NewGuid().ToString("N"));
            try
            {
                var refused = UnifiedEventWriter.CheckScripts(scratch, items).Concat(UnifiedAnimationWriter.Rehearse(scratch, items)).ToList();

                if (refused.Count > 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"Nothing was drawn: {string.Join("; ", refused)}");
                }
            }
            finally
            {
                scratch.Delete();
            }
        }
    }
}
