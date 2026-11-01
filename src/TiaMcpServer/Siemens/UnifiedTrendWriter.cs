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
    /// empty, a bottom time axis spanning 1 minute and a left value axis. The first trend is filled; the others
    /// are created (<c>Trends.Create()</c>, nameless) and deleted again by an undo. The name a legend shows is
    /// a formatted text in each language the project has.
    /// </remarks>
    internal sealed class UnifiedTrendWriter
    {
        private readonly HmiTrendControl _control;
        private readonly UnifiedTrendList _list;
        private readonly UnifiedTrendRules _rules;

        /// <summary>Creates a writer for one trend control, reading the HMI's tags and logging tags once.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="control">The trend control.</param>
        /// <param name="list">The trends.</param>
        public UnifiedTrendWriter(UnifiedHmiSoftware hmi, HmiTrendControl control, UnifiedTrendList list)
        {
            _control = control;
            _list = list;
            _rules = new UnifiedTrendRules(hmi.Tags.ToDictionary(tag => tag.Name, tag => (IReadOnlyList<string>)tag.LoggingTags.Select(logged => logged.Name).ToList(), StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>Refuses a list that cannot be written as it stands, before anything is changed.</summary>
        public void Check()
        {
            if (_control.TrendAreas.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{_control.Name}' has no trend area; draw the trend control again with DrawUnifiedScreen");
            }

            _rules.Check(_list, Area.Trends.Select(trend => trend.DataSourceY.Source ?? string.Empty).ToList());
        }

        /// <summary>Writes the trends and the time range; puts the control back as it was when one is refused.</summary>
        /// <returns>The control's trends and time range, read back.</returns>
        public JsonObject Write()
        {
            var before = (Trends: Area.Trends.Count, First: Snapshot(Area.Trends[0]), Range: Range());
            var made = new List<(string Name, Action Delete)>();

            try
            {
                for (var index = 0; index < _list.Trends.Count; index++)
                {
                    WriteTrend(index, made);
                }

                WriteRange();
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                made.Insert(0, ("the first trend, to be put back", () => PutBack(before.First, before.Range)));
                throw UnifiedRollback.Undo(made, failure, "trends");
            }

            return UnifiedTrendReader.Describe(_control);
        }

        private HmiTrendAreaPart Area => _control.TrendAreas[0];

        private void WriteTrend(int index, List<(string Name, Action Delete)> made)
        {
            var trend = _list.Trends[index];
            var what = $"trend {index + 1} ({_rules.SourceOf(trend)})";

            if (index < Area.Trends.Count)
            {
                UnifiedRollback.Create(what, new List<(string Name, Action Delete)>(), () => Area.Trends[index], (_ => () => { }, part => Fill(part, trend)));
                return;
            }

            UnifiedRollback.Create(what, made, () => Area.Trends.Create(), (part => part.Delete, part => Fill(part, trend)));
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
                foreach (var item in part.DisplayName.Items)
                {
                    item.Text = UnifiedRichText.Format(trend.Name);
                }
            }
        }

        private void WriteRange()
        {
            if (_list.TimeRange == null || Area.BottomTimeAxes.Count == 0)
            {
                return;
            }

            var axis = Area.BottomTimeAxes[0];
            axis.TimeRangeBase = (HmiTimeRangeBase)Enum.Parse(typeof(HmiTimeRangeBase), _list.TimeRange.Value.Base);
            axis.TimeRangeFactor = _list.TimeRange.Value.Factor;
        }

        private (int Factor, HmiTimeRangeBase Base)? Range()
        {
            return Area.BottomTimeAxes.Count == 0 ? ((int, HmiTimeRangeBase)?)null : (Area.BottomTimeAxes[0].TimeRangeFactor, Area.BottomTimeAxes[0].TimeRangeBase);
        }

        private static (string Source, System.Drawing.Color Color, HmiTrendMode Mode, string[] Names) Snapshot(HmiTrendPart part)
        {
            return (part.DataSourceY.Source ?? string.Empty, part.LineColor, part.TrendMode, part.DisplayName.Items.Select(item => item.Text).ToArray());
        }

        /// <remarks>An empty source is written back as the empty string it was read as; the names go back item by item.</remarks>
        private void PutBack((string Source, System.Drawing.Color Color, HmiTrendMode Mode, string[] Names) first, (int Factor, HmiTimeRangeBase Base)? range)
        {
            var part = Area.Trends[0];
            part.DataSourceY.Source = first.Source;
            part.LineColor = first.Color;
            part.TrendMode = first.Mode;

            var items = part.DisplayName.Items.ToList();
            for (var index = 0; index < items.Count && index < first.Names.Length; index++)
            {
                items[index].Text = first.Names[index];
            }

            if (range != null)
            {
                Area.BottomTimeAxes[0].TimeRangeFactor = range.Value.Factor;
                Area.BottomTimeAxes[0].TimeRangeBase = range.Value.Base;
            }
        }
    }
}
