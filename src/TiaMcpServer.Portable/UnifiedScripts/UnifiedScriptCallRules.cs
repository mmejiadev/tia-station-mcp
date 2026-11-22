using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>The rules that check a script's use of the HMI's global script modules.</summary>
    /// <remarks>
    /// The TIA Portal V21 help ("Script and execution context", "Global modules"): a script reaches a module only
    /// through an <c>import</c> in its global definition area, <c>import * as modA from 'ModuleA';</c>, and calls its
    /// functions as <c>modA.function1()</c>. Measured 2026-11-18 on TIA Portal V21: with a module <c>Lib</c> exporting
    /// <c>f</c>, buttons calling <c>Lib.g(1)</c>, <c>Nope.f(1)</c> and <c>f(1)</c>, none importing anything, all
    /// compiled with no error and no warning - so every one of these mistakes reaches runtime unseen.
    /// Only names this reader can tie to a module are judged: any other <c>x.f()</c> is a call on an object it cannot know.
    /// </remarks>
    internal static class UnifiedScriptCallRules
    {
        /// <summary>Finds imports of modules the HMI lacks, calls to functions a module lacks, and modules called without an import.</summary>
        /// <param name="tokens">The script's tokens; only these are reported on.</param>
        /// <param name="prelude">Its global definitions' tokens, whose imports the script uses.</param>
        /// <param name="context">The HMI's modules; when not known, nothing is found.</param>
        /// <returns>The findings.</returns>
        public static IEnumerable<UnifiedScriptFinding> Check(IReadOnlyList<UnifiedScriptToken> tokens, IReadOnlyList<UnifiedScriptToken> prelude, UnifiedScriptContext context)
        {
            if (!context.KnowsModules)
            {
                return Enumerable.Empty<UnifiedScriptFinding>();
            }

            var aliases = UnifiedScriptImports.Read(prelude).Concat(UnifiedScriptImports.Read(tokens)).ToList();
            var declared = Declared(prelude).Concat(Declared(tokens)).ToList();

            return UnifiedScriptImports.Read(tokens).SelectMany(import => ImportProblems(import, context))
                .Concat(CallProblems(tokens, aliases, (context, declared)));
        }

        private static IEnumerable<UnifiedScriptFinding> ImportProblems(UnifiedScriptImport import, UnifiedScriptContext context)
        {
            if (!context.ModuleFunctions.TryGetValue(import.Module, out var functions))
            {
                // A warning: modules are written one at a time, and the one imported may simply come next.
                yield return new UnifiedScriptFinding("missing-module", UnifiedScriptSeverity.Warning, import.Line, $"the HMI has no script module '{import.Module}' to import, so this import fails in runtime unless it is written; it has: {List(context.ModuleFunctions.Keys)}");
                yield break;
            }

            if (import.Function != null && !functions.Contains(import.Function))
            {
                yield return Missing(import.Module, import.Function, import.Line, functions);
            }
        }

        /// <summary>The names a script declares - after <c>let</c>, <c>const</c>, <c>var</c>, <c>class</c>, and the parameters of its functions.</summary>
        /// <remarks>Review of 2026-11-18: <c>const Motor = {...}; Motor.start()</c> beside a module Motor is a call on the local, not on the module.</remarks>
        private static IEnumerable<string> Declared(IReadOnlyList<UnifiedScriptToken> tokens)
        {
            for (var index = 1; index < tokens.Count; index++)
            {
                var before = tokens[index - 1];
                if (tokens[index].Kind == UnifiedScriptTokenKind.Identifier && (before.IsName("let") || before.IsName("const") || before.IsName("var") || before.IsName("class")))
                {
                    yield return tokens[index].Text;
                }
            }

            foreach (var parameter in Parameters(tokens))
            {
                yield return parameter;
            }
        }

        /// <returns>The names between the parentheses after each <c>function</c> or <c>function name</c>.</returns>
        private static IEnumerable<string> Parameters(IReadOnlyList<UnifiedScriptToken> tokens)
        {
            return Enumerable.Range(0, tokens.Count).Where(index => tokens[index].IsName("function")).SelectMany(index => ParametersAt(tokens, index));
        }

        private static IEnumerable<string> ParametersAt(IReadOnlyList<UnifiedScriptToken> tokens, int function)
        {
            var open = function + 1 < tokens.Count && tokens[function + 1].Kind == UnifiedScriptTokenKind.Identifier ? function + 2 : function + 1;
            if (open >= tokens.Count || !tokens[open].Is("("))
            {
                yield break;
            }

            for (var at = open + 1; at < tokens.Count && !tokens[at].Is(")"); at++)
            {
                if (tokens[at].Kind == UnifiedScriptTokenKind.Identifier)
                {
                    yield return tokens[at].Text;
                }
            }
        }

        private static IEnumerable<UnifiedScriptFinding> CallProblems(IReadOnlyList<UnifiedScriptToken> tokens, IReadOnlyList<UnifiedScriptImport> imports, (UnifiedScriptContext Context, IReadOnlyList<string> Declared) known)
        {
            var context = known.Context;
            var namespaces = imports.Where(import => import.Function == null).GroupBy(import => import.Alias, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().Module, StringComparer.Ordinal);

            for (var index = 0; index + 3 < tokens.Count; index++)
            {
                if (MemberCall(tokens, index) is (string owner, UnifiedScriptToken function) && !known.Declared.Contains(owner)
                    && CallProblem(owner, function, namespaces, context) is UnifiedScriptFinding problem)
                {
                    yield return problem;
                }
            }
        }

        /// <returns>What is wrong with <c>owner.function(</c>: a function its imported module lacks, or a module called by its own name without an import; null when neither.</returns>
        private static UnifiedScriptFinding? CallProblem(string owner, UnifiedScriptToken function, Dictionary<string, string> namespaces, UnifiedScriptContext context)
        {
            if (namespaces.TryGetValue(owner, out var module))
            {
                return context.ModuleFunctions.TryGetValue(module, out var functions) && !functions.Contains(function.Text)
                    ? Missing(module, function.Text, function.Line, functions)
                    : null;
            }

            return context.ModuleFunctions.ContainsKey(owner)
                ? new UnifiedScriptFinding(
                    "module-not-imported",
                    UnifiedScriptSeverity.Error,
                    function.Line,
                    $"'{owner}' is a script module, but this script does not import it, so '{owner}.{function.Text}' is undefined in runtime; add to its global definitions: import * as {owner} from '{owner}';")
                : null;
        }

        /// <returns>The owner and the function of <c>owner.function(</c> at this token, when nothing comes before the owner.</returns>
        private static (string Owner, UnifiedScriptToken Function)? MemberCall(IReadOnlyList<UnifiedScriptToken> tokens, int index)
        {
            var isCall = tokens[index].Kind == UnifiedScriptTokenKind.Identifier && tokens[index + 1].Is(".")
                && tokens[index + 2].Kind == UnifiedScriptTokenKind.Identifier && tokens[index + 3].Is("(")
                && !(index > 0 && tokens[index - 1].Is("."));

            return isCall ? (tokens[index].Text, tokens[index + 2]) : ((string, UnifiedScriptToken)?)null;
        }

        private static UnifiedScriptFinding Missing(string module, string function, int line, IReadOnlyCollection<string> offered)
        {
            return new UnifiedScriptFinding(
                "missing-module-function",
                UnifiedScriptSeverity.Error,
                line,
                $"the script module '{module}' has no function '{function}', and TIA Portal compiles this without a word; it has: {List(offered)}");
        }

        private static string List(IEnumerable<string> names)
        {
            var sorted = names.OrderBy(name => name, StringComparer.Ordinal).ToList();

            return sorted.Count == 0 ? "none" : string.Join(", ", sorted);
        }
    }
}
