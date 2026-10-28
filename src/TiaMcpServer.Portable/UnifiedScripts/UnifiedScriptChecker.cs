using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>Finds faults in a WinCC Unified script that TIA Portal compiles without a word.</summary>
    /// <remarks>
    /// The script is read, never run (see <see cref="UnifiedScriptLexer"/>). A script past
    /// <see cref="MaxLength"/> characters is not read at all and says so: a screen script that long is not
    /// one a person wrote, and reading it is not this tool's place.
    /// </remarks>
    public static class UnifiedScriptChecker
    {
        /// <summary>The longest script read.</summary>
        public const int MaxLength = 100_000;

        /// <summary>Checks one script.</summary>
        /// <param name="script">The script.</param>
        /// <param name="context">The HMI's tags; <see cref="UnifiedScriptContext.None"/> for none.</param>
        /// <param name="globals">
        /// The global definitions the script runs with, or null: what they declare is known to the script (a
        /// <c>not</c> defined there, a tag held there), but only the script is reported on. Check the
        /// definitions themselves as a script of their own.
        /// </param>
        /// <returns>The findings, by line.</returns>
        public static IReadOnlyList<UnifiedScriptFinding> Check(string script, UnifiedScriptContext context, string? globals = null)
        {
            var given = script ?? string.Empty;
            var prelude = globals ?? string.Empty;
            if (given.Length > MaxLength || prelude.Length > MaxLength)
            {
                return new[] { new UnifiedScriptFinding("not-checked", UnifiedScriptSeverity.Warning, 1, $"the script or its global definitions have more than the {MaxLength} characters this tool reads; it was not checked") };
            }

            var tokens = UnifiedScriptLexer.Tokenize(given);

            return UnifiedScriptSyntaxRules.Check(tokens, UnifiedScriptLexer.Tokenize(prelude))
                .Concat(UnifiedScriptTagRules.Check(tokens, context ?? UnifiedScriptContext.None))
                .GroupBy(finding => (finding.Rule, finding.Line, finding.Message))
                .Select(group => group.First())
                .OrderBy(finding => finding.Line)
                .ToList();
        }
    }
}
