using System;
using System.Collections.Generic;
using System.Globalization;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>The HMI tag types each kind of WinCC Unified alarm takes.</summary>
    /// <remarks>
    /// Measured 2026-10-25 on TIA Portal V20, one alarm of each kind on a tag of each type, compiled: a
    /// discrete alarm takes only bit strings (Bool, Byte, Word, DWord, LWord) and an analog alarm only
    /// numbers; every other pairing, a PLC-linked Int under a discrete alarm included, compiled as "The
    /// data type of the tag is not permitted for this type of alarm". Openness takes them all. A bit past
    /// the width of its tag is refused by Openness itself, as "The trigger bit number is invalid".
    /// </remarks>
    public static class UnifiedAlarmTriggerTypes
    {
        /// <summary>The largest whole number a double holds exactly, 2^53.</summary>
        private const decimal LargestExactInDouble = 9007199254740992m;

        private static readonly Dictionary<string, uint> BitWidths = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bool"] = 1,
            ["Byte"] = 8,
            ["Word"] = 16,
            ["DWord"] = 32,
            ["LWord"] = 64
        };

        /// <summary>Why a tag cannot raise a discrete alarm at a bit, or null when it can.</summary>
        /// <param name="dataType">The tag's data type, as TIA Portal spells it.</param>
        /// <param name="bit">The bit.</param>
        /// <returns>The reason, or null.</returns>
        public static string? DiscreteProblem(string dataType, uint bit)
        {
            if (!BitWidths.TryGetValue(dataType ?? string.Empty, out var width))
            {
                return $"a discrete alarm takes a Bool, Byte, Word, DWord or LWord tag, not a {dataType}";
            }

            return bit < width ? null : $"a {dataType} has bits 0 to {width - 1}, not {bit}";
        }

        /// <summary>Why an analog alarm's value does not fit its tag, or null when it does.</summary>
        /// <param name="dataType">The tag's data type, as TIA Portal spells it.</param>
        /// <param name="value">The value as given.</param>
        /// <param name="what">The alarm, as the reason names it.</param>
        /// <returns>The reason, or null.</returns>
        /// <remarks>
        /// TIA Portal keeps the value as a double (it reads back as one, measured 2026-10-25), so a whole number
        /// past 2^53, which an LInt or ULInt can hold, would be rounded without a word; it is refused instead.
        /// </remarks>
        public static string? AnalogValueProblem(string dataType, string value, string what)
        {
            var problem = UnifiedTagValues.Problem(dataType, value, what);
            if (problem != null)
            {
                return problem;
            }

            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact) && decimal.Truncate(exact) == exact && Math.Abs(exact) > LargestExactInDouble
                ? $"{what}: {value} cannot be held exactly by the double TIA Portal keeps an alarm value in; use one within 2^53"
                : null;
        }

        /// <summary>Why a tag cannot be compared by an analog alarm, or null when it can.</summary>
        /// <param name="dataType">The tag's data type, as TIA Portal spells it.</param>
        /// <returns>The reason, or null.</returns>
        /// <remarks>A bit string holds whole numbers too, but an analog alarm on one did not compile, so it is not a number here.</remarks>
        public static string? AnalogProblem(string dataType)
        {
            var given = dataType ?? string.Empty;

            return UnifiedTagValues.IsNumeric(given) && !BitWidths.ContainsKey(given) ? null : $"an analog alarm takes a number tag (an integer type, Real or LReal), not a {dataType}";
        }
    }
}
