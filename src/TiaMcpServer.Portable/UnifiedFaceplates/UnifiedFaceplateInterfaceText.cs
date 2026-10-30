using System.Text.RegularExpressions;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>Reads a faceplate interface value back in the form it was written.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: a text property given <c>Motor 1</c> reads back as a whole
    /// document, <c>&lt;?xml …?&gt;&lt;ProjectText …&gt;&lt;body&gt;&lt;p&gt;Motor 1&lt;/p&gt;&lt;/body&gt;&lt;/ProjectText&gt;</c>,
    /// and an empty one as the same with an empty paragraph. A tag property reads back as the tag's name.
    /// </remarks>
    public static class UnifiedFaceplateInterfaceText
    {
        private static readonly Regex Body = new Regex(@"\A<\?xml[^>]*\?>\s*<ProjectText[^>]*>(<body>.*</body>)</ProjectText>\s*\z", RegexOptions.CultureInvariant | RegexOptions.Singleline);

        /// <summary>The value as text: the plain text of a one-paragraph document, otherwise the value as stored.</summary>
        /// <param name="stored">The value TIA Portal holds, or null.</param>
        /// <returns>The text, empty for null.</returns>
        public static string Read(string? stored)
        {
            var value = stored ?? string.Empty;
            var match = Body.Match(value);

            return match.Success ? UnifiedRichText.Read(match.Groups[1].Value) : value;
        }
    }
}
