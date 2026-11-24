using System;
using System.Collections.Generic;
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
        private const long AreaPeripheralInputs = 0x1;
        private const long AreaPeripheralOutputs = 0x2;
        private const long LargestOffset = 0xFFFF;

        private static readonly HashSet<long> KnownAreas = new HashSet<long> { 0x81, 0x82, 0x83, AreaDataBlock, AreaPeripheralInputs, AreaPeripheralOutputs };
        private static readonly HashSet<long> OutputAreas = new HashSet<long> { 0x82, AreaPeripheralOutputs };
        private static readonly string[] OneSide = { string.Empty };
        private static readonly string[] BothSides = { "_SRC", "_DEST" };

        private static readonly Regex Call = new Regex(@"\b(PEEK(?:_BOOL|_BYTE|_WORD|_DWORD|_LWORD)?|POKE(?:_BOOL|_BLK)?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Argument = new Regex(@"(\w+)\s*(:=|=>)\s*([^,]+)", RegexOptions.CultureInvariant);
        private static readonly Regex Hidden = new Regex(@"""[^""\n]*""|//[^\n]*|\(\*.*?\*\)|/\*.*?\*/|'(?:\$.|[^'$\n])*'", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex BlockStart = new Regex(@"\b(?:FUNCTION_BLOCK|FUNCTION|ORGANIZATION_BLOCK|DATA_BLOCK)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ErrorIdCall = new Regex(@"\bGET_ERR_ID\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Whether an SCL source calls PEEK or POKE at all, outside comments and strings.</summary>
        /// <param name="source">The SCL source.</param>
        /// <returns>True when it does: only then is the project asked for its data blocks.</returns>
        public static bool HasCalls(string? source)
        {
            return Call.IsMatch(Hide(source));
        }

        /// <summary>The data block numbers the PEEK and POKE calls of a source name as constants, with the DB area.</summary>
        /// <param name="source">The SCL source.</param>
        /// <returns>The numbers, each once: only these need the project's data blocks read.</returns>
        public static IReadOnlyCollection<long> NamedDataBlocks(string? source)
        {
            return Calls(Hide(source))
                .SelectMany(found => Sides(found.Call).Select(side => DataBlockNumber(found.Call, side)))
                .OfType<long>()
                .Distinct()
                .ToList();
        }

        /// <summary>Checks the PEEK and POKE calls of an SCL source against what is known of the program's data blocks.</summary>
        /// <param name="source">The SCL source.</param>
        /// <param name="dataBlocks">The program's data blocks, read from the project; <see cref="ProgramDataBlocks.Unknown"/> judges the text alone.</param>
        /// <returns>The findings, by line.</returns>
        public static IReadOnlyList<SclFinding> Check(string source, ProgramDataBlocks dataBlocks)
        {
            var known = dataBlocks ?? ProgramDataBlocks.Unknown;

            return Calls(Hide(source))
                .SelectMany(found => Problems(found.Call, known, found.ReadsErrorId))
                .OrderBy(finding => finding.Line)
                .ToList();
        }

        /// <summary>Checks the PEEK and POKE calls of an SCL source from its text alone, before anything is written.</summary>
        /// <param name="source">The SCL source.</param>
        /// <returns>The findings, by line: every rule but those that need the project's data blocks.</returns>
        public static IReadOnlyList<SclFinding> CheckText(string source)
        {
            return Check(source, ProgramDataBlocks.Unknown);
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

        private static IEnumerable<(SclMemoryAccessCall Call, bool ReadsErrorId)> Calls(string text)
        {
            var blocks = BlocksReadingErrorId(text);
            var line = 1;
            var counted = 0;

            foreach (Match match in Call.Matches(text))
            {
                line += Breaks(text, counted, match.Index);
                counted = match.Index;
                var call = new SclMemoryAccessCall(match.Groups[1].Value.ToUpperInvariant(), line, ReadArguments(text, match.Index + match.Length));

                yield return (call, blocks.Last(block => block.Start <= match.Index).ReadsErrorId);
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
        private static string[] Sides(SclMemoryAccessCall call)
        {
            return call.Name == "POKE_BLK" ? BothSides : OneSide;
        }

        private static List<SclFinding> Problems(SclMemoryAccessCall call, ProgramDataBlocks dataBlocks, bool readsErrorId)
        {
            var found = Sides(call).SelectMany(side => SideProblems(call, side, dataBlocks)).ToList();

            if (call.Name.StartsWith("POKE", StringComparison.Ordinal) && (call.Constant("AREA") ?? call.Constant("AREA_DEST")) is long output && OutputAreas.Contains(output))
            {
                found.Add(call.Warning("poke-writes-outputs", "it writes outputs by address: what it switches is named nowhere, and the cross-references do not show it (measured)"));
            }

            if (readsErrorId && call.Value("ENO") == null)
            {
                found.Add(call.Warning("peek-poke-eno-check", "GET_ERR_ID returns 0 after a failed PEEK or POKE (measured); read its ENO => instead"));
            }

            return found;
        }

        private static IEnumerable<SclFinding> SideProblems(SclMemoryAccessCall call, string side, ProgramDataBlocks dataBlocks)
        {
            var area = call.Constant("AREA" + side);
            var offset = call.Constant("BYTEOFFSET" + side);

            var found = new List<SclFinding?>
            {
                area is long known && !KnownAreas.Contains(known) ? call.Error("peek-poke-unknown-area", $"AREA{side} 16#{known:X} is none of 16#81 (I), 16#82 (Q), 16#83 (M), 16#84 (DB), 16#1 or 16#2 (peripheral)") : null,
                CheckPeripheral(call, side, area),
                CheckDataBlockNumber(call, side, area, dataBlocks),
                offset is long beyond && beyond > LargestOffset ? call.Warning("peek-poke-offset-over-16-bit", $"BYTEOFFSET{side} {beyond} is past 65535: measured, only its 16 low bits are used, so it reaches byte {beyond & LargestOffset}") : null
            };

            return found.OfType<SclFinding>();
        }

        /// <remarks>
        /// The help lists the peripheral inputs, 16#1, for PEEK and PEEK_BOOL only, the peripheral outputs, 16#2, for POKE and
        /// POKE_BOOL only, and neither for POKE_BLK. Measured on a 1511, the other directions compile; what they do at run
        /// time was not measured, so this warns rather than calls it an error.
        /// </remarks>
        private static SclFinding? CheckPeripheral(SclMemoryAccessCall call, string side, long? area)
        {
            if (area is not long peripheral || (peripheral != AreaPeripheralInputs && peripheral != AreaPeripheralOutputs))
            {
                return null;
            }

            var listed = call.Name == "POKE_BLK" ? (long?)null : call.Name.StartsWith("PEEK", StringComparison.Ordinal) ? AreaPeripheralInputs : AreaPeripheralOutputs;
            var named = peripheral == AreaPeripheralInputs ? "the peripheral inputs" : "the peripheral outputs";

            return peripheral == listed
                ? null
                : call.Warning("peek-poke-peripheral", $"AREA{side} 16#{peripheral:X} is {named}, which the help does not list for {call.Name}: it compiles (measured), what it does when it runs is not measured");
        }

        private static SclFinding? CheckDataBlockNumber(SclMemoryAccessCall call, string side, long? area, ProgramDataBlocks dataBlocks)
        {
            if (area is not long given || call.Constant("DBNUMBER" + side) is not long number)
            {
                return null;
            }

            if (given != AreaDataBlock)
            {
                return number == 0 ? null : call.Error("peek-poke-dbnumber-not-zero", $"DBNUMBER{side} must be 0 outside the DB area: measured, the call returns 0 with ENO FALSE and the CPU runs on");
            }

            if (dataBlocks.IsMissing(number))
            {
                return call.Error("peek-poke-missing-db", $"DB {number} does not exist in the program: measured, the call returns 0 with ENO FALSE and the CPU runs on; create it with standard access, or name one that exists");
            }

            return dataBlocks.IsOptimized(number)
                ? call.Error("peek-poke-optimized-db", $"DB {number} has optimized access: measured, the call returns 0 with ENO FALSE; clear 'Optimized block access' on it, or use the DB symbolically")
                : null;
        }

        /// <returns>The constant DBNUMBER of a side whose constant AREA is the DB area, or null.</returns>
        private static long? DataBlockNumber(SclMemoryAccessCall call, string side)
        {
            return call.Constant("AREA" + side) == AreaDataBlock ? call.Constant("DBNUMBER" + side) : null;
        }
    }
}
