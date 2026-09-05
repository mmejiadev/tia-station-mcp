using System;
using System.Globalization;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns the text of a tag write into the .NET value the PLCSIM API expects for that PLC type.
    /// </summary>
    /// <remarks>
    /// A tag write arrives as text because a tool call cannot know the type in advance: the tag
    /// list decides it, and the same parameter has to be able to carry TRUE, 17 and 1.5. Parsing is
    /// separated from <c>SimulationTagAccess</c> because it needs no controller and no
    /// PLCSIM API at all, which is also what lets it be tested without one.
    ///
    /// The methods are named after the PLC type rather than the .NET one, because that is the name
    /// the caller sees in the tag list and in the error message when a value does not fit.
    /// </remarks>
    internal static class SimulationTagValueParser
    {
        /// <summary>Parses a Bool.</summary>
        public static bool ToBool(string value)
        {
            var text = (value ?? string.Empty).Trim();

            if (bool.TryParse(text, out var parsed))
            {
                return parsed;
            }

            // TRUE and FALSE are how SCL spells them; 1 and 0 are how a watch table shows them.
            // Both reach here from a caller who has made no mistake.
            if (string.Equals(text, "1", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(text, "0", StringComparison.Ordinal))
            {
                return false;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{value}' is not a Bool. Write 'true', 'false', '1' or '0'.");
        }

        /// <summary>Parses a WChar, which is written as the character itself.</summary>
        /// <remarks>
        /// A Char is not parsed here. The PLCSIM API writes one through <c>WriteChar(string, SByte)</c>,
        /// so a Char is written as its numeric code and goes through <see cref="ToSInt"/>.
        /// </remarks>
        public static char ToWChar(string value)
        {
            if (value == null || value.Length != 1)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{value}' is not a single character.");
            }

            return value[0];
        }

        /// <summary>Parses an SInt, and a Char, which is written as its numeric code.</summary>
        public static sbyte ToSInt(string value) => Parse<sbyte>(value, "SInt", NumberStyles.Integer, sbyte.TryParse);

        /// <summary>Parses a USInt or a Byte.</summary>
        public static byte ToUSInt(string value) => Parse<byte>(value, "USInt", NumberStyles.Integer, byte.TryParse);

        /// <summary>Parses an Int.</summary>
        public static short ToInt(string value) => Parse<short>(value, "Int", NumberStyles.Integer, short.TryParse);

        /// <summary>Parses a UInt or a Word.</summary>
        public static ushort ToUInt(string value) => Parse<ushort>(value, "UInt", NumberStyles.Integer, ushort.TryParse);

        /// <summary>Parses a DInt.</summary>
        public static int ToDInt(string value) => Parse<int>(value, "DInt", NumberStyles.Integer, int.TryParse);

        /// <summary>Parses a UDInt or a DWord.</summary>
        public static uint ToUDInt(string value) => Parse<uint>(value, "UDInt", NumberStyles.Integer, uint.TryParse);

        /// <summary>Parses an LInt.</summary>
        public static long ToLInt(string value) => Parse<long>(value, "LInt", NumberStyles.Integer, long.TryParse);

        /// <summary>Parses a ULInt or an LWord.</summary>
        public static ulong ToULInt(string value) => Parse<ulong>(value, "ULInt", NumberStyles.Integer, ulong.TryParse);

        /// <summary>Parses a Real.</summary>
        public static float ToReal(string value) => Parse<float>(value, "Real", NumberStyles.Float, float.TryParse);

        /// <summary>Parses an LReal.</summary>
        public static double ToLReal(string value) => Parse<double>(value, "LReal", NumberStyles.Float, double.TryParse);

        private delegate bool NumberParser<T>(string text, NumberStyles styles, IFormatProvider provider, out T parsed);

        /// <summary>Parses a number the same way whatever the machine's locale is.</summary>
        /// <remarks>
        /// Invariant culture, always. A Real written as '1,5' on a Spanish machine and as '1.5' on
        /// an English one would otherwise make the same tool call mean two different values.
        ///
        /// **The styles are narrow on purpose, and `NumberStyles.Any` is a trap.** It was used here
        /// first and it defeated the whole point: `Any` includes `AllowThousands`, so the invariant
        /// culture read '1,5' as one thousand five hundred — measured, 1,5 parsed to 15 — and the
        /// mistake this method exists to reject was accepted, written to a controller, and read
        /// back as a success. It also includes `AllowParentheses`, so '(5)' arrived as -5.
        ///
        /// `Integer` is sign and surrounding space; `Float` adds a decimal point and an exponent.
        /// Neither admits a group separator, which is what makes the error message below true.
        /// </remarks>
        private static T Parse<T>(string value, string plcTypeName, NumberStyles styles, NumberParser<T> parser)
        {
            if (parser(value ?? string.Empty, styles, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{value}' is not a {plcTypeName}. Use a decimal point rather than a comma, and no thousands separator.");
        }
    }
}
