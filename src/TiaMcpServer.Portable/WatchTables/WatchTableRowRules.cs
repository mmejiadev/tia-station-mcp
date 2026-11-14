using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.WatchTables
{
    /// <summary>
    /// What a row may hold: the addresses, display formats and values this server writes into watch and force tables.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-13 on TIA Portal V21, importing into a probe project: TIA Portal takes a row with any address
    /// and checks little — it took <c>%M5.0</c> into the force table, although an S7-1500 forces inputs and outputs
    /// only, and it added <c>:P</c> to the inputs and outputs it was given there. So the checks are this server's, made
    /// before anything is imported, and only what was written and read back is accepted: absolute addresses, five
    /// display formats and their values. A symbolic name is refused until it is measured, and so is a data block
    /// address: a row on a data block the program does not have made TIA Portal drop every row of the table, without
    /// a word (measured 2026-11-13 with %DB1.DBX0.0 and no DB1).
    /// </remarks>
    public static class WatchTableRowRules
    {
        /// <summary>The display formats a row may take.</summary>
        public static readonly IReadOnlyList<string> DisplayFormats = new[] { "Bool", "DEC_signed", "DEC_unsigned", "Hex", "Float" };

        private const string BoolFormat = "Bool";
        private const string FloatFormat = "Float";
        private static readonly Regex BitAddress = new Regex(@"^%(?<area>[IQM])(?<byte>\d+)\.[0-7](?<periphery>:P)?$", RegexOptions.CultureInvariant);
        private static readonly Regex WordAddress = new Regex(@"^%(?<area>[IQM])(?<size>[BWD])(?<byte>\d+)(?<periphery>:P)?$", RegexOptions.CultureInvariant);
        private static readonly Regex HexValue = new Regex(@"^16#(?<digits>[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant);
        private const int DoubleWordBits = 32;
        private const int BitsPerHexDigit = 4;

        /// <summary>Checks one row and says what is wrong with it.</summary>
        /// <param name="kind">The table it goes in.</param>
        /// <param name="row">The row, with its fields trimmed.</param>
        /// <returns>The problems found; empty when the row may be written.</returns>
        public static IReadOnlyList<string> Check(WatchTableKind kind, WatchTableRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            var problems = new List<string>();

            CheckAddress(kind, row, problems);
            CheckFormat(row, problems);

            if (problems.Count == 0)
            {
                CheckValue(kind, row, problems);
            }

            return problems;
        }

        /// <summary>The value as TIA Portal writes it: <c>TRUE</c> and <c>FALSE</c> upper case, anything else as given.</summary>
        /// <param name="displayFormat">The row's display format.</param>
        /// <param name="value">The value as the caller wrote it.</param>
        /// <returns>The value to write.</returns>
        public static string NormalizeValue(string displayFormat, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return displayFormat == BoolFormat ? value.ToUpperInvariant() : value;
        }

        private static void CheckAddress(WatchTableKind kind, WatchTableRow row, List<string> problems)
        {
            var bit = BitAddress.Match(row.Address);
            var word = WordAddress.Match(row.Address);

            if (!bit.Success && !word.Success)
            {
                problems.Add($"'{row.Address}' is not an address this server writes: an input, output or memory bit, byte, word or double word "
                    + "such as %I0.0, %QW2 or %MD4 (symbolic names and data blocks are not measured yet)");
                return;
            }

            var area = bit.Success ? bit.Groups["area"].Value : word.Groups["area"].Value;
            var periphery = (bit.Success ? bit : word).Groups["periphery"].Success;

            if (kind == WatchTableKind.Force && area != "I" && area != "Q")
            {
                problems.Add($"'{row.Address}' cannot be forced: an S7-1500 forces inputs and outputs only (%I, %Q)");
            }

            if (periphery && area == "M")
            {
                problems.Add($"'{row.Address}': only inputs and outputs have a peripheral access (:P)");
            }
        }

        private static void CheckFormat(WatchTableRow row, List<string> problems)
        {
            if (!DisplayFormats.Contains(row.DisplayFormat))
            {
                problems.Add($"'{row.DisplayFormat}' is not a display format this server writes; use one of {string.Join(", ", DisplayFormats)}");
                return;
            }

            var isBitAddress = BitAddress.IsMatch(row.Address);

            if (isBitAddress != (row.DisplayFormat == BoolFormat))
            {
                problems.Add($"'{row.Address}' and '{row.DisplayFormat}' do not go together: a bit is shown as Bool, and Bool shows a bit only");
            }

            if (row.DisplayFormat == FloatFormat && AddressBits(row.Address) != DoubleWordBits)
            {
                problems.Add($"'{row.Address}' and Float do not go together: a Float takes a double word, such as %MD4");
            }
        }

        /// <remarks>
        /// The width the value has to fit: TIA Portal's import does not check it, and a row it later drops would only be
        /// found after the table's earlier rows are gone.
        /// </remarks>
        private static int AddressBits(string address)
        {
            var word = WordAddress.Match(address);

            if (!word.Success)
            {
                return 1;
            }

            return word.Groups["size"].Value switch
            {
                "B" => 8,
                "W" => 16,
                "D" => DoubleWordBits,
                _ => throw new InvalidOperationException($"Unrecognised address size in '{address}'")
            };
        }

        private static void CheckValue(WatchTableKind kind, WatchTableRow row, List<string> problems)
        {
            if (row.Value.Length == 0)
            {
                if (kind == WatchTableKind.Force)
                {
                    problems.Add($"'{row.Address}' has no value: a force table row says what the address is forced to");
                }

                return;
            }

            var bits = AddressBits(row.Address);

            if (!IsValueOfFormat(row.DisplayFormat, row.Value, bits))
            {
                problems.Add($"'{row.Value}' is not a {row.DisplayFormat} value that fits the {bits} bit(s) of '{row.Address}' "
                    + "(Bool TRUE or FALSE, DEC_signed -12, DEC_unsigned 12, Hex 16#0C, Float 1.5)");
            }
        }

        private static bool IsValueOfFormat(string displayFormat, string value, int bits)
        {
            return displayFormat switch
            {
                "Bool" => string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase),
                "DEC_signed" => long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var signed)
                    && signed >= -(1L << (bits - 1)) && signed <= (1L << (bits - 1)) - 1,
                "DEC_unsigned" => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var unsigned)
                    && unsigned <= (1UL << bits) - 1,
                "Hex" => IsHexOfWidth(value, bits),
                "Float" => IsReal(value),
                _ => false
            };
        }

        /// <remarks>A double word holds a 32-bit REAL: a value beyond its range is one TIA Portal could only drop.</remarks>
        private static bool IsReal(string value)
        {
            return value.Contains('.')
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && !double.IsInfinity(number)
                && Math.Abs(number) <= float.MaxValue;
        }

        private static bool IsHexOfWidth(string value, int bits)
        {
            var hex = HexValue.Match(value);

            return hex.Success && hex.Groups["digits"].Value.TrimStart('0').Length <= bits / BitsPerHexDigit;
        }
    }
}
