using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec
{
    /// <summary>
    /// Turns an SCL template plus a cell specification into SCL source.
    /// </summary>
    /// <remarks>
    /// **The template language is deliberately not a language.** It has exactly two constructs:
    /// <list type="bullet">
    /// <item><description><c>{{name}}</c> — replaced by a value.</description></item>
    /// <item><description><c>{{#stations}} … {{/stations}}</c> and <c>{{#handovers}} … {{/handovers}}</c>
    /// — repeated once per station, or once per handover.</description></item>
    /// </list>
    ///
    /// There are no conditionals and no expressions, and that is the design rather than a stage it
    /// has not reached. The one thing a coordinator template would want a conditional for is "the
    /// last station hands over to nobody", and <see cref="CellSpecification.Handovers"/> answers that
    /// in C# by producing a list one shorter. Every other question a template might ask is a question
    /// the specification should have answered.
    ///
    /// The cost of the alternative is what makes this worth stating: a template language needs its
    /// own parser, its own error messages, its own tests, and a person debugging generated PLC code
    /// then has two languages to hold in their head instead of one.
    ///
    /// **An unreplaced placeholder is an error, not a warning.** Left alone it would reach the SCL
    /// compiler as <c>{{stationName}}</c> and come back as a syntax error pointing at generated code,
    /// which is the least useful place to be told about a typo in a template.
    /// </remarks>
    public static class SclTemplateExpander
    {
        private const string StationsRegion = "stations";
        private const string HandoversRegion = "handovers";

        private static readonly Regex Placeholder = new Regex(
            @"\{\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Expands a template for one cell.</summary>
        /// <param name="template">The template text.</param>
        /// <param name="cell">The cell to expand it for.</param>
        /// <returns>SCL source, ready for <c>WriteScl</c>.</returns>
        /// <exception cref="ArgumentException"><paramref name="template"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="cell"/> is null.</exception>
        /// <exception cref="PortalException">A region is malformed, or a placeholder has no value.</exception>
        public static string Expand(string template, CellSpecification cell)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("There is nothing to expand", nameof(template));
            }

            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell));
            }

            var expanded = ExpandRegion(template, StationsRegion, StationValues(cell));

            expanded = ExpandRegion(expanded, HandoversRegion, HandoverValues(cell));

            return Replace(expanded, CellValues(cell));
        }

        private static List<Dictionary<string, string>> StationValues(CellSpecification cell)
        {
            return cell.Stations
                .Select((station, offset) => new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["stationName"] = station.Name,
                    ["stationIndex"] = Number(offset + 1),
                    ["workSteps"] = Number(station.WorkSteps),
                    ["dwellCycles"] = Number(station.DwellCycles)
                })
                .ToList();
        }

        private static List<Dictionary<string, string>> HandoverValues(CellSpecification cell)
        {
            return cell.Handovers()
                .Select(handover => new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["fromName"] = handover.From.Name,
                    ["toName"] = handover.To.Name,
                    ["fromIndex"] = Number(handover.FromIndex),
                    ["toIndex"] = Number(handover.ToIndex)
                })
                .ToList();
        }

        private static Dictionary<string, string> CellValues(CellSpecification cell)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["cellName"] = cell.Name,
                ["stationCount"] = Number(cell.Stations.Count),
                ["firstStationName"] = cell.Stations[0].Name,
                ["lastStationName"] = cell.Stations[cell.Stations.Count - 1].Name
            };
        }

        private static string ExpandRegion(
            string template,
            string region,
            List<Dictionary<string, string>> items)
        {
            var opening = "{{#" + region + "}}";
            var closing = "{{/" + region + "}}";

            var result = template;

            // A loop rather than a regex: a region can appear more than once in one template, and
            // the coordinator uses that - stations are declared in one region and called in another.
            while (true)
            {
                var tag = result.IndexOf(opening, StringComparison.Ordinal);

                if (tag < 0)
                {
                    RequireNoOrphan(result, closing, region);

                    return result;
                }

                var closingTag = result.IndexOf(closing, tag + opening.Length, StringComparison.Ordinal);

                if (closingTag < 0)
                {
                    throw new PortalException(
                        PortalErrorCode.InvalidParams,
                        $"The template opens the '{region}' region and never closes it. Expected '{closing}'.");
                }

                // A region tag on a line of its own takes the whole line with it, including the
                // newline that ends it. Without that every region would leave a blank line behind
                // and the generated SCL would carry a scar everywhere a template repeated anything.
                var start = LineStart(result, tag);
                var bodyStart = AfterLineEnd(result, tag + opening.Length);
                var bodyEnd = LineStart(result, closingTag);
                var end = AfterLineEnd(result, closingTag + closing.Length);

                var body = result.Substring(bodyStart, Math.Max(0, bodyEnd - bodyStart));
                var expanded = new StringBuilder();

                foreach (var item in items)
                {
                    expanded.Append(Replace(body, item));
                }

                result = result.Substring(0, start) + expanded + result.Substring(end);
            }
        }

        /// <summary>Start of the line holding <paramref name="index"/>, if only spaces precede the tag.</summary>
        /// <remarks>
        /// Only when the tag is alone on its line. A region opened halfway along a line is an inline
        /// region and swallowing the text in front of it would delete something a person wrote.
        /// </remarks>
        private static int LineStart(string text, int index)
        {
            var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;

            for (var scan = lineStart; scan < index; scan++)
            {
                if (text[scan] != ' ' && text[scan] != '\t')
                {
                    return index;
                }
            }

            return lineStart;
        }

        /// <summary>Index just past the newline that ends the line, when nothing but it remains.</summary>
        private static int AfterLineEnd(string text, int index)
        {
            var scan = index;

            while (scan < text.Length && (text[scan] == ' ' || text[scan] == '\t'))
            {
                scan++;
            }

            if (scan < text.Length && text[scan] == '\r')
            {
                scan++;
            }

            if (scan < text.Length && text[scan] == '\n')
            {
                return scan + 1;
            }

            return index;
        }

        private static void RequireNoOrphan(string template, string closing, string region)
        {
            if (template.IndexOf(closing, StringComparison.Ordinal) < 0)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"The template closes the '{region}' region without opening it.");
        }

        private static string Replace(string text, Dictionary<string, string> values)
        {
            var unresolved = new List<string>();

            var replaced = Placeholder.Replace(text, match =>
            {
                var name = match.Groups["name"].Value;

                if (values.TryGetValue(name, out var value))
                {
                    return value;
                }

                unresolved.Add(name);

                return match.Value;
            });

            if (unresolved.Count > 0)
            {
                // Reported together rather than one at a time: a template with three typos should
                // take one round trip to fix, not three.
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "The template uses placeholders the specification does not define: "
                    + string.Join(", ", unresolved.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
                    + ". Known values: " + string.Join(", ", values.Keys.OrderBy(name => name, StringComparer.Ordinal)) + ".");
            }

            return replaced;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
