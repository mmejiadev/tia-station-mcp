using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Library
{
    /// <summary>Compares what two versions of a library type hold, document by document.</summary>
    /// <remarks>
    /// Measured 2026-11-24 on TIA Portal V21: <c>LibraryTypeVersion.CompareTo</c> compares only what describes a version - its
    /// GUID, number, default, author, comment, time and library - and nothing of its content. The content is compared here from
    /// each version exported as SIMATIC SD documents (<c>ExportAsDocuments</c> with the format <c>SimaticSD</c>), which TIA Portal
    /// writes for a block type and a PLC data type alike.
    /// </remarks>
    public static class LibraryDocumentComparer
    {
        /// <summary>Compares two versions' documents by file name.</summary>
        /// <param name="left">The first version's documents, by file name.</param>
        /// <param name="right">The second version's documents, by file name.</param>
        /// <returns>One entry per file name either side holds, in name order.</returns>
        /// <remarks>
        /// Line endings and blanks at the end of a line or of the text are set aside: they are how a file was written, not what
        /// the version holds. Anything else, comments included, counts.
        /// </remarks>
        public static IReadOnlyList<DocumentComparison> Compare(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
        {
            if (left == null || right == null)
            {
                throw new ArgumentNullException(left == null ? nameof(left) : nameof(right));
            }

            return left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => CompareOne(name, Find(left, name), Find(right, name)))
                .ToList();
        }

        /// <summary>Whether every document compared is identical.</summary>
        /// <param name="comparisons">The comparisons.</param>
        /// <returns>True when none differs and none is on one side only.</returns>
        public static bool AreIdentical(IEnumerable<DocumentComparison> comparisons)
        {
            return comparisons.All(comparison => comparison.State == DocumentComparisonState.Identical);
        }

        private static DocumentComparison CompareOne(string name, string? left, string? right)
        {
            if (left == null)
            {
                return new DocumentComparison(name, DocumentComparisonState.OnlyRight, 0);
            }

            if (right == null)
            {
                return new DocumentComparison(name, DocumentComparisonState.OnlyLeft, 0);
            }

            var line = FirstDifferentLine(Lines(left), Lines(right));

            return new DocumentComparison(name, line == 0 ? DocumentComparisonState.Identical : DocumentComparisonState.Different, line);
        }

        private static int FirstDifferentLine(List<string> left, List<string> right)
        {
            var length = Math.Max(left.Count, right.Count);

            for (var index = 0; index < length; index++)
            {
                var leftLine = index < left.Count ? left[index] : null;
                var rightLine = index < right.Count ? right[index] : null;
                if (!string.Equals(leftLine, rightLine, StringComparison.Ordinal))
                {
                    return index + 1;
                }
            }

            return 0;
        }

        private static List<string> Lines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd().Split('\n').Select(line => line.TrimEnd()).ToList();
        }

        private static string? Find(IReadOnlyDictionary<string, string> documents, string name)
        {
            return documents.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
        }
    }
}
