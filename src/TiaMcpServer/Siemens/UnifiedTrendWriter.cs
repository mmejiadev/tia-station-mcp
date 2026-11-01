using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTrends;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Writes trends into a WinCC Unified trend control's first trend area, all of a list or none of it.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: a new trend control holds one area with one trend whose source is
    /// empty, a bottom time axis spanning 1 minute and a left value axis. Trends already there and empty are
    /// filled, the rest created (<c>Trends.Create()</c>). An undo deletes the created ones and replaces each
    /// filled one by a new empty trend, each its own step so one refusal does not stop the others.
    /// </remarks>
    internal sealed class UnifiedTrendWriter
    {
        private readonly HmiTrendControl _control;
        private readonly UnifiedTrendList _list;
        private readonly Language _language;
        private readonly UnifiedTrendRules _rules;

        /// <summary>Creates a writer for one trend control, refusing a list that cannot be written as it stands.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="control">The trend control.</param>
        /// <param name="list">The trends.</param>
        /// <param name="language">The project's editing language, which a trend's name is written in.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when the control already shows a trend, has no trend area or its time
        /// axis cannot take the range; <see cref="PortalErrorCode.InvalidParams"/> when a tag or logging tag does not exist.
        /// </exception>
        /// <remarks>Only the tags the list names are read: each is a call into TIA Portal while the Openness gate is held.</remarks>
        public UnifiedTrendWriter(UnifiedHmiSoftware hmi, HmiTrendControl control, UnifiedTrendList list, Language language)
        {
            _control = control;
            _list = list;
            _language = language;

            UnifiedTrendRules.RequireNoTrendSet(control.Name, control.TrendAreas.SelectMany(area => area.Trends).Select(trend => trend.DataSourceY.Source ?? string.Empty).ToList());
            RequireArea();
            RequireRangeCanBeSet();

            _rules = new UnifiedTrendRules(ReadLoggingTags(hmi, list));
            _rules.Check(list);
        }

        private HmiTrendAreaPart Area => _control.TrendAreas[0];

        /// <summary>Writes the trends and the time range; puts the control back as it was when one is refused.</summary>
        /// <returns>The control's trends and time range, read back.</returns>
        public JsonObject Write()
        {
            var made = new List<(string Name, Action Delete)>();

            try
            {
                for (var index = 0; index < _list.Trends.Count; index++)
                {
                    WriteTrend(index, made);
                }

                WriteRange(made);
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw UnifiedRollback.Undo(made, failure, "trends");
            }

            return UnifiedTrendReader.Describe(_control, _language);
        }

        private static Dictionary<string, IReadOnlyList<string>> ReadLoggingTags(UnifiedHmiSoftware hmi, UnifiedTrendList list)
        {
            var tags = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var found in list.Trends.Select(trend => trend.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Select(hmi.Tags.Find).Where(tag => tag != null))
            {
                tags[found.Name] = found.LoggingTags.Select(logged => logged.Name).ToList();
            }

            return tags;
        }

        private void RequireArea()
        {
            if (_control.TrendAreas.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{_control.Name}' has no trend area; draw the trend control again with DrawUnifiedScreen");
            }
        }

        /// <remarks>A range is refused rather than dropped: the control may have its time axis on top, or one not set by time.</remarks>
        private void RequireRangeCanBeSet()
        {
            if (_list.TimeRange == null)
            {
                return;
            }

            if (Area.BottomTimeAxes.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{_control.Name}' has no bottom time axis to take a time range; leave 'timeRange' out or add the axis in TIA Portal");
            }

            if (Area.BottomTimeAxes[0].RangeType != HmiTimeRangeType.TimeRange)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The time axis of '{_control.Name}' is set by {Area.BottomTimeAxes[0].RangeType}, not by a time range; leave 'timeRange' out or change it in TIA Portal");
            }
        }

        private void WriteTrend(int index, List<(string Name, Action Delete)> made)
        {
            var trend = _list.Trends[index];
            var what = $"trend {index + 1} ({_rules.SourceOf(trend)})";

            if (index >= Area.Trends.Count)
            {
                UnifiedRollback.Create(what, made, () => Area.Trends.Create(), (part => part.Delete, part => Fill(part, trend)));
                return;
            }

            var existing = Area.Trends[index];
            made.Add(($"trend {index + 1}, to be emptied again", ReplacementOf(existing)));
            UnifiedRollback.Create(what, new List<(string Name, Action Delete)>(), () => existing, (_ => () => { }, part => Fill(part, trend)));
        }

        /// <summary>The undo of filling an empty trend: a new trend in its place, with the look it had.</summary>
        /// <remarks>
        /// Measured 2026-11-01 on TIA Portal V20: a source cannot be emptied, as <c>""</c> and null are both refused
        /// ("Empty string is an invalid PropertyValue"), while a trend deleted and created anew has an empty source
        /// and the same defaults, and an area may hold no trend meanwhile. Every trend filled here was empty, so
        /// which of them ends where among the others does not change what the control shows.
        /// </remarks>
        private Action ReplacementOf(HmiTrendPart part)
        {
            var color = part.LineColor;
            var mode = part.TrendMode;
            var name = part.DisplayName.Items.Find(_language)?.Text;

            return () =>
            {
                part.Delete();
                var replacement = Area.Trends.Create();
                replacement.LineColor = color;
                replacement.TrendMode = mode;
                PutNameBack(replacement, name);
            };
        }

        private void PutNameBack(HmiTrendPart part, string? name)
        {
            var item = part.DisplayName.Items.Find(_language);

            if (item != null && name != null && item.Text != name)
            {
                item.Text = name;
            }
        }

        private void Fill(HmiTrendPart part, UnifiedTrendDescription trend)
        {
            part.DataSourceY.Source = _rules.SourceOf(trend);

            if (trend.Color != null)
            {
                part.LineColor = trend.Color.Value;
            }

            if (trend.Mode != null)
            {
                part.TrendMode = (HmiTrendMode)Enum.Parse(typeof(HmiTrendMode), trend.Mode);
            }

            if (trend.Name != null)
            {
                var item = part.DisplayName.Items.Find(_language)
                    ?? throw new PortalException(PortalErrorCode.InvalidState, $"The trend of '{trend.Tag}' has no name entry for the project's editing language {_language.Culture.Name}");

                item.Text = UnifiedRichText.Format(trend.Name);
            }
        }

        private void WriteRange(List<(string Name, Action Delete)> made)
        {
            if (_list.TimeRange == null)
            {
                return;
            }

            var axis = Area.BottomTimeAxes[0];
            var (factor, unit) = (axis.TimeRangeFactor, axis.TimeRangeBase);

            made.Add(("the time range, to be put back", () =>
            {
                axis.TimeRangeBase = unit;
                axis.TimeRangeFactor = factor;
            }));

            axis.TimeRangeBase = (HmiTimeRangeBase)Enum.Parse(typeof(HmiTimeRangeBase), _list.TimeRange.Value.Base);
            axis.TimeRangeFactor = _list.TimeRange.Value.Factor;
        }
    }
}
