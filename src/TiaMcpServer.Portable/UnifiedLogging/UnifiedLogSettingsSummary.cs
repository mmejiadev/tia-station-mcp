using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>The change plan's line for a list of log settings: every log and every setting it changes.</summary>
    public static class UnifiedLogSettingsSummary
    {
        /// <summary>Describes a list.</summary>
        /// <param name="requests">The settings asked.</param>
        /// <returns>E.g. <c>Log_Tolva (maxSize 500, duration 30d 0h 0m 0s, backup to D:\Backup)</c>.</returns>
        public static string Of(IReadOnlyList<UnifiedLogSettingsRequest> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            return string.Join("; ", requests.Select(request => $"{request.Name} ({string.Join(", ", Parts(request))})"));
        }

        private static IEnumerable<string> Parts(UnifiedLogSettingsRequest request)
        {
            if (request.MaxSize != null)
            {
                yield return $"maxSize {request.MaxSize}";
            }

            if (request.Duration != null)
            {
                yield return $"duration {request.Duration}";
            }

            if (request.SegmentMaxSize != null)
            {
                yield return $"segment maxSize {request.SegmentMaxSize}";
            }

            if (request.SegmentDuration != null)
            {
                yield return $"segment duration {request.SegmentDuration}";
            }

            if (request.SegmentStart != null)
            {
                yield return "segment start " + request.SegmentStart.Value.ToString("s", CultureInfo.InvariantCulture);
            }

            if (request.BackupPath != null)
            {
                yield return request.BackupPath.Length == 0 ? "no backup" : $"backup to {request.BackupPath}";
            }
        }
    }
}
