using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>The rules that check a script against the HMI's tags.</summary>
    /// <remarks>
    /// The bit functions are defined in the WinCC function list TIA Portal V20 ships
    /// (<c>Data\Hmi\Scripting\Utah-Functions.en.xml</c>, read 2026-10-25, classic WinCC's; Unified scripts
    /// call functions of the same names, tag then bit): <c>SetBitInTag</c> sets a bit to 1 and
    /// <c>ResetBitInTag</c> sets it to 0; neither takes a value. So <c>SetBitInTag("Motor", 0)</c>, as in the
    /// 0968 final project, sets bit 0 and switches nothing off. That list documents them for integer, byte,
    /// char, time and timer tags, not Bool; whether Unified refuses a Bool was not measured, so a Bool is a
    /// warning, and a bit past its tag's width an error.
    /// </remarks>
    internal static class UnifiedScriptTagRules
    {
        private static readonly HashSet<string> BitFunctions = new HashSet<string>(StringComparer.Ordinal) { "SetBitInTag", "ResetBitInTag", "InvertBitInTag" };

        /// <summary>System functions whose first argument is a tag's name.</summary>
        private static readonly HashSet<string> TagFunctions = new HashSet<string>(StringComparer.Ordinal)
        {
            "SetTagValue", "IncreaseTag", "DecreaseTag", "SetBitInTag", "ResetBitInTag", "InvertBitInTag"
        };

        private static readonly Dictionary<string, int> BitWidths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Byte"] = 8, ["SInt"] = 8, ["USInt"] = 8, ["WChar"] = 16, ["Int"] = 16, ["UInt"] = 16, ["Word"] = 16,
            ["DInt"] = 32, ["UDInt"] = 32, ["DWord"] = 32, ["LInt"] = 64, ["ULInt"] = 64, ["LWord"] = 64
        };

        /// <summary>Runs every rule; with no tags in the context, none of them finds anything.</summary>
        /// <param name="tokens">The script's tokens.</param>
        /// <param name="context">The HMI's tags.</param>
        /// <returns>The findings.</returns>
        public static IEnumerable<UnifiedScriptFinding> Check(IReadOnlyList<UnifiedScriptToken> tokens, UnifiedScriptContext context)
        {
            return context.TagTypes.Count == 0 ? Enumerable.Empty<UnifiedScriptFinding>() : BitCalls(tokens, context).Concat(MissingTags(tokens, context));
        }

        private static IEnumerable<UnifiedScriptFinding> BitCalls(IReadOnlyList<UnifiedScriptToken> tokens, UnifiedScriptContext context)
        {
            for (var index = 0; index + 4 < tokens.Count; index++)
            {
                if (!(tokens[index].Kind == UnifiedScriptTokenKind.Identifier && BitFunctions.Contains(tokens[index].Text) && tokens[index + 1].Is("(")
                    && tokens[index + 2].Kind == UnifiedScriptTokenKind.StringLiteral && tokens[index + 3].Is(",") && tokens[index + 4].Kind == UnifiedScriptTokenKind.Number))
                {
                    continue;
                }

                var finding = BitProblem(tokens[index], tokens[index + 2].Text, tokens[index + 4].Text, context);
                if (finding != null)
                {
                    yield return finding;
                }
            }
        }

        private static UnifiedScriptFinding? BitProblem(UnifiedScriptToken call, string tag, string bitText, UnifiedScriptContext context)
        {
            var function = call.Text;
            if (!context.TagTypes.TryGetValue(BaseName(tag), out var dataType) || !int.TryParse(bitText, NumberStyles.None, CultureInfo.InvariantCulture, out var bit))
            {
                return null;
            }

            if (dataType.Equals("Bool", StringComparison.OrdinalIgnoreCase))
            {
                return new UnifiedScriptFinding("bit-function", UnifiedScriptSeverity.Warning, call.Line, $"{function} on the Bool '{tag}': it sets or clears bit {bit}, it does not take a value, so a 0 does not switch the tag off; write HMIRuntime.Tags.SysFct.SetTagValue(\"{tag}\", 1) or (\"{tag}\", 0)");
            }

            return BitWidths.TryGetValue(dataType, out var width) && bit >= width
                ? new UnifiedScriptFinding("bit-function", UnifiedScriptSeverity.Error, call.Line, $"{function}: '{tag}' is a {dataType} with bits 0 to {width - 1}, not {bit}")
                : null;
        }

        private static IEnumerable<UnifiedScriptFinding> MissingTags(IReadOnlyList<UnifiedScriptToken> tokens, UnifiedScriptContext context)
        {
            for (var index = 0; index + 2 < tokens.Count; index++)
            {
                var callsTag = tokens[index].IsName("Tags") || (index > 0 && tokens[index].Kind == UnifiedScriptTokenKind.Identifier && TagFunctions.Contains(tokens[index].Text) && tokens[index - 1].Is("."));
                if (!callsTag || !tokens[index + 1].Is("(") || tokens[index + 2].Kind != UnifiedScriptTokenKind.StringLiteral)
                {
                    continue;
                }

                var tag = tokens[index + 2].Text;
                if (!context.TagTypes.ContainsKey(tag) && !context.TagTypes.ContainsKey(BaseName(tag)))
                {
                    yield return new UnifiedScriptFinding("missing-tag", UnifiedScriptSeverity.Error, tokens[index].Line, $"tag '{tag}' does not exist on this HMI");
                }
            }
        }

        /// <summary>A structured tag's name without its member or element, e.g. <c>Motor</c> for <c>Motor.Speed</c> or <c>Motor[2]</c>.</summary>
        private static string BaseName(string tag)
        {
            var end = tag.IndexOfAny(new[] { '.', '[' });

            return end > 0 ? tag.Substring(0, end) : tag;
        }
    }
}
