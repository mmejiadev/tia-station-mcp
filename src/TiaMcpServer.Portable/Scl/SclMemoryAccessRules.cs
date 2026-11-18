using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Scl
{
    /// <summary>Finds PEEK and POKE calls in SCL that TIA Portal compiles and that fail, quietly, when they run.</summary>
    /// <remarks>
    /// Each rule rests on <c>docs/PEEK-POKE.md</c>: the TIA Portal V21 help, and what was measured on 2026-11-18 on a CPU
    /// 1511 in PLCSIM Advanced V8 - an optimized DB read by number, a DB number with an area that is not a DB, a DB that
    /// does not exist: each compiles, and at run time returns 0 with ENO FALSE, the CPU staying in RUN, and
    /// <c>GET_ERR_ID</c> returning 0 after it. Only constants are judged: a value computed at run time is not known here,
    /// and nothing is said of it - which is not a pass. The source is read as text, never compiled; comments and string
    /// literals are left out.
    /// </remarks>
    public static class SclMemoryAccessRules
    {
        private const long AreaDataBlock = 0x84;
        private const long LargestOffset = 0xFFFF;

        /// <summary>The most digits a literal is read with, per radix: a DWORD's, so the value is never negative.</summary>
        private static readonly Dictionary<string, (int Radix, int Digits)> Radixes = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["hex"] = (16, 8),
            ["oct"] = (8, 11),
            ["bin"] = (2, 32),
            ["dec"] = (10, 10)
        };

        private static readonly HashSet<long> KnownAreas = new HashSet<long> { 0x81, 0x82, 0x83, 0x84, 0x1, 0x2 };
        private static readonly HashSet<long> OutputAreas = new HashSet<long> { 0x82, 0x2 };
        private static readonly string[] OneSide = { string.Empty };
        private static readonly string[] BothSides = { "_SRC", "_DEST" };

        private static readonly Regex Call = new Regex(@"\b(PEEK(?:_BOOL|_BYTE|_WORD|_DWORD|_LWORD)?|POKE(?:_BOOL|_BLK)?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Argument = new Regex(@"(\w+)\s*(:=|=>)\s*([^,]+)", RegexOptions.CultureInvariant);
        private static readonly Regex Hidden = new Regex(@"""[^""\n]*""|//[^\n]*|\(\*.*?\*\)|/\*.*?\*/|'(?:\$.|[^'$\n])*'", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex BlockStart = new Regex(@"\b(?:FUNCTION_BLOCK|FUNCTION|ORGANIZATION_BLOCK|DATA_BLOCK)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ErrorIdCall = new Regex(@"\bGET_ERR_ID\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Literal = new Regex(@"^(?:[A-Z]+#)?(?:16#(?<hex>[0-9A-F_]+)|8#(?<oct>[0-7_]+)|2#(?<bin>[01_]+)|(?<dec>[0-9][0-9_]*))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Whether an SCL source calls PEEK or POKE at all, outside comments and strings.</summary>
        /// <param name="source">The SCL source.</param>
        /// <returns>True when it does: only then is the project asked for its optimized data blocks.</returns>
        public static bool HasCalls(string? source)
        {
            return Call.IsMatch(Hide(source));
        }

        /// <summary>Checks the PEEK and POKE calls of an SCL source.</summary>
        /// <param name="source">The SCL source.</param>
        /// <param name="optimizedDataBlocks">The numbers of the program's optimized data blocks, read from the project.</param>
        /// <returns>The findings, by line.</returns>
        public static IReadOnlyList<SclFinding> Check(string source, IReadOnlyCollection<int> optimizedDataBlocks)
        {
            var text = Hide(source);
            var optimized = optimizedDataBlocks ?? Array.Empty<int>();

            return Findings(text, optimized).OrderBy(finding => finding.Line).ToList();
        }

        /// <remarks>
        /// Comments become their line breaks and strings become empty, so line numbers hold. A quoted name is matched only
        /// to be kept whole: an apostrophe or <c>//</c> in <c>"Tank's level"</c> opens no string and no comment.
        /// </remarks>
        private static string Hide(string? source)
        {
            return Hidden.Replace(source ?? string.Empty, match => match.Value.StartsWith("\"", StringComparison.Ordinal)
                ? match.Value
                : (match.Value.StartsWith("'", StringComparison.Ordinal) ? "''" : string.Empty) + new string('\n', match.Value.Count(character => character == '\n')));
        }

        private static IEnumerable<SclFinding> Findings(string text, IReadOnlyCollection<int> optimized)
        {
            var blocks = BlocksReadingErrorId(text);
            var line = 1;
            var counted = 0;

            foreach (Match match in Call.Matches(text))
            {
                line += Breaks(text, counted, match.Index);
                counted = match.Index;
                var call = new SclMemoryAccessCall(match.Groups[1].Value.ToUpperInvariant(), line, ReadArguments(text, match.Index + match.Length));
                var readsErrorId = blocks.Last(block => block.Start <= match.Index).ReadsErrorId;

                foreach (var finding in Problems(call, optimized, readsErrorId))
                {
                    yield return finding;
                }
            }
        }

        /// <remarks>Where each block of the source starts, and whether it calls GET_ERR_ID; text before any block is one too.</remarks>
        private static List<(int Start, bool ReadsErrorId)> BlocksReadingErrorId(string text)
        {
            var starts = new[] { 0 }.Concat(BlockStart.Matches(text).Cast<Match>().Select(match => match.Index)).Append(text.Length).ToList();

            return starts.Take(starts.Count - 1)
                .Select((start, index) => (start, ErrorIdCall.IsMatch(text.Substring(start, starts[index + 1] - start))))
                .ToList();
        }

        private static int Breaks(string text, int from, int to)
        {
            var breaks = 0;
            for (var index = from; index < to; index++)
            {
                breaks += text[index] == '\n' ? 1 : 0;
            }

            return breaks;
        }

        /// <remarks>
        /// Up to the parenthesis that closes the call. What is inside a nested parenthesis is left out, so a nested call's
        /// own <c>ENO =&gt;</c> is not taken for this call's.
        /// </remarks>
        private static Dictionary<string, string> ReadArguments(string text, int start)
        {
            var own = new StringBuilder();
            var depth = 1;
            for (var index = start; index < text.Length && depth > 0; index++)
            {
                var character = text[index];
                depth += character == '(' ? 1 : character == ')' ? -1 : 0;
                own.Append(depth == 1 && character != ')' ? character : ' ');
            }

            return Argument.Matches(own.ToString())
                .Cast<Match>()
                .GroupBy(match => match.Groups[1].Value.ToUpperInvariant())
                .ToDictionary(group => group.Key, group => group.First().Groups[2].Value == "=>" ? "=>" : group.First().Groups[3].Value.Trim(), StringComparer.Ordinal);
        }

        /// <remarks>POKE_BLK is judged on both sides, the source and the destination.</remarks>
        private static List<SclFinding> Problems(SclMemoryAccessCall call, IReadOnlyCollection<int> optimized, bool readsErrorId)
        {
            var found = (call.Name == "POKE_BLK" ? BothSides : OneSide).SelectMany(side => SideProblems(call, side, optimized)).ToList();

            if (call.Name.StartsWith("POKE", StringComparison.Ordinal) && Constant(call.Value("AREA") ?? call.Value("AREA_DEST")) is long output && OutputAreas.Contains(output))
            {
                found.Add(call.Warning("poke-writes-outputs", "it writes outputs by address: what it switches is named nowhere, and the cross-references do not show it (measured)"));
            }

            if (readsErrorId && call.Value("ENO") == null)
            {
                found.Add(call.Warning("peek-poke-eno-check", "GET_ERR_ID returns 0 after a failed PEEK or POKE (measured); read its ENO => instead"));
            }

            return found;
        }

        private static IEnumerable<SclFinding> SideProblems(SclMemoryAccessCall call, string side, IReadOnlyCollection<int> optimized)
        {
            var area = Constant(call.Value("AREA" + side));
            var number = Constant(call.Value("DBNUMBER" + side));
            var offset = Constant(call.Value("BYTEOFFSET" + side));

            var found = new List<SclFinding?>
            {
                area is long known && !KnownAreas.Contains(known) ? call.Error("peek-poke-unknown-area", $"AREA{side} 16#{known:X} is none of 16#81 (I), 16#82 (Q), 16#83 (M), 16#84 (DB), 16#1 or 16#2 (peripheral)") : null,
                CheckDataBlockNumber(call, side, (area, number), optimized),
                offset is long beyond && beyond > LargestOffset ? call.Warning("peek-poke-offset-over-16-bit", $"BYTEOFFSET{side} {beyond} is past 65535: measured, only its 16 low bits are used, so it reaches byte {beyond & LargestOffset}") : null
            };

            return found.OfType<SclFinding>();
        }

        private static SclFinding? CheckDataBlockNumber(SclMemoryAccessCall call, string side, (long? Area, long? Number) given, IReadOnlyCollection<int> optimized)
        {
            if (given.Area is not long area || given.Number is not long number)
            {
                return null;
            }

            if (area != AreaDataBlock)
            {
                return number == 0 ? null : call.Error("peek-poke-dbnumber-not-zero", $"DBNUMBER{side} must be 0 outside the DB area: measured, the call returns 0 with ENO FALSE and the CPU runs on");
            }

            return number <= int.MaxValue && optimized.Contains((int)number)
                ? call.Error("peek-poke-optimized-db", $"DB {number} has optimized access: measured, the call returns 0 with ENO FALSE; clear 'Optimized block access' on it, or use the DB symbolically")
                : null;
        }

        /// <returns>
        /// The value of an integer literal written alone - <c>16#84</c>, <c>B#16#84</c>, <c>2#1000_0100</c>, <c>132</c> - or
        /// null for anything else, an expression holding a literal included.
        /// </returns>
        private static long? Constant(string? text)
        {
            var match = Literal.Match(text?.Trim() ?? string.Empty);
            var group = Radixes.FirstOrDefault(radix => match.Groups[radix.Key].Success);
            if (!match.Success || group.Key == null)
            {
                return null;
            }

            var digits = match.Groups[group.Key].Value.Replace("_", string.Empty);

            return digits.Length == 0 || digits.Length > group.Value.Digits
                ? null
                : group.Value.Radix == 10 ? long.Parse(digits, CultureInfo.InvariantCulture) : Convert.ToInt64(digits, group.Value.Radix);
        }
    }
}
