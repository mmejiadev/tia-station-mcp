using System;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A caller's name filter, compiled safely or refused.
    /// </summary>
    /// <remarks>
    /// Several tools take a <c>regexName</c> and hand it straight to <see cref="Regex"/>. Two things
    /// went wrong with that, and both were found in the audit of 2026-09-02.
    ///
    /// **A pattern with no match timeout can hang the server.** Backtracking on an expression like
    /// <c>(a+)+$</c> takes exponential time, and these filters run inside the Openness gate — so one
    /// bad pattern does not slow a listing down, it stops every other tool in the process from
    /// reaching TIA Portal at all, with no error and nothing in the log. The timeout below turns
    /// that into a refusal with a reason.
    ///
    /// **An invalid pattern was reported as the wrong kind of failure.** <c>new Regex("[")</c>
    /// throws <see cref="ArgumentException"/>, which reached the portal layer's decoration point and
    /// came back as an operation failure — telling the caller the environment broke and to retry,
    /// when what actually happened is that they typed a bracket wrong. It is
    /// <see cref="PortalErrorCode.InvalidParams"/>, which is what this throws.
    /// </remarks>
    public sealed class NameFilter
    {
        /// <summary>
        /// How long a single match may take.
        /// </summary>
        /// <remarks>
        /// One second, against names that are at most a few dozen characters: no honest filter comes
        /// anywhere near it, and a pattern that does is pathological rather than slow. It is a
        /// ceiling on damage, not a performance budget.
        /// </remarks>
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(1);

        private readonly Regex? _pattern;

        private NameFilter(Regex? pattern)
        {
            _pattern = pattern;
        }

        /// <summary>Reads a filter from what the caller supplied.</summary>
        /// <param name="pattern">The expression, or empty for "everything".</param>
        /// <returns>The filter. An empty pattern matches every name.</returns>
        /// <exception cref="PortalException">The expression is not a valid one.</exception>
        /// <remarks>
        /// An empty filter matching everything is the behaviour every caller already relied on, and
        /// it is stated here once instead of as a null check at each of them.
        /// </remarks>
        public static NameFilter Parse(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return new NameFilter(null);
            }

            try
            {
                return new NameFilter(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Patience));
            }
            catch (ArgumentException failure)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{pattern}' is not a valid name filter: {failure.Message}",
                    null,
                    failure);
            }
        }

        /// <summary>Whether a name passes the filter.</summary>
        /// <param name="name">The name to test.</param>
        /// <returns>True when it matches, and always true when no pattern was given.</returns>
        /// <exception cref="PortalException">The pattern took too long on this name.</exception>
        /// <remarks>
        /// The timeout arrives as an exception rather than as "no match" on purpose. A filter that
        /// silently dropped the names it could not decide about would return a shorter list that
        /// looks complete, which for an export is the worst possible answer.
        /// </remarks>
        public bool Matches(string name)
        {
            if (_pattern == null)
            {
                return true;
            }

            try
            {
                return _pattern.IsMatch(name ?? string.Empty);
            }
            catch (RegexMatchTimeoutException failure)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The name filter '{_pattern}' took longer than {Patience.TotalSeconds:0} s on '{name}'. " +
                    "Simplify it: a filter that backtracks like this blocks every other operation.",
                    null,
                    failure);
            }
        }
    }
}
