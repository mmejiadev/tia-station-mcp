using System;
using System.Globalization;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Writes a parameter value as text that <see cref="ParameterValueParser"/> reads back.
    /// </summary>
    /// <remarks>
    /// The other half of the parser, and the half that was missing. The parser reads invariant
    /// text, while string interpolation prints with the machine's culture: on a Spanish machine a
    /// half prints as 0,5. Whatever a caller reads from GetDeviceParameters is what it copies into
    /// SetDeviceParameter, so the read has to be written in the language the write reads — and a
    /// backup has to mean the same number on whichever machine opens it.
    /// </remarks>
    public static class ParameterValueFormatter
    {
        /// <summary>Formats a value the way the parser reads it.</summary>
        /// <param name="value">The value, or null.</param>
        /// <returns>Invariant text, or null when there is no value.</returns>
        public static string? Format(object? value)
        {
            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value?.ToString();
        }
    }
}
