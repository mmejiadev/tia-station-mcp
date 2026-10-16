using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Hmi
{
    /// <summary>Reads a colour as a person or an agent writes it, and spells it as TIA Portal does.</summary>
    /// <remarks>
    /// TIA Portal writes colours as <c>R, G, B</c>. A description may also use <c>#RRGGBB</c> or one
    /// of a few names, because "green" is how a pilot light is specified in a class exercise.
    /// </remarks>
    public static class HmiColor
    {
        private const int ChannelCount = 3;
        private const int HexLength = 7;
        private const int MaxChannel = 255;
        private const int RedShift = 16;
        private const int GreenShift = 8;

        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = "0, 0, 0",
            ["white"] = "255, 255, 255",
            ["red"] = "255, 0, 0",
            ["green"] = "0, 255, 0",
            ["blue"] = "0, 0, 255",
            ["yellow"] = "255, 255, 0",
            ["orange"] = "255, 165, 0",
            ["grey"] = "128, 128, 128",
            ["gray"] = "128, 128, 128"
        };

        /// <summary>Reads a colour.</summary>
        /// <param name="text"><c>R, G, B</c>, <c>#RRGGBB</c> or a name such as <c>green</c>.</param>
        /// <param name="what">What the colour is for, named in the refusal.</param>
        /// <returns>The colour as <c>R, G, B</c>.</returns>
        /// <exception cref="PortalException">The text is not a colour.</exception>
        public static string Parse(string text, string what)
        {
            var trimmed = (text ?? string.Empty).Trim();

            if (Named.TryGetValue(trimmed, out var named))
            {
                return named;
            }

            var channels = trimmed.StartsWith("#", StringComparison.Ordinal) ? ParseHex(trimmed) : ParseTriple(trimmed);

            return channels != null
                ? string.Join(", ", channels.Select(channel => channel.ToString(CultureInfo.InvariantCulture)))
                : throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"{what}: '{text}' is not a colour. Write 'R, G, B' (0-255 each), '#RRGGBB', or one of: {string.Join(", ", Named.Keys)}");
        }

        private static int[]? ParseTriple(string text)
        {
            var parts = text.Split(',');

            if (parts.Length != ChannelCount)
            {
                return null;
            }

            var channels = parts.Select(part => int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : -1).ToArray();

            return channels.All(channel => channel >= 0 && channel <= MaxChannel) ? channels : null;
        }

        private static int[]? ParseHex(string text)
        {
            if (text.Length != HexLength || !int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                return null;
            }

            return new[] { (rgb >> RedShift) & MaxChannel, (rgb >> GreenShift) & MaxChannel, rgb & MaxChannel };
        }
    }
}
