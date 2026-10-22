using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Turns a description's JSON value into the value an Openness property takes, and back.</summary>
    /// <remarks>
    /// The property types are the ones every WinCC Unified screen item uses, counted from
    /// <c>Siemens.Engineering.dll</c> V20 on 2026-10-21: booleans, strings, colours, the integer and
    /// floating-point types, and enums. Anything else is refused with its type named, never converted by
    /// guess: an integer that does not fit its property is a mistake to report, not a value to clip.
    /// Texts (<c>MultilingualText</c>) and parts are not values and are handled by the writer.
    /// </remarks>
    public static class UnifiedPropertyConverter
    {
        private const int MaxNamesListed = 40;

        private static readonly Regex WithAlpha = new Regex(@"^\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<Type, (decimal Minimum, decimal Maximum)> Integers = new Dictionary<Type, (decimal, decimal)>
        {
            [typeof(byte)] = (byte.MinValue, byte.MaxValue),
            [typeof(sbyte)] = (sbyte.MinValue, sbyte.MaxValue),
            [typeof(short)] = (short.MinValue, short.MaxValue),
            [typeof(ushort)] = (ushort.MinValue, ushort.MaxValue),
            [typeof(int)] = (int.MinValue, int.MaxValue),
            [typeof(uint)] = (uint.MinValue, uint.MaxValue),
            [typeof(long)] = (long.MinValue, long.MaxValue),
            [typeof(ulong)] = (ulong.MinValue, ulong.MaxValue)
        };

        /// <summary>Whether a property of this type can be set from a description value.</summary>
        /// <param name="type">The property's type.</param>
        /// <returns>True for the types <see cref="Convert"/> takes.</returns>
        public static bool Supports(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type == typeof(bool) || type == typeof(string) || type == typeof(Color) || type == typeof(float)
                || type == typeof(double) || Integers.ContainsKey(type) || type.IsEnum;
        }

        /// <summary>Converts a value for a property.</summary>
        /// <param name="type">The property's type.</param>
        /// <param name="value">The value as the description wrote it.</param>
        /// <param name="what">The property, named in a refusal.</param>
        /// <returns>The value, of <paramref name="type"/>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the value does not fit the property.</exception>
        public static object Convert(Type type, JsonElement value, string what)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type == typeof(bool))
            {
                return ToBoolean(value, what);
            }

            if (type == typeof(string))
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Refuse(what, "a string", value);
            }

            if (type == typeof(Color))
            {
                return ToColor(value, what);
            }

            if (type == typeof(float) || type == typeof(double))
            {
                return ToFloatingPoint(type, value, what);
            }

            if (Integers.TryGetValue(type, out var range))
            {
                return ToInteger(type, range, value, what);
            }

            return type.IsEnum
                ? ToEnum(type, value, what)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is a {type.Name}, which this tool does not set");
        }

        /// <summary>Writes a property's value the way a description spells it.</summary>
        /// <param name="value">The value read from Openness.</param>
        /// <returns>JSON for it; null when the type is not one <see cref="Supports"/> takes.</returns>
        public static JsonElement? Describe(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case Color color:
                    return JsonSerializer.SerializeToElement(
                        color.A == byte.MaxValue ? $"{color.R}, {color.G}, {color.B}" : $"{color.R}, {color.G}, {color.B}, {color.A}");
                case Enum enumeration:
                    return JsonSerializer.SerializeToElement(enumeration.ToString());
            }

            return Supports(value.GetType()) ? JsonSerializer.SerializeToElement(value, value.GetType()) : (JsonElement?)null;
        }

        private static bool ToBoolean(JsonElement value, string what)
        {
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                throw Refuse(what, "true or false", value);
            }

            return value.GetBoolean();
        }

        /// <remarks>
        /// Unified colours carry an alpha channel (a transparent background is alpha 0), so a colour that is
        /// not opaque is written <c>R, G, B, A</c>; anything else goes through the classic colour names.
        /// </remarks>
        private static Color ToColor(JsonElement value, string what)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw Refuse(what, "a colour such as 'R, G, B', 'R, G, B, A', '#RRGGBB' or 'green'", value);
            }

            var given = value.GetString()!;
            var withAlpha = WithAlpha.Match(given);
            if (withAlpha.Success)
            {
                var argb = new[] { 4, 1, 2, 3 }.Select(group => int.Parse(withAlpha.Groups[group].Value, CultureInfo.InvariantCulture)).ToArray();

                return argb.All(channel => channel <= byte.MaxValue)
                    ? Color.FromArgb(argb[0], argb[1], argb[2], argb[3])
                    : throw Refuse(what, "channels from 0 to 255", value);
            }

            var channels = HmiColor.Parse(given, what).Split(',').Select(channel => int.Parse(channel.Trim(), CultureInfo.InvariantCulture)).ToArray();

            return Color.FromArgb(channels[0], channels[1], channels[2]);
        }

        private static object ToFloatingPoint(Type type, JsonElement value, string what)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
            {
                throw Refuse(what, "a number", value);
            }

            return type == typeof(float) ? (object)(float)number : number;
        }

        private static object ToInteger(Type type, (decimal Minimum, decimal Maximum) range, JsonElement value, string what)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number) || decimal.Truncate(number) != number)
            {
                throw Refuse(what, "a whole number", value);
            }

            if (number < range.Minimum || number > range.Maximum)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} takes {range.Minimum} to {range.Maximum}; got {number}");
            }

            return System.Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
        }

        /// <remarks>
        /// A set of flags is read back as its names joined by commas (<c>"UnSuppressed, Disabled"</c>, met on
        /// an alarm control on 2026-10-21), so a flags type takes that list; any other type takes one name.
        /// </remarks>
        private static object ToEnum(Type type, JsonElement value, string what)
        {
            var names = Enum.GetNames(type);
            var given = value.ValueKind == JsonValueKind.String ? value.GetString()!.Split(',').Select(part => part.Trim()).ToList() : new List<string>();
            var matches = given.Select(part => names.FirstOrDefault(name => name.Equals(part, StringComparison.OrdinalIgnoreCase))).ToList();
            var isFlags = type.IsDefined(typeof(FlagsAttribute), false);

            if (matches.Count > 0 && matches.All(match => match != null) && (isFlags || matches.Count == 1))
            {
                return Enum.Parse(type, string.Join(", ", matches));
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"{what} is {(isFlags ? "a comma-separated list of" : "one of")}: {string.Join(", ", names.Take(MaxNamesListed))}{(names.Length > MaxNamesListed ? ", …" : string.Empty)}; got {value.GetRawText()}");
        }

        private static PortalException Refuse(string what, string expected, JsonElement value)
        {
            return new PortalException(PortalErrorCode.InvalidParams, $"{what} takes {expected}; got {value.GetRawText()}");
        }
    }
}
