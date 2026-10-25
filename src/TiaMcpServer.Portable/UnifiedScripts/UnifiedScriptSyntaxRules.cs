using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>The rules that need only the script: what is valid JavaScript but does not do what it reads as.</summary>
    /// <remarks>
    /// Each rule comes from a fault found in the 2025-26 class final project (0968), read 2026-10-25: a
    /// button whose script wrote <c>if (man = true)</c> and set a local variable instead of its tag, and a
    /// visibility script calling <c>not(...)</c>, which JavaScript does not have.
    /// </remarks>
    internal static class UnifiedScriptSyntaxRules
    {
        private static readonly HashSet<string> Declarations = new HashSet<string> { "let", "const", "var", "function" };

        private static readonly HashSet<string> ConditionKeywords = new HashSet<string> { "if", "while" };

        /// <summary>Runs every rule on a script, knowing what its global definitions declare.</summary>
        /// <param name="tokens">The script's tokens.</param>
        /// <param name="prelude">The tokens of the global definitions it runs with; empty for none. Only the script is reported on.</param>
        /// <returns>The findings.</returns>
        public static IEnumerable<UnifiedScriptFinding> Check(IReadOnlyList<UnifiedScriptToken> tokens, IReadOnlyList<UnifiedScriptToken> prelude)
        {
            var declared = new HashSet<string>(Declared(prelude).Concat(Declared(tokens)));
            var heldTags = HeldTags(prelude);
            foreach (var held in HeldTags(tokens))
            {
                heldTags[held.Key] = held.Value;
            }

            return AssignmentsInConditions(tokens).Concat(ForeignWords(tokens, declared)).Concat(LocalWrites(tokens, heldTags));
        }

        private static IEnumerable<string> Declared(IReadOnlyList<UnifiedScriptToken> tokens)
        {
            for (var index = 1; index < tokens.Count; index++)
            {
                if (tokens[index].Kind == UnifiedScriptTokenKind.Identifier && tokens[index - 1].Kind == UnifiedScriptTokenKind.Identifier && Declarations.Contains(tokens[index - 1].Text))
                {
                    yield return tokens[index].Text;
                }
            }
        }

        /// <remarks>
        /// <c>if (a = b)</c> assigns and then tests the value assigned, so the branch runs whenever <c>b</c> is
        /// true. One pass with a stack of open brackets, so a script of thousands of unclosed <c>if (</c> is
        /// read in linear time: scanning ahead from each one for its closing bracket took 18 s on 100 000
        /// characters (measured in the test that guards it).
        /// </remarks>
        private static IEnumerable<UnifiedScriptFinding> AssignmentsInConditions(IReadOnlyList<UnifiedScriptToken> tokens)
        {
            // Whether each open bracket opens a condition, and the conditions still open: the bracket depth of
            // each, and whether it was reported. Only an '=' directly inside the condition's own brackets is
            // reported: one in more brackets, ((m = next()) !== null), or in a function passed in, is meant.
            var brackets = new Stack<bool>();
            var conditions = new Stack<(string Keyword, int Depth, bool Reported)>();

            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                if (token.Is("("))
                {
                    var isCondition = index > 0 && tokens[index - 1].Kind == UnifiedScriptTokenKind.Identifier && ConditionKeywords.Contains(tokens[index - 1].Text) && !After(tokens, index - 1, ".");
                    brackets.Push(isCondition);
                    if (isCondition)
                    {
                        conditions.Push((tokens[index - 1].Text, brackets.Count, false));
                    }
                }
                else if (token.Is(")") && brackets.Count > 0)
                {
                    if (brackets.Pop())
                    {
                        conditions.Pop();
                    }
                }
                else if (token.Is("=") && conditions.Count > 0 && !conditions.Peek().Reported && conditions.Peek().Depth == brackets.Count)
                {
                    var condition = conditions.Pop();
                    conditions.Push((condition.Keyword, condition.Depth, true));
                    yield return new UnifiedScriptFinding("assignment-in-condition", UnifiedScriptSeverity.Error, token.Line, $"'=' assigns inside the condition of '{condition.Keyword}', so the condition is the value assigned; compare with '===' (or '==')");
                }
            }
        }

        /// <remarks>Words from Structured Text or Basic that JavaScript spells as operators: <c>not</c>, <c>and</c>, <c>or</c>.</remarks>
        private static IEnumerable<UnifiedScriptFinding> ForeignWords(IReadOnlyList<UnifiedScriptToken> tokens, HashSet<string> declared)
        {
            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                if (token.Kind != UnifiedScriptTokenKind.Identifier || After(tokens, index, ".") || declared.Contains(token.Text))
                {
                    continue;
                }

                if (token.Text == "not" && index + 1 < tokens.Count && tokens[index + 1].Is("("))
                {
                    yield return new UnifiedScriptFinding("not-javascript", UnifiedScriptSeverity.Error, token.Line, "JavaScript has no not(...): the script fails when it runs; write !(...)");
                }

                if ((token.Text == "and" || token.Text == "or") && EndsOperand(tokens, index - 1) && StartsOperand(tokens, index + 1))
                {
                    yield return new UnifiedScriptFinding("not-javascript", UnifiedScriptSeverity.Error, token.Line, $"JavaScript has no '{token.Text}' operator: write '{(token.Text == "and" ? "&&" : "||")}'");
                }
            }
        }

        /// <summary>The variables that hold a tag object: <c>let p = Tags("X");</c> or <c>HMIRuntime.Tags("X")</c>, with nothing called on it.</summary>
        /// <remarks><c>let v = Tags("X").Read();</c> holds a value, which a script may change freely before writing it back.</remarks>
        private static Dictionary<string, string> HeldTags(IReadOnlyList<UnifiedScriptToken> tokens)
        {
            var held = new Dictionary<string, string>();
            for (var index = 0; index + 2 < tokens.Count; index++)
            {
                if (!(tokens[index].Kind == UnifiedScriptTokenKind.Identifier && Declarations.Contains(tokens[index].Text) && tokens[index + 1].Kind == UnifiedScriptTokenKind.Identifier && tokens[index + 2].Is("=")))
                {
                    continue;
                }

                var call = index + 3 + (index + 4 < tokens.Count && tokens[index + 3].IsName("HMIRuntime") && tokens[index + 4].Is(".") ? 2 : 0);
                if (call + 3 < tokens.Count && tokens[call].IsName("Tags") && tokens[call + 1].Is("(") && tokens[call + 2].Kind == UnifiedScriptTokenKind.StringLiteral && tokens[call + 3].Is(")")
                    && !(call + 4 < tokens.Count && tokens[call + 4].Is(".")))
                {
                    held[tokens[index + 1].Text] = tokens[call + 2].Text;
                }
            }

            return held;
        }

        /// <remarks>
        /// <c>let p = Tags("X");</c> then <c>p = true;</c> replaces the local variable and leaves the tag as it
        /// was: the 0968 robot button wrote nothing to the PLC this way.
        /// </remarks>
        private static IEnumerable<UnifiedScriptFinding> LocalWrites(IReadOnlyList<UnifiedScriptToken> tokens, Dictionary<string, string> heldTags)
        {
            for (var index = 0; index + 1 < tokens.Count; index++)
            {
                var token = tokens[index];
                if (token.Kind == UnifiedScriptTokenKind.Identifier && heldTags.TryGetValue(token.Text, out var tag) && tokens[index + 1].Is("=")
                    && !After(tokens, index, ".") && !(index > 0 && tokens[index - 1].Kind == UnifiedScriptTokenKind.Identifier && Declarations.Contains(tokens[index - 1].Text))
                    && !OpensCondition(tokens, index))
                {
                    yield return new UnifiedScriptFinding("local-variable-write", UnifiedScriptSeverity.Warning, token.Line, $"assigning to '{token.Text}' changes only the local variable, not tag '{tag}'; write the tag with Tags(\"{tag}\").Write(value) or HMIRuntime.Tags.SysFct.SetTagValue(\"{tag}\", value)");
                }
            }
        }

        /// <remarks><c>if (man = true)</c> is reported once, as an assignment in a condition, not again as a local write.</remarks>
        private static bool OpensCondition(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            return index > 1 && tokens[index - 1].Is("(") && tokens[index - 2].Kind == UnifiedScriptTokenKind.Identifier && ConditionKeywords.Contains(tokens[index - 2].Text);
        }

        private static bool After(IReadOnlyList<UnifiedScriptToken> tokens, int index, string punctuation)
        {
            return index > 0 && tokens[index - 1].Is(punctuation);
        }

        private static bool EndsOperand(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            return index >= 0 && (tokens[index].Kind != UnifiedScriptTokenKind.Punctuation || tokens[index].Is(")") || tokens[index].Is("]"));
        }

        private static bool StartsOperand(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            return index < tokens.Count && (tokens[index].Kind != UnifiedScriptTokenKind.Punctuation || tokens[index].Is("(") || tokens[index].Is("!"));
        }
    }
}
