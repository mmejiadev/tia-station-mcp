using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>A screen made for a moment - a syntax check, a rehearsal - and deleted again, leaving the HMI as it was.</summary>
    /// <remarks>
    /// Measured 2026-11-18 on TIA Portal V21: on an HMI with no screen yet, the first screen created becomes the
    /// runtime's start screen. Deleting it leaves the start screen naming a screen that is gone, and the HMI then fails
    /// to compile ("The object 'TiaMcp_ModuleCheck_…' at the 'Start screen' property does not exist") - which writing a
    /// script module to a new HMI did. The start screen is put back as it was when the scratch screen goes.
    /// </remarks>
    internal sealed class UnifiedScratchScreen : IDisposable
    {
        private readonly UnifiedHmiSoftware _software;
        private readonly string _startScreen;

        private UnifiedScratchScreen(UnifiedHmiSoftware software, HmiScreen screen, string startScreen)
        {
            _software = software;
            Screen = screen;
            _startScreen = startScreen;
        }

        /// <summary>The screen.</summary>
        public HmiScreen Screen { get; }

        /// <summary>Creates a screen to delete again.</summary>
        /// <param name="software">The WinCC Unified software.</param>
        /// <param name="name">The screen's name.</param>
        /// <returns>The scratch screen; dispose it to delete it.</returns>
        public static UnifiedScratchScreen Create(UnifiedHmiSoftware software, string name)
        {
            if (software == null)
            {
                throw new ArgumentNullException(nameof(software));
            }

            var startScreen = software.RuntimeSettings.StartScreen ?? string.Empty;

            return new UnifiedScratchScreen(software, software.Screens.Create(name), startScreen);
        }

        /// <summary>Deletes the screen, and puts the start screen back when creating it changed it.</summary>
        public void Dispose()
        {
            var name = Screen.Name;
            Screen.Delete();

            if (string.Equals(_software.RuntimeSettings.StartScreen, name, StringComparison.OrdinalIgnoreCase))
            {
                _software.RuntimeSettings.StartScreen = _startScreen;
            }
        }
    }
}
