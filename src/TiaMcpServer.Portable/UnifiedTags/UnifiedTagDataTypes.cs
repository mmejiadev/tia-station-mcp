using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The data types an internal WinCC Unified HMI tag takes.</summary>
    /// <remarks>
    /// Measured 2026-10-22 on TIA Portal V20, one internal tag per type on an MTP700: these were taken
    /// and read back as written. Char, String, Date, Time, Date_And_Time, DTL and the .NET spellings
    /// (Boolean, Integer, Float, Double) were refused with "Empty data type or HMI data type", so they
    /// are refused here, before a tag exists. TIA Portal takes the spelling in any case.
    /// Arrays measured 2026-10-25: one dimension from 0 (<c>Array [0..3] of Int</c>, spaced that way on
    /// read-back); a lower bound other than 0 and a second dimension are refused by TIA Portal, and an
    /// internal tag cannot be a structure.
    /// </remarks>
    public static class UnifiedTagDataTypes
    {
        private static readonly string[] Measured =
        {
            "Bool", "Byte", "SInt", "USInt", "Int", "UInt", "DInt", "UDInt", "LInt", "ULInt", "Real", "LReal",
            "Word", "DWord", "LWord", "WChar", "WString", "DateTime", "LTime"
        };

        private static readonly Regex ArrayType = new Regex(@"^\s*Array\s*\[(?<bounds>[^\]]*)\]\s*of\s+(?<element>\S+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // ASCII digits only, and few enough of them to fit a long: \d would take any Unicode digit.
        private static readonly Regex Bounds = new Regex(@"^\s*(?<lower>-?[0-9]{1,18})\s*\.\.\s*(?<upper>-?[0-9]{1,18})\s*$", RegexOptions.CultureInvariant);

        /// <summary>Every data type an internal tag takes.</summary>
        public static IReadOnlyList<string> All => Measured;

        /// <summary>The data type as TIA Portal spells it.</summary>
        /// <param name="dataType">The data type, in any case.</param>
        /// <param name="what">The tag, as a refusal names it.</param>
        /// <returns>The measured spelling, e.g. <c>Bool</c> for <c>bool</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> listing the types there are.</exception>
        public static string Normalise(string? dataType, string what)
        {
            var given = (dataType ?? string.Empty).Trim();

            return Measured.FirstOrDefault(type => type.Equals(given, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"{what}: '{given}' is not a data type an internal Unified tag takes. Use one of: {string.Join(", ", Measured)}, or Array [0..n] of one of them");
        }

        /// <summary>An internal tag's data type, a single value or an array of them, as TIA Portal spells it.</summary>
        /// <param name="dataType">The data type, e.g. <c>int</c> or <c>Array[0..3] of Int</c>.</param>
        /// <param name="what">The tag, as a refusal names it.</param>
        /// <returns>The measured spelling, e.g. <c>Array [0..3] of Int</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a type or array shape TIA Portal does not take.</exception>
        public static string NormaliseInternal(string? dataType, string what)
        {
            var array = ArrayType.Match(dataType ?? string.Empty);

            return array.Success ? NormaliseArray(array, what) : Normalise(dataType, what);
        }

        /// <summary>Whether a data type, as TIA Portal spells it, is an array.</summary>
        /// <param name="dataType">The data type.</param>
        /// <returns>True for <c>Array [..] of ..</c>.</returns>
        public static bool IsArray(string dataType)
        {
            return ArrayType.IsMatch(dataType);
        }

        private static string NormaliseArray(Match array, string what)
        {
            var bounds = Bounds.Match(array.Groups["bounds"].Value);
            if (!bounds.Success)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: an array takes one dimension written [0..n]; TIA Portal refuses '[{array.Groups["bounds"].Value}]'");
            }

            var lower = long.Parse(bounds.Groups["lower"].Value, CultureInfo.InvariantCulture);
            var upper = long.Parse(bounds.Groups["upper"].Value, CultureInfo.InvariantCulture);
            if (lower != 0 || upper < 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: an array starts at 0 and ends at 0 or higher, as TIA Portal requires; got [{lower}..{upper}]");
            }

            return $"Array [0..{upper.ToString(CultureInfo.InvariantCulture)}] of {Normalise(array.Groups["element"].Value, what)}";
        }
    }
}
