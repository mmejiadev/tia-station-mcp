using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Scl
{
    /// <summary>Words the PEEK and POKE findings of a source for the change plan, before it is written.</summary>
    /// <remarks>
    /// In Workshop Mode a supervisor confirms the plan, and the write runs only then: findings reported after the write
    /// reach nobody in time. So the rules that need only the text run before, and their findings travel in the plan's
    /// value, which the plan shows and the audit trail keeps. Rule and line only: the whole message of each would drown
    /// the trail, and the rule names what is wrong.
    /// </remarks>
    public static class SclMemoryAccessSummary
    {
        /// <summary>The findings as a suffix for the plan's value, or empty when there are none.</summary>
        /// <param name="findings">The findings of <see cref="SclMemoryAccessRules.CheckText"/>.</param>
        /// <returns>The suffix, for example <c> - PEEK/POKE: line 4 warning poke-writes-outputs</c>.</returns>
        public static string ForPlan(IReadOnlyList<SclFinding> findings)
        {
            if (findings == null || findings.Count == 0)
            {
                return string.Empty;
            }

            var described = findings.Select(finding => $"line {finding.Line} {(finding.IsError ? "error" : "warning")} {finding.Rule}");

            return $" - PEEK/POKE: {string.Join("; ", described)}";
        }
    }
}
