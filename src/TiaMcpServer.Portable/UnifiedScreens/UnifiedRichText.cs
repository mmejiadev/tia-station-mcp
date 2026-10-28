using System;
using System.Net;
using System.Text.RegularExpressions;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Turns plain text into the formatted text a TIA Portal text holds, and back.</summary>
    /// <remarks>
    /// A multilingual text is stored as XHTML: every exported text of the class projects reads
    /// <c>&lt;body&gt;&lt;p&gt;TANCAR&lt;/p&gt;&lt;/body&gt;</c>, and on 2026-10-21 WinCC Unified refused the
    /// plain text <c>Tank 1</c> as "an invalid format". A plain text is wrapped and escaped; a text that
    /// already starts with <c>&lt;body&gt;</c> is formatted by its author and goes in unchanged.
    /// </remarks>
    public static class UnifiedRichText
    {
        private const string FormattedStart = "<body>";

        /// <summary>The formatted text TIA Portal holds for a text nobody has written, e.g. a new alarm's info text.</summary>
        public const string Empty = "<body><p/></body>";

        private static readonly Regex OnePlainParagraph = new Regex(@"^<body><p>([^<]*)</p></body>$", RegexOptions.CultureInvariant);

        /// <summary>The formatted text to store.</summary>
        /// <param name="text">A plain text, or one already formatted as <c>&lt;body&gt;…</c>.</param>
        /// <returns>The formatted text.</returns>
        public static string Format(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return text.StartsWith(FormattedStart, StringComparison.Ordinal)
                ? text
                : $"{FormattedStart}<p>{WebUtility.HtmlEncode(text)}</p></body>";
        }

        /// <summary>The text to show: plain when it is one plain paragraph, otherwise as stored.</summary>
        /// <param name="stored">The formatted text TIA Portal holds.</param>
        /// <returns>The text, which <see cref="Format"/> turns back into <paramref name="stored"/>.</returns>
        public static string Read(string stored)
        {
            if (stored == null)
            {
                throw new ArgumentNullException(nameof(stored));
            }

            var match = OnePlainParagraph.Match(stored);
            var plain = match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : stored;

            // A plain text that itself starts with "<body>" would be taken as formatted when written back.
            return plain.StartsWith(FormattedStart, StringComparison.Ordinal) && match.Success ? stored : plain;
        }
    }
}
