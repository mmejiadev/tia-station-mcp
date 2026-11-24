using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>The logical address of a PLC tag, checked before it reaches a tag table.</summary>
    /// <remarks>
    /// Measured 2026-11-19 on V21: <c>PlcTagTable.Tags.Create</c> takes any text as an address without a word -
    /// <c>nonsense</c>, <c>%Q0.8</c>, <c>%M0.0.0</c>, <c>%DB1.DBX0.0</c>, <c>%Q9999999.9</c> - and the table then holds it.
    /// So the address is checked here, the one place every tag write passes through: an input, output or memory bit (bit 0
    /// to 7), byte, word, double word or long word. Only tags: a constant's value is not an address.
    /// </remarks>
    public static class PlcTagAddress
    {
        /// <remarks>
        /// <c>[0-9]</c> rather than <c>\d</c>: in .NET <c>\d</c> also matches digits of other scripts, which no address
        /// holds.
        /// </remarks>
        private static readonly Regex Pattern = new Regex(@"^%[IQM](?:[0-9]{1,5}\.[0-7]|[BWDL][0-9]{1,5})$", RegexOptions.CultureInvariant);

        /// <summary>Reads a tag's address, in upper case, refusing one that is not an input, output or memory address.</summary>
        /// <param name="address">The address as written, for example <c>%i0.1</c>.</param>
        /// <param name="tagName">The tag it is for, named in the refusal.</param>
        /// <returns>The address in upper case, for example <c>%I0.1</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is not a tag address.</exception>
        public static string Parse(string? address, string tagName)
        {
            var written = address?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!Pattern.IsMatch(written))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{written}' for '{tagName}' is not a tag address: an input, output or memory bit (%I0.0, bit 0 to 7), " +
                    "or a byte, word, double word or long word (%QB1, %MW10, %ID4, %ML8)");
            }

            return written;
        }
    }
}
