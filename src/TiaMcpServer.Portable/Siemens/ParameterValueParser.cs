using System;
using System.Collections.Generic;
using System.Globalization;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns the text a caller wrote into the type the attribute already holds.
    /// </summary>
    /// <remarks>
    /// **The type comes from the value that is there now, not from the caller.** Openness takes
    /// <c>SetAttribute(name, object)</c> and decides afterwards whether it liked what it got: hand
    /// it the string "5" where an integer belongs and it fails with a message about nothing in
    /// particular. Every parameter, on the other hand, already has a value, and that value knows
    /// its own type.
    ///
    /// So an MCP caller writes text — it is all JSON can send unambiguously — and the conversion
    /// happens here, where a failure can name the type that was expected and, for an enumeration,
    /// the words it accepts. That last one matters more than it looks: a protection level or a
    /// start-up mode is an enum whose spellings appear in no documentation a model has read.
    ///
    /// Deliberately narrow. Booleans, whole numbers, decimals, enumerations and strings are what
    /// device parameters are made of; anything else is refused by name rather than guessed at,
    /// because a parameter this cannot convert is one nobody has looked at yet.
    /// </remarks>
    public static class ParameterValueParser
    {
        // Integer is sign and surrounding space; Float adds a decimal point and an exponent. Neither
        // admits a group separator, which is what turns a decimal comma into a refusal.
        private const NumberStyles WholeNumber = NumberStyles.Integer;
        private const NumberStyles DecimalNumber = NumberStyles.Float;

        private static readonly Dictionary<Type, Func<string, object?>> NumberParsers = new Dictionary<Type, Func<string, object?>>
        {
            [typeof(byte)] = text => byte.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(sbyte)] = text => sbyte.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(short)] = text => short.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(ushort)] = text => ushort.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(int)] = text => int.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(uint)] = text => uint.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(long)] = text => long.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(ulong)] = text => ulong.TryParse(text, WholeNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(float)] = text => float.TryParse(text, DecimalNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(double)] = text => double.TryParse(text, DecimalNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null,
            [typeof(decimal)] = text => decimal.TryParse(text, DecimalNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : (object?)null
        };

        /// <summary>Converts text to the type of the value an attribute currently holds.</summary>
        /// <param name="currentValue">What the attribute holds now, which supplies the type.</param>
        /// <param name="text">What the caller wrote.</param>
        /// <param name="attributeName">The attribute, for the messages.</param>
        /// <returns>The value to hand to Openness.</returns>
        /// <exception cref="PortalException">
        /// The current value is missing, the text does not fit the type, or the type is one this
        /// does not convert.
        /// </exception>
        public static object Parse(object? currentValue, string text, string attributeName)
        {
            if (text == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"A value is required for '{attributeName}'");
            }

            if (currentValue == null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{attributeName}' has no current value, so there is nothing to take its type from. " +
                    "Set it in TIA Portal once and it becomes writable from here.");
            }

            var type = currentValue.GetType();

            if (type.IsEnum)
            {
                return ParseEnum(type, text, attributeName);
            }

            if (type == typeof(bool))
            {
                return ParseBoolean(text, attributeName);
            }

            if (type == typeof(string))
            {
                return text;
            }

            return ParseNumber(type, text, attributeName);
        }

        private static object ParseEnum(Type type, string text, string attributeName)
        {
            foreach (var name in Enum.GetNames(type))
            {
                if (string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse(type, name);
                }
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{text}' is not one of the values '{attributeName}' takes. It takes: {string.Join(", ", Enum.GetNames(type))}.");
        }

        private static object ParseBoolean(string text, string attributeName)
        {
            if (bool.TryParse(text.Trim(), out var parsed))
            {
                return parsed;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{text}' is not a value for '{attributeName}', which is a yes-or-no parameter. Write 'true' or 'false'.");
        }

        /// <remarks>
        /// Invariant culture and narrow styles, the two decisions <see cref="SimulationTagValueParser"/>
        /// documents, for the same reason. <c>Convert.ChangeType</c> was used here first, and it
        /// parses with <c>AllowThousands</c>: measured, it reads '0,5' as 5. On a Spanish machine a
        /// half prints as 0,5, so a value copied from GetDeviceParameters into SetDeviceParameter
        /// would have been written ten times larger and read back as a success.
        /// </remarks>
        private static object ParseNumber(Type type, string text, string attributeName)
        {
            if (!NumberParsers.TryGetValue(type, out var parse))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{attributeName}' holds a {type.Name}, which this does not know how to write. " +
                    "Only yes-or-no values, numbers, words from a fixed list and free text can be set from here.");
            }

            return parse(text.Trim())
                ?? throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{text}' is not a {type.Name}, which is what '{attributeName}' holds. " +
                    "Use a decimal point rather than a comma, and no thousands separator.");
        }
    }
}
