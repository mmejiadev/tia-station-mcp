using System.Collections.Generic;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>The Custom Web Controls to place on one WinCC Unified screen.</summary>
    public sealed class CustomWebControlPlacementList
    {
        /// <summary>Creates the list.</summary>
        /// <param name="screen">The screen's name.</param>
        /// <param name="placements">What to place, in order.</param>
        public CustomWebControlPlacementList(string screen, IReadOnlyList<CustomWebControlPlacement> placements)
        {
            Screen = screen;
            Placements = placements;
        }

        /// <summary>The screen's name.</summary>
        public string Screen { get; }

        /// <summary>What to place, in order.</summary>
        public IReadOnlyList<CustomWebControlPlacement> Placements { get; }
    }
}
