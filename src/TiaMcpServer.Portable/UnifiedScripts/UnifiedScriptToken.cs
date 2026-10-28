namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>One token of a WinCC Unified script, with the line it is on.</summary>
    public sealed class UnifiedScriptToken
    {
        /// <summary>Creates a token.</summary>
        /// <param name="kind">What it is.</param>
        /// <param name="text">Its text; a string's content.</param>
        /// <param name="line">The line it starts on, from 1.</param>
        public UnifiedScriptToken(UnifiedScriptTokenKind kind, string text, int line)
        {
            Kind = kind;
            Text = text;
            Line = line;
        }

        /// <summary>What it is.</summary>
        public UnifiedScriptTokenKind Kind { get; }

        /// <summary>Its text.</summary>
        public string Text { get; }

        /// <summary>The line it starts on.</summary>
        public int Line { get; }

        /// <summary>Whether it is this punctuation.</summary>
        /// <param name="punctuation">E.g. <c>(</c>.</param>
        /// <returns>True when it is.</returns>
        public bool Is(string punctuation)
        {
            return Kind == UnifiedScriptTokenKind.Punctuation && Text == punctuation;
        }

        /// <summary>Whether it is this name.</summary>
        /// <param name="name">E.g. <c>if</c>.</param>
        /// <returns>True when it is.</returns>
        public bool IsName(string name)
        {
            return Kind == UnifiedScriptTokenKind.Identifier && Text == name;
        }
    }
}
