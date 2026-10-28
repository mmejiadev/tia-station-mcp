namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>What a token of a WinCC Unified script is.</summary>
    public enum UnifiedScriptTokenKind
    {
        /// <summary>A name: a variable, a function, a keyword.</summary>
        Identifier,

        /// <summary>A number as written.</summary>
        Number,

        /// <summary>A string literal; the token's text is its content, quotes and escapes resolved.</summary>
        StringLiteral,

        /// <summary>A template string with <c>${...}</c> in it: its value is computed, so it names no tag by itself.</summary>
        TemplateLiteral,

        /// <summary>A regular-expression literal, read whole so a quote inside it opens no string.</summary>
        RegexLiteral,

        /// <summary>An operator or a bracket.</summary>
        Punctuation
    }
}
