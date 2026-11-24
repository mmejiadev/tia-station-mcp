using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Scl
{
    /// <summary>One PEEK or POKE call found in an SCL source, with its arguments as written.</summary>
    internal sealed class SclMemoryAccessCall
    {
        /// <summary>The most digits a literal is read with, per radix: a DWORD's, so the value is never negative.</summary>
        private static readonly Dictionary<string, (int Radix, int Digits)> Radixes = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["hex"] = (16, 8),
            ["oct"] = (8, 11),
            ["bin"] = (2, 32),
            ["dec"] = (10, 10)
        };

        private static readonly Regex Literal = new Regex(@"^(?:[A-Z]+#)?(?:16#(?<hex>[0-9A-F_]+)|8#(?<oct>[0-7_]+)|2#(?<bin>[01_]+)|(?<dec>[0-9][0-9_]*))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly IReadOnlyDictionary<string, string> _arguments;

        /// <summary>Creates a call.</summary>
        /// <param name="name">The instruction, upper case, e.g. <c>PEEK_WORD</c>.</param>
        /// <param name="line">The line it starts on.</param>
        /// <param name="arguments">Each argument's text by its parameter's name, upper case; an output given as <c>=&gt;</c>.</param>
        public SclMemoryAccessCall(string name, int line, IReadOnlyDictionary<string, string> arguments)
        {
            Name = name;
            Line = line;
            _arguments = arguments;
        }

        /// <summary>The instruction.</summary>
        public string Name { get; }

        /// <summary>The line it starts on.</summary>
        public int Line { get; }

        /// <summary>An argument's text, or null when the call does not give it.</summary>
        /// <param name="parameter">The parameter's name, upper case.</param>
        /// <returns>The text.</returns>
        public string? Value(string parameter)
        {
            return _arguments.TryGetValue(parameter, out var value) ? value : null;
        }

        /// <summary>An argument's value, when it is an integer literal written alone.</summary>
        /// <param name="parameter">The parameter's name, upper case.</param>
        /// <returns>
        /// The value of <c>16#84</c>, <c>B#16#84</c>, <c>2#1000_0100</c>, <c>132</c>; null for anything else, an expression
        /// holding a literal included, and when the call does not give the argument.
        /// </returns>
        public long? Constant(string parameter)
        {
            var match = Literal.Match(Value(parameter)?.Trim() ?? string.Empty);
            var group = Radixes.FirstOrDefault(radix => match.Groups[radix.Key].Success);
            if (!match.Success || group.Key == null)
            {
                return null;
            }

            var digits = match.Groups[group.Key].Value.Replace("_", string.Empty);

            return digits.Length == 0 || digits.Length > group.Value.Digits
                ? null
                : group.Value.Radix == 10 ? long.Parse(digits, CultureInfo.InvariantCulture) : Convert.ToInt64(digits, group.Value.Radix);
        }

        /// <summary>A finding that this call is wrong.</summary>
        /// <param name="rule">The rule.</param>
        /// <param name="message">Why.</param>
        /// <returns>The finding.</returns>
        public SclFinding Error(string rule, string message)
        {
            return new SclFinding(rule, true, Line, $"{Name}: {message}");
        }

        /// <summary>A finding that this call may not do what it seems.</summary>
        /// <param name="rule">The rule.</param>
        /// <param name="message">Why.</param>
        /// <returns>The finding.</returns>
        public SclFinding Warning(string rule, string message)
        {
            return new SclFinding(rule, false, Line, $"{Name}: {message}");
        }
    }
}
