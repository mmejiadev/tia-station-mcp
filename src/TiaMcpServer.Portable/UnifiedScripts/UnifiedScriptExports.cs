using System;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>The functions a WinCC Unified global script module offers the screens.</summary>
    /// <remarks>
    /// Measured 2026-11-08: TIA Portal exports every top-level function of a module it imports, written with
    /// <c>export</c> or not. So a module offers each function declared outside any brace - exported or not as written -
    /// each one written <c>export function</c>, each name written <c>export const|let|var name</c> (an arrow function,
    /// say), and each name an export list gives, <c>export { f, g as h }</c> (review of 2026-11-18).
    /// </remarks>
    public static class UnifiedScriptExports
    {
        /// <summary>The names of the functions a module's code offers.</summary>
        /// <param name="code">The module's JavaScript.</param>
        /// <returns>The function names, each once.</returns>
        public static IReadOnlyCollection<string> Functions(string code)
        {
            var tokens = UnifiedScriptLexer.Tokenize(code ?? string.Empty);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var depth = 0;

            for (var index = 0; index < tokens.Count; index++)
            {
                depth += tokens[index].Is("{") ? 1 : tokens[index].Is("}") ? -1 : 0;

                if (FunctionName(tokens, index) is string name && (depth == 0 || IsExported(tokens, index)))
                {
                    names.Add(name);
                }

                names.UnionWith(ExportedNames(tokens, index));
            }

            return names;
        }

        /// <returns>The name declared by <c>function name(</c> at this token, or null.</returns>
        private static string? FunctionName(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            return index + 2 < tokens.Count && tokens[index].IsName("function")
                && tokens[index + 1].Kind == UnifiedScriptTokenKind.Identifier && tokens[index + 2].Is("(")
                ? tokens[index + 1].Text
                : null;
        }

        /// <returns>The names <c>export const|let|var name</c> or <c>export { a, b as c }</c> at this token gives; none elsewhere.</returns>
        private static IEnumerable<string> ExportedNames(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            if (!tokens[index].IsName("export") || index + 2 >= tokens.Count)
            {
                return Array.Empty<string>();
            }

            var next = tokens[index + 1];
            if ((next.IsName("const") || next.IsName("let") || next.IsName("var")) && tokens[index + 2].Kind == UnifiedScriptTokenKind.Identifier)
            {
                return new[] { tokens[index + 2].Text };
            }

            return next.Is("{") ? ListedNames(tokens, index + 2) : Array.Empty<string>();
        }

        /// <remarks>From just after the brace: each name, or the alias after <c>as</c>, up to the closing brace.</remarks>
        private static List<string> ListedNames(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            var names = new List<string>();

            for (; index < tokens.Count && !tokens[index].Is("}"); index++)
            {
                var isAlias = index + 1 < tokens.Count && tokens[index + 1].IsName("as");
                if (tokens[index].Kind == UnifiedScriptTokenKind.Identifier && !tokens[index].IsName("as") && !isAlias)
                {
                    names.Add(tokens[index].Text);
                }
            }

            return names;
        }

        /// <returns>Whether <c>export</c>, or <c>export async</c>, comes right before the <c>function</c> at this token.</returns>
        private static bool IsExported(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            var before = index > 0 && tokens[index - 1].IsName("async") ? index - 2 : index - 1;

            return before >= 0 && tokens[before].IsName("export");
        }
    }
}
