using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The lower and upper limits of a numeric WinCC Unified tag.</summary>
    /// <remarks>
    /// Measured 2026-10-25: Bool and text tags have none (a disabled field), and Openness takes a crossed
    /// pair, a value outside the type and a limit tag that does not exist, all of which only the compile
    /// refuses; so all three are checked before writing.
    /// </remarks>
    public sealed class UnifiedTagLimits
    {
        /// <summary>No limits.</summary>
        public static readonly UnifiedTagLimits None = new UnifiedTagLimits(null, null);

        /// <summary>Creates the limits; null leaves that side without one.</summary>
        /// <param name="lower">The lower limit.</param>
        /// <param name="upper">The upper limit.</param>
        public UnifiedTagLimits(UnifiedTagLimit? lower, UnifiedTagLimit? upper)
        {
            Lower = lower;
            Upper = upper;
        }

        /// <summary>The lower limit, or null.</summary>
        public UnifiedTagLimit? Lower { get; }

        /// <summary>The upper limit, or null.</summary>
        public UnifiedTagLimit? Upper { get; }

        /// <summary>Whether either side has a limit.</summary>
        public bool IsSet => Lower != null || Upper != null;

        /// <summary>The names of the HMI tags the limits are held by; empty when both are constants or unset.</summary>
        public IReadOnlyList<string> TagNames => new[] { Lower, Upper }.Where(limit => limit is { IsTag: true }).Select(limit => limit!.Value).ToList();
    }
}
