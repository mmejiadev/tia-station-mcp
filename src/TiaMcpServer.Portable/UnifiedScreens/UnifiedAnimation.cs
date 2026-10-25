using System;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>One animated property of a WinCC Unified item: how its value follows the process.</summary>
    /// <remarks>
    /// Openness calls these dynamizations, and a property holds one at most: creating a second replaces
    /// the first without a word (measured 2026-10-25 on TIA Portal V20), so a description animates each
    /// property once, and never one it also links through <c>tags</c>.
    /// </remarks>
    public abstract class UnifiedAnimation
    {
        /// <summary>Creates an animation of a property.</summary>
        /// <param name="path">The property's path, already normalised, e.g. <c>BackColor</c>.</param>
        protected UnifiedAnimation(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Path = path;
            Segments = path.Split('.');
        }

        /// <summary>The property's path.</summary>
        public string Path { get; }

        /// <summary>The path's segments, the property last.</summary>
        public IReadOnlyList<string> Segments { get; }

        /// <summary>The HMI tags the animation reads, which must exist before anything is drawn.</summary>
        public virtual IReadOnlyList<string> LinkedTags => Array.Empty<string>();
    }
}
