using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTrends
{
    /// <summary>What must hold before trends are written into a trend control, checked without TIA Portal.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: a trend's source is <c>Tag</c> for live values and
    /// <c>Tag:LoggingTag</c> for logged ones. Openness refuses a <c>Tag:...</c> whose tag does not exist, but a
    /// bare name it does not know was taken; so the tag and the logging tag are both checked here. Only a control
    /// whose trends have no source yet, as DrawUnifiedScreen draws one, is filled: trends already set are a
    /// person's work, and replacing them is not undone exactly.
    /// </remarks>
    public sealed class UnifiedTrendRules
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _loggingTags;

        /// <summary>Creates the rules over what TIA Portal holds.</summary>
        /// <param name="loggingTags">Each HMI tag's logging tags, by tag name as TIA Portal spells it.</param>
        public UnifiedTrendRules(IReadOnlyDictionary<string, IReadOnlyList<string>> loggingTags)
        {
            if (loggingTags == null)
            {
                throw new ArgumentNullException(nameof(loggingTags));
            }

            _loggingTags = loggingTags.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Throws unless every trend can be written as described.</summary>
        /// <param name="list">The trends.</param>
        /// <param name="existingSources">The sources of the trends the control's first area holds now.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when a tag or logging tag does not exist;
        /// <see cref="PortalErrorCode.InvalidState"/> when the control already shows a trend.
        /// </exception>
        public void Check(UnifiedTrendList list, IReadOnlyCollection<string> existingSources)
        {
            if (list == null || existingSources == null)
            {
                throw new ArgumentNullException(list == null ? nameof(list) : nameof(existingSources));
            }

            var set = existingSources.Where(source => !string.IsNullOrEmpty(source)).ToList();
            if (set.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was written: '{list.Control}' already shows {string.Join(", ", set)}. This tool only fills a trend control with no trend set yet, such as one DrawUnifiedScreen has just drawn.");
            }

            var missing = list.Trends.Select(Problem).Where(problem => problem != null).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Nothing was written: {string.Join("; ", missing)}");
            }
        }

        /// <summary>The source to write, with the tag and logging tag as TIA Portal spells them.</summary>
        /// <param name="trend">A trend checked by <see cref="Check"/>.</param>
        /// <returns><c>Level</c> or <c>Level:Level_log</c>.</returns>
        public string SourceOf(UnifiedTrendDescription trend)
        {
            if (trend == null)
            {
                throw new ArgumentNullException(nameof(trend));
            }

            var tag = _loggingTags.Keys.First(name => string.Equals(name, trend.Tag, StringComparison.OrdinalIgnoreCase));

            return trend.LoggingTag == null
                ? tag
                : $"{tag}:{_loggingTags[tag].First(name => string.Equals(name, trend.LoggingTag, StringComparison.OrdinalIgnoreCase))}";
        }

        private string? Problem(UnifiedTrendDescription trend)
        {
            if (!_loggingTags.TryGetValue(trend.Tag, out var logged))
            {
                return $"the HMI tag '{trend.Tag}' does not exist";
            }

            if (trend.LoggingTag != null && !logged.Contains(trend.LoggingTag, StringComparer.OrdinalIgnoreCase))
            {
                return logged.Count == 0
                    ? $"'{trend.Tag}' has no logging tag; CreateUnifiedLogging makes one"
                    : $"'{trend.Tag}' has no logging tag '{trend.LoggingTag}'; it has: {string.Join(", ", logged)}";
            }

            return null;
        }
    }
}
