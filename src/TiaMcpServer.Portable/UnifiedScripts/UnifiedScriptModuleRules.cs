using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>What a global script module must be before it reaches TIA Portal, and the files TIA Portal imports it from.</summary>
    /// <remarks>
    /// Measured 2026-11-08 on TIA Portal V20: a module is exported as <c>NAME.hmi.yml</c> and <c>NAME.hmi.js</c>, and
    /// <c>Import</c> refuses a folder without the YAML. Importing a module of a name already there replaces it without a word,
    /// so replacing has to be asked for; and JavaScript that does not parse is imported and compiles with no error, so
    /// the code is checked first. TIA Portal also rebuilds what it imports: every top-level function comes back exported,
    /// in its order, and anything else moves up to the module's global definition area, in its order, above a comment it
    /// generates. The code is read token by token, as it is data from outside (see <see cref="UnifiedScriptLexer"/>).
    /// </remarks>
    public static class UnifiedScriptModuleRules
    {
        /// <summary>The ending of a module's JavaScript file, as TIA Portal writes it.</summary>
        public const string ScriptExtension = ".hmi.js";

        /// <summary>The ending of a module's YAML file, as TIA Portal writes it.</summary>
        public const string DescriptionExtension = ".hmi.yml";

        // The name is written unquoted as a YAML key and as part of a file name.
        private static readonly Regex ValidName = new Regex(@"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant);

        // Words a YAML parser may read as something other than a string, and device names Windows will not create a file for.
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "null", "true", "false", "yes", "no", "on", "off", "y", "n",
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        /// <summary>Throws unless the write can be attempted.</summary>
        /// <param name="write">The write.</param>
        /// <param name="existing">The names of the modules the HMI holds.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> for a missing write, a name a YAML key or file cannot hold, code that is empty or too long, or an export TIA Portal breaks;
        /// <see cref="PortalErrorCode.InvalidState"/> for a module already there when replacing was not asked for.
        /// </exception>
        public static void Check(UnifiedScriptModuleWrite write, IEnumerable<string> existing)
        {
            if (write == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The module is required: its name and its code");
            }

            if (!ValidName.IsMatch(write.Name) || ReservedNames.Contains(write.Name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{write.Name}' cannot name a script module: use letters, digits and '_', not starting with a digit, and no word YAML or Windows reserves");
            }

            if (string.IsNullOrWhiteSpace(write.Code) || write.Code.Length > UnifiedScriptChecker.MaxLength)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The code of '{write.Name}' must be between 1 and {UnifiedScriptChecker.MaxLength} characters");
            }

            if (HasExportTiaPortalBreaks(write.Code))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The code of '{write.Name}' uses 'export default' or an export list, which TIA Portal breaks on import. Write 'export function name(...)': every top-level function comes out exported.");
            }

            if (!write.Replace && Holds(existing, write.Name))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The HMI already has a script module '{write.Name}', and TIA Portal would replace it without a word; nothing was changed. Ask to replace it if that is what you mean.");
            }
        }

        /// <summary>Whether the HMI holds a module of a name, case aside.</summary>
        /// <param name="existing">The names of the modules the HMI holds.</param>
        /// <param name="name">The name.</param>
        /// <returns>True when it does.</returns>
        public static bool Holds(IEnumerable<string> existing, string name)
        {
            return (existing ?? Enumerable.Empty<string>()).Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The YAML file TIA Portal imports a module from.</summary>
        /// <param name="name">The module's name, already checked.</param>
        /// <returns>The text of <c>NAME.hmi.yml</c>, in the form TIA Portal exports it.</returns>
        public static string Description(string name)
        {
            return $"#Version: 2.0\r\n\r\nScriptModules:\r\n  {name}:\r\n    ScriptFile: {name}{ScriptExtension}\r\n";
        }

        /// <summary>What the change plan and the audit trail say about a write, as the code is too long to carry.</summary>
        /// <param name="write">The write.</param>
        /// <returns>Whether it may replace a module, the code's length and its SHA-256.</returns>
        public static string Summary(UnifiedScriptModuleWrite write)
        {
            if (write == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The module is required: its name and its code");
            }

            using var sha256 = SHA256.Create();
            var hash = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(write.Code))).Replace("-", string.Empty);

            return $"{(write.Replace ? "replacing it if it is there" : "a new module")}, {write.Code.Length} characters, SHA-256 {hash}";
        }

        /// <summary>The module's code as the body of a script, the only place TIA Portal's syntax check reads.</summary>
        /// <param name="code">The module's JavaScript.</param>
        /// <returns>The code with its imports and each <c>export</c> blanked, as a function body may hold neither; line numbers do not move.</returns>
        public static string SyntaxCheckBody(string code)
        {
            var body = (code ?? string.Empty).ToCharArray();
            var tokens = UnifiedScriptLexer.Tokenize(code ?? string.Empty);
            var depths = DepthsOf(tokens);

            for (var index = 0; index < tokens.Count; index++)
            {
                if (IsTopLevelName(tokens, depths, index, "export"))
                {
                    Blank(body, tokens[index].Start, tokens[index].End);
                }
                else if (IsImportStatement(tokens, depths, index))
                {
                    Blank(body, tokens[index].Start, tokens[EndOfImport(tokens, index)].End);
                }
            }

            return new string(body);
        }

        /// <summary>Whether TIA Portal holds the code asked for, given what it does on import.</summary>
        /// <param name="exported">The module's JavaScript read back.</param>
        /// <param name="code">The code written.</param>
        /// <returns>True when both have the same functions in the same order and the same other statements in the same order, each token for token.</returns>
        public static bool HoldsCode(string exported, string code)
        {
            var held = ShapeOf(exported);
            var asked = ShapeOf(code);

            return held.Definitions.SequenceEqual(asked.Definitions) && held.Functions.SequenceEqual(asked.Functions);
        }

        private static bool HasExportTiaPortalBreaks(string code)
        {
            var tokens = UnifiedScriptLexer.Tokenize(code);
            var depths = DepthsOf(tokens);

            return Enumerable.Range(0, Math.Max(0, tokens.Count - 1)).Any(index => IsTopLevelName(tokens, depths, index, "export")
                && (tokens[index + 1].IsName("default") || tokens[index + 1].Is("{") || tokens[index + 1].Is("*")));
        }

        /// <remarks>Measured 2026-11-08: "export default" comes back as "default ...", which does not parse, and an export list stays beside the "export" TIA Portal puts on the function, exporting it twice.</remarks>
        private static (List<string> Definitions, List<string> Functions) ShapeOf(string text)
        {
            var tokens = UnifiedScriptLexer.Tokenize(text);
            var depths = DepthsOf(tokens);
            var shape = (Definitions: new List<string>(), Functions: new List<string>());

            for (var index = 0; index < tokens.Count; index++)
            {
                if (IsTopLevelName(tokens, depths, index, "export") && index + 1 < tokens.Count && StartsFunction(tokens, depths, index + 1))
                {
                    continue;
                }

                if (!StartsFunction(tokens, depths, index))
                {
                    shape.Definitions.Add(WordOf(tokens[index]));
                    continue;
                }

                var end = EndOfFunction(tokens, depths, index);
                shape.Functions.Add(string.Join(" ", tokens.Skip(index).Take(end - index + 1).Select(WordOf)));
                index = end;
            }

            return shape;
        }

        private static string WordOf(UnifiedScriptToken token)
        {
            return $"{token.Kind}:{token.Text}";
        }

        /// <remarks>An opening bracket and its closing one carry the depth around them.</remarks>
        private static int[] DepthsOf(IReadOnlyList<UnifiedScriptToken> tokens)
        {
            var depths = new int[tokens.Count];
            var depth = 0;

            for (var index = 0; index < tokens.Count; index++)
            {
                depth -= tokens[index].Is("}") || tokens[index].Is(")") || tokens[index].Is("]") ? 1 : 0;
                depths[index] = depth;
                depth += tokens[index].Is("{") || tokens[index].Is("(") || tokens[index].Is("[") ? 1 : 0;
            }

            return depths;
        }

        private static bool IsTopLevelName(IReadOnlyList<UnifiedScriptToken> tokens, int[] depths, int index, string name)
        {
            return depths[index] == 0 && tokens[index].IsName(name) && (index == 0 || !tokens[index - 1].Is("."));
        }

        private static bool IsImportStatement(IReadOnlyList<UnifiedScriptToken> tokens, int[] depths, int index)
        {
            return IsTopLevelName(tokens, depths, index, "import")
                && index + 1 < tokens.Count
                && !tokens[index + 1].Is("(")
                && !tokens[index + 1].Is(".");
        }

        /// <returns>The index of the token ending the statement: its module's name, or the semicolon after it.</returns>
        private static int EndOfImport(IReadOnlyList<UnifiedScriptToken> tokens, int start)
        {
            var end = start;
            while (end < tokens.Count && tokens[end].Kind != UnifiedScriptTokenKind.StringLiteral)
            {
                end++;
            }

            if (end == tokens.Count)
            {
                return start;
            }

            return end + 1 < tokens.Count && tokens[end + 1].Is(";") ? end + 1 : end;
        }

        /// <remarks>A <c>function</c> after an operator or an opening bracket is an expression, which TIA Portal leaves among the definitions.</remarks>
        private static bool StartsFunction(IReadOnlyList<UnifiedScriptToken> tokens, int[] depths, int index)
        {
            var isFunction = IsTopLevelName(tokens, depths, index, "function")
                || (IsTopLevelName(tokens, depths, index, "async") && index + 1 < tokens.Count && tokens[index + 1].IsName("function"));

            return isFunction && (index == 0 || !IsInsideAnExpression(tokens[index - 1]));
        }

        private static bool IsInsideAnExpression(UnifiedScriptToken previous)
        {
            return previous.Kind == UnifiedScriptTokenKind.Punctuation && !previous.Is(";") && !previous.Is("}") && !previous.Is(")") && !previous.Is("]");
        }

        /// <returns>The index of the closing brace of its body; a brace in a default parameter is deeper, inside the brackets.</returns>
        private static int EndOfFunction(IReadOnlyList<UnifiedScriptToken> tokens, int[] depths, int start)
        {
            var end = start;
            while (end < tokens.Count && !(tokens[end].Is("}") && depths[end] == 0))
            {
                end++;
            }

            return Math.Min(end, tokens.Count - 1);
        }

        private static void Blank(char[] text, int start, int end)
        {
            for (var index = start; index < end; index++)
            {
                text[index] = text[index] == '\r' || text[index] == '\n' ? text[index] : ' ';
            }
        }
    }
}
