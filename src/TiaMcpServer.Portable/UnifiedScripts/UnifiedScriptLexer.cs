using System.Collections.Generic;
using System.Text;

namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>Splits a WinCC Unified script (JavaScript) into tokens, comments dropped and strings read.</summary>
    /// <remarks>
    /// A script comes from a project, so it is data from outside: it is read one character at a time in a
    /// single pass, never run and never matched with a regular expression, so no script can make the
    /// reading slow. It is not a full JavaScript parser and does not try to be: a <c>/</c> starts a
    /// regular-expression literal where an operand may start (after an operator, a bracket or a keyword
    /// such as <c>return</c>) and is a division after an operand, which is how JavaScript itself tells
    /// them apart; the code inside a template's <c>${...}</c> is not read.
    /// </remarks>
    public static class UnifiedScriptLexer
    {
        /// <summary>Keywords after which a <c>/</c> starts a regular expression, not a division.</summary>
        private static readonly HashSet<string> BeforeOperand = new HashSet<string>
        {
            "return", "typeof", "case", "do", "else", "in", "of", "new", "delete", "void", "throw", "instanceof", "yield", "await"
        };

        private static readonly string[] Operators =
        {
            ">>>=", "===", "!==", "**=", "<<=", ">>=", ">>>", "...", "==", "!=", "<=", ">=", "=>", "&&", "||", "??",
            "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "?.", "<<", ">>", "**"
        };

        /// <summary>The tokens of a script.</summary>
        /// <param name="script">The script.</param>
        /// <returns>Every token, in order.</returns>
        public static IReadOnlyList<UnifiedScriptToken> Tokenize(string script)
        {
            var reader = new Reader(script ?? string.Empty);
            var tokens = new List<UnifiedScriptToken>();

            while (!reader.AtEnd)
            {
                var token = reader.Next();
                if (token != null)
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        /// <summary>The position in the script, and the line it is on.</summary>
        private sealed class Reader
        {
            private readonly string _text;
            private int _position;
            private int _line = 1;
            private UnifiedScriptToken? _last;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _position >= _text.Length;

            /// <summary>The next token, or null for whitespace or a comment.</summary>
            public UnifiedScriptToken? Next()
            {
                var token = Read();
                _last = token ?? _last;

                return token;
            }

            private UnifiedScriptToken? Read()
            {
                var current = _text[_position];

                if (char.IsWhiteSpace(current))
                {
                    Advance();
                    return null;
                }

                if (current == '/' && Peek(1) == '/')
                {
                    SkipLineComment();
                    return null;
                }

                if (current == '/' && Peek(1) == '*')
                {
                    SkipBlockComment();
                    return null;
                }

                if (current == '"' || current == '\'' || current == '`')
                {
                    return ReadString(current);
                }

                if (current == '/' && OperandMayStart())
                {
                    return ReadRegex();
                }

                if (char.IsLetter(current) || current == '_' || current == '$')
                {
                    return ReadWhile(UnifiedScriptTokenKind.Identifier, character => char.IsLetterOrDigit(character) || character == '_' || character == '$');
                }

                return char.IsDigit(current)
                    ? ReadWhile(UnifiedScriptTokenKind.Number, character => char.IsLetterOrDigit(character) || character == '.' || character == '_')
                    : ReadOperator();
            }

            private char Peek(int ahead)
            {
                return _position + ahead < _text.Length ? _text[_position + ahead] : '\0';
            }

            private void Advance()
            {
                if (_text[_position] == '\n')
                {
                    _line++;
                }

                _position++;
            }

            private void SkipLineComment()
            {
                while (!AtEnd && _text[_position] != '\n')
                {
                    Advance();
                }
            }

            private void SkipBlockComment()
            {
                _position += 2;
                while (!AtEnd && !(_text[_position] == '*' && Peek(1) == '/'))
                {
                    Advance();
                }

                _position = AtEnd ? _position : _position + 2;
            }

            private bool OperandMayStart()
            {
                return _last == null
                    || (_last.Kind == UnifiedScriptTokenKind.Punctuation && !_last.Is(")") && !_last.Is("]"))
                    || (_last.Kind == UnifiedScriptTokenKind.Identifier && BeforeOperand.Contains(_last.Text));
            }

            /// <remarks>Ends at the closing <c>/</c> outside a character class, or at the line's end; the flags after it are read too.</remarks>
            private UnifiedScriptToken ReadRegex()
            {
                var start = _position;
                var inClass = false;
                _position++;

                while (!AtEnd && _text[_position] != '\n' && (inClass || _text[_position] != '/'))
                {
                    inClass = _text[_position] == '[' || (inClass && _text[_position] != ']');
                    _position += _text[_position] == '\\' && _position + 1 < _text.Length && _text[_position + 1] != '\n' ? 2 : 1;
                }

                _position = !AtEnd && _text[_position] == '/' ? _position + 1 : _position;
                while (!AtEnd && char.IsLetter(_text[_position]))
                {
                    _position++;
                }

                return new UnifiedScriptToken(UnifiedScriptTokenKind.RegexLiteral, _text.Substring(start, _position - start), _line);
            }

            /// <remarks>An unterminated quote ends at the line's end, as JavaScript would refuse it; a template string may span lines.</remarks>
            private UnifiedScriptToken ReadString(char quote)
            {
                var line = _line;
                var content = new StringBuilder();
                Advance();

                while (!AtEnd && _text[_position] != quote && (quote == '`' || _text[_position] != '\n'))
                {
                    if (_text[_position] == '\\' && _position + 1 < _text.Length)
                    {
                        Advance();
                    }

                    content.Append(_text[_position]);
                    Advance();
                }

                if (!AtEnd && _text[_position] == quote)
                {
                    Advance();
                }

                var text = content.ToString();
                var kind = quote == '`' && text.Contains("${") ? UnifiedScriptTokenKind.TemplateLiteral : UnifiedScriptTokenKind.StringLiteral;

                return new UnifiedScriptToken(kind, text, line);
            }

            private UnifiedScriptToken ReadWhile(UnifiedScriptTokenKind kind, System.Func<char, bool> belongs)
            {
                var start = _position;
                while (!AtEnd && belongs(_text[_position]))
                {
                    _position++;
                }

                return new UnifiedScriptToken(kind, _text.Substring(start, _position - start), _line);
            }

            private UnifiedScriptToken ReadOperator()
            {
                foreach (var candidate in Operators)
                {
                    if (string.CompareOrdinal(_text, _position, candidate, 0, candidate.Length) == 0)
                    {
                        _position += candidate.Length;
                        return new UnifiedScriptToken(UnifiedScriptTokenKind.Punctuation, candidate, _line);
                    }
                }

                var single = _text[_position].ToString();
                Advance();

                return new UnifiedScriptToken(UnifiedScriptTokenKind.Punctuation, single, _line);
            }
        }
    }
}
