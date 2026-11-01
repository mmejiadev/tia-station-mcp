using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Library;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>What a faceplate list is checked against: names and numbers only, read from TIA Portal once.</summary>
    public sealed class UnifiedFaceplateState
    {
        /// <summary>Creates a state.</summary>
        /// <param name="screenItems">The names of the items on the screen.</param>
        /// <param name="releasedVersions">Each project library type's released versions, by type name as TIA Portal spells it.</param>
        /// <param name="tags">The HMI's tag names, as TIA Portal spells them.</param>
        public UnifiedFaceplateState(IReadOnlyCollection<string> screenItems, IReadOnlyDictionary<string, IReadOnlyList<Version>> releasedVersions, IReadOnlyCollection<string> tags)
        {
            if (releasedVersions == null)
            {
                throw new ArgumentNullException(nameof(releasedVersions));
            }

            ScreenItems = screenItems ?? throw new ArgumentNullException(nameof(screenItems));
            ReleasedVersions = releasedVersions.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<Version>)entry.Value.Select(LibraryVersionNumber.Normalise).ToList(),
                StringComparer.OrdinalIgnoreCase);
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        }

        /// <summary>The names of the items on the screen.</summary>
        public IReadOnlyCollection<string> ScreenItems { get; }

        /// <summary>Each project library type's released versions in three parts, by type name, looked up case aside.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<Version>> ReleasedVersions { get; }

        /// <summary>The HMI's tag names.</summary>
        public IReadOnlyCollection<string> Tags { get; }
    }
}
