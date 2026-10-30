using System.Collections.Generic;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>Faceplate instances to place on one WinCC Unified screen.</summary>
    public sealed class UnifiedFaceplateList
    {
        /// <summary>Creates a list.</summary>
        /// <param name="screen">The screen, which must exist.</param>
        /// <param name="faceplates">The instances, placed in this order.</param>
        public UnifiedFaceplateList(string screen, IReadOnlyList<UnifiedFaceplateDescription> faceplates)
        {
            Screen = screen;
            Faceplates = faceplates;
        }

        /// <summary>The screen.</summary>
        public string Screen { get; }

        /// <summary>The instances.</summary>
        public IReadOnlyList<UnifiedFaceplateDescription> Faceplates { get; }
    }
}
