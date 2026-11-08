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

        private UnifiedScriptToken(UnifiedScriptToken token, int start, int end)
            : this(token.Kind, token.Text, token.Line)
        {
            Start = start;
            End = end;
        }

        /// <summary>What it is.</summary>
        public UnifiedScriptTokenKind Kind { get; }

        /// <summary>Its text.</summary>
        public string Text { get; }

        /// <summary>The line it starts on.</summary>
        public int Line { get; }

        /// <summary>Where it starts in the script, as an offset; 0 unless the lexer placed it.</summary>
        public int Start { get; }

        /// <summary>Where it ends in the script, as the offset after its last character; 0 unless the lexer placed it.</summary>
        public int End { get; }

        /// <summary>This token, placed in the script.</summary>
        /// <param name="start">The offset it starts at.</param>
        /// <param name="end">The offset after its last character.</param>
        /// <returns>The placed token.</returns>
        public UnifiedScriptToken At(int start, int end)
        {
            return new UnifiedScriptToken(this, start, end);
        }

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
