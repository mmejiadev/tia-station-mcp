using System;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// One entry of an allow or deny list.
    /// </summary>
    /// <remarks>
    /// Matching is deliberately dull: literal text, with <c>*</c> standing for any run of
    /// characters. Not regular expressions — a policy file is read by people deciding what a
    /// machine may touch, and a regex is a language in which mistakes are easy and invisible.
    /// </remarks>
    public sealed class TargetPattern
    {
        private readonly Regex _matcher;

        private TargetPattern(string pattern, bool hasWildcard)
        {
            Pattern = pattern;
            HasWildcard = hasWildcard;
            _matcher = Compile(pattern);
        }

        /// <summary>The pattern as written in the policy.</summary>
        public string Pattern { get; }

        /// <summary>Whether it matches more than one literal target.</summary>
        public bool HasWildcard { get; }

        /// <summary>Reads a pattern.</summary>
        /// <param name="pattern">The text form, for example <c>PLC_0/Blocks/*</c>.</param>
        /// <returns>The pattern.</returns>
        /// <exception cref="ArgumentException"><paramref name="pattern"/> is empty.</exception>
        public static TargetPattern Parse(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("A policy pattern cannot be empty", nameof(pattern));
            }

            var trimmed = pattern.Trim();

            return new TargetPattern(trimmed, trimmed.IndexOf('*') >= 0);
        }

        /// <summary>Whether a target matches.</summary>
        /// <param name="target">The full path being written to.</param>
        /// <returns>True when it matches.</returns>
        public bool Matches(string target)
        {
            return !string.IsNullOrEmpty(target) && _matcher.IsMatch(target);
        }

        private static Regex Compile(string pattern)
        {
            // Everything escaped, then only the wildcard put back. Building the expression this
            // way means a pattern containing regex punctuation is treated as the literal text a
            // reader of the policy file would expect.
            var expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";

            return new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
