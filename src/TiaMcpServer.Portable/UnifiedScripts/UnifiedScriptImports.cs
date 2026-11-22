using System;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>Reads the <c>import</c> statements of a WinCC Unified script.</summary>
    /// <remarks>
    /// The forms the TIA Portal V21 help shows - <c>import * as modA from 'ModuleA';</c> - and the named one,
    /// <c>import { f, g as h } from 'M';</c>. <c>import X from 'M'</c> is read as the whole module under X. Anything else
    /// after <c>import</c> is not an import this reader knows, and is left alone.
    /// </remarks>
    internal static class UnifiedScriptImports
    {
        /// <summary>Every name the script's imports give it.</summary>
        /// <param name="tokens">The script's tokens.</param>
        /// <returns>The imports, in order.</returns>
        public static IReadOnlyList<UnifiedScriptImport> Read(IReadOnlyList<UnifiedScriptToken> tokens)
        {
            var imports = new List<UnifiedScriptImport>();

            for (var index = 0; index < tokens.Count; index++)
            {
                if (tokens[index].IsName("import"))
                {
                    imports.AddRange(ReadOne(tokens, index + 1, tokens[index].Line));
                }
            }

            return imports;
        }

        private static IEnumerable<UnifiedScriptImport> ReadOne(IReadOnlyList<UnifiedScriptToken> tokens, int index, int line)
        {
            if (At(tokens, index, "*") && Name(tokens, index + 1) == "as" && Name(tokens, index + 2) is string alias && Module(tokens, index + 3) is string module)
            {
                return new[] { new UnifiedScriptImport(alias, module, null, line) };
            }

            if (Name(tokens, index) is string whole && Module(tokens, index + 1) is string wholeModule)
            {
                return new[] { new UnifiedScriptImport(whole, wholeModule, null, line) };
            }

            return At(tokens, index, "{") ? Named(tokens, index + 1, line) : Array.Empty<UnifiedScriptImport>();
        }

        /// <remarks><c>{ f, g as h } from 'M'</c>, from just after the brace.</remarks>
        private static IEnumerable<UnifiedScriptImport> Named(IReadOnlyList<UnifiedScriptToken> tokens, int index, int line)
        {
            var names = new List<(string Function, string Alias)>();

            while (Name(tokens, index) is string function)
            {
                var hasAlias = Name(tokens, index + 1) == "as" && Name(tokens, index + 2) != null;
                names.Add((function, hasAlias ? Name(tokens, index + 2)! : function));
                index += hasAlias ? 3 : 1;
                index += At(tokens, index, ",") ? 1 : 0;
            }

            if (!At(tokens, index, "}") || !(Module(tokens, index + 1) is string module))
            {
                return Array.Empty<UnifiedScriptImport>();
            }

            return names.ConvertAll(name => new UnifiedScriptImport(name.Alias, module, name.Function, line));
        }

        /// <remarks><c>from 'M'</c> at this token.</remarks>
        private static string? Module(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            return Name(tokens, index) == "from" && index + 1 < tokens.Count && tokens[index + 1].Kind == UnifiedScriptTokenKind.StringLiteral
                ? tokens[index + 1].Text
                : null;
        }

        private static string? Name(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            return index < tokens.Count && tokens[index].Kind == UnifiedScriptTokenKind.Identifier ? tokens[index].Text : null;
        }

        private static bool At(IReadOnlyList<UnifiedScriptToken> tokens, int index, string punctuation)
        {
            return index < tokens.Count && tokens[index].Is(punctuation);
        }
    }
}
