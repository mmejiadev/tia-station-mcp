using System.Collections.Generic;

namespace TiaMcpServer.Scl
{
    /// <summary>One PEEK or POKE call found in an SCL source, with its arguments as written.</summary>
    internal sealed class SclMemoryAccessCall
    {
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
