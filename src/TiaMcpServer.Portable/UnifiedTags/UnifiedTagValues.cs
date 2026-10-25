using System;
using System.Collections.Generic;
using System.Globalization;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The values a WinCC Unified tag of a given data type takes: start values, limits, substitute values.</summary>
    /// <remarks>
    /// Openness V20 takes any text for these and only the compile refuses it (measured 2026-10-25): "Only
    /// the values 0 and 1 are permitted" for a Bool given <c>true</c>, "Only the following characters are
    /// permitted: 0123456789eE-+." for <c>abc</c> or <c>1,5</c>, and "outside the value range" for 99999 on
    /// an Int. So they are checked here, before anything is written. Values are written with a point and
    /// no thousands separator, the only form measured.
    /// </remarks>
    public static class UnifiedTagValues
    {
        private static readonly Dictionary<string, (decimal Min, decimal Max)> Integers = new Dictionary<string, (decimal Min, decimal Max)>(StringComparer.OrdinalIgnoreCase)
        {
            ["SInt"] = (sbyte.MinValue, sbyte.MaxValue),
            ["USInt"] = (byte.MinValue, byte.MaxValue),
            ["Byte"] = (byte.MinValue, byte.MaxValue),
            ["Int"] = (short.MinValue, short.MaxValue),
            ["UInt"] = (ushort.MinValue, ushort.MaxValue),
            ["Word"] = (ushort.MinValue, ushort.MaxValue),
            ["DInt"] = (int.MinValue, int.MaxValue),
            ["UDInt"] = (uint.MinValue, uint.MaxValue),
            ["DWord"] = (uint.MinValue, uint.MaxValue),
            ["LInt"] = (long.MinValue, long.MaxValue),
            ["ULInt"] = (ulong.MinValue, ulong.MaxValue),
            ["LWord"] = (ulong.MinValue, ulong.MaxValue)
        };

        private static readonly Dictionary<string, double> Reals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Real"] = float.MaxValue,
            ["LReal"] = double.MaxValue
        };

        /// <summary>Whether a tag of this data type has limits; Bool and text tags have none (measured 2026-10-25).</summary>
        /// <param name="dataType">The data type, as TIA Portal spells it.</param>
        /// <returns>True for the integer and floating-point types.</returns>
        public static bool IsNumeric(string dataType)
        {
            return Integers.ContainsKey(dataType) || Reals.ContainsKey(dataType);
        }

        /// <summary>A value checked against a data type and written the way TIA Portal compiles it.</summary>
        /// <param name="dataType">The tag's data type, as TIA Portal spells it.</param>
        /// <param name="value">The value as given: digits for a number, 0 or 1 for a Bool, any text for a WString.</param>
        /// <param name="what">The tag and setting, as a refusal names them.</param>
        /// <returns>The value to write.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the value does not fit the type.</exception>
        public static string Constant(string dataType, string value, string what)
        {
            var problem = Problem(dataType, value, what);
            if (problem != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, problem);
            }

            return Integers.ContainsKey(dataType)
                ? decimal.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
                : value.Trim();
        }

        /// <summary>Why a value does not fit a data type, or null when it does; <see cref="Constant"/> without the exception.</summary>
        /// <param name="dataType">The tag's data type, as TIA Portal spells it.</param>
        /// <param name="value">The value as given.</param>
        /// <param name="what">The tag and setting, as the reason names them.</param>
        /// <returns>The reason, or null.</returns>
        public static string? Problem(string dataType, string value, string what)
        {
            if (dataType == null || value == null)
            {
                throw new ArgumentNullException(dataType == null ? nameof(dataType) : nameof(value));
            }

            if (Integers.TryGetValue(dataType, out var range))
            {
                return IntegerProblem(value, range, dataType, what);
            }

            if (Reals.TryGetValue(dataType, out var largest))
            {
                return RealProblem(value, largest, dataType, what);
            }

            if (dataType.Equals("Bool", StringComparison.OrdinalIgnoreCase))
            {
                return value == "0" || value == "1" ? null : $"{what}: a Bool takes 0 or 1 (true and false are given as 1 and 0); got '{value}'";
            }

            return dataType.Equals("WString", StringComparison.OrdinalIgnoreCase)
                ? null
                : $"{what}: a {dataType} tag takes no value through this tool; only numbers, Bool and WString were measured";
        }

        /// <summary>Refuses a lower limit that is not below the upper one, which the compile refuses too.</summary>
        /// <param name="lower">The lower limit, already passed through <see cref="Constant"/>.</param>
        /// <param name="upper">The upper limit, already passed through <see cref="Constant"/>.</param>
        /// <param name="what">The tag, as a refusal names it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when <paramref name="lower"/> is not below <paramref name="upper"/>.</exception>
        public static void RequireOrdered(string lower, string upper, string what)
        {
            if (IsNotBelow(lower, upper))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: the lower limit {lower} must be below the upper limit {upper}");
            }
        }

        private static string? IntegerProblem(string value, (decimal Min, decimal Max) range, string dataType, string what)
        {
            if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            {
                return $"{what}: a {dataType} takes a whole number; got '{value}'";
            }

            return number >= range.Min && number <= range.Max ? null : $"{what}: {value} is outside the range of a {dataType}, {range.Min} to {range.Max}";
        }

        private static string? RealProblem(string value, double largest, string dataType, string what)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsNaN(number) || double.IsInfinity(number))
            {
                return $"{what}: a {dataType} takes a number written with a point, e.g. 1.5 or 1e3; got '{value}'";
            }

            return Math.Abs(number) <= largest ? null : $"{what}: {value} is outside the range of a {dataType}";
        }

        /// <remarks>
        /// As decimals when both fit one, so LInt and ULInt limits that differ beyond a double's 53 bits
        /// stay apart; as doubles otherwise, for a Real written with an exponent beyond a decimal.
        /// </remarks>
        private static bool IsNotBelow(string lower, string upper)
        {
            if (decimal.TryParse(lower, NumberStyles.Float, CultureInfo.InvariantCulture, out var exactLower)
                && decimal.TryParse(upper, NumberStyles.Float, CultureInfo.InvariantCulture, out var exactUpper))
            {
                return exactLower >= exactUpper;
            }

            return double.Parse(lower, NumberStyles.Float, CultureInfo.InvariantCulture) >= double.Parse(upper, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
