using System.Linq;
using TiaMcpServer.UnifiedScripts;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class UnifiedScriptLexerTests
    {
        private static readonly string[] Expected = { "if", "(", "a", "===", "b", ")", "x", "=", "Tags", "(", "Motor 1", ")" };

        [TestMethod]
        public void Tokenize_Script_DropsCommentsAndReadsStrings()
        {
            var tokens = UnifiedScriptLexer.Tokenize("if (a === b) // compare\r\n/* block\r\n comment */ x = Tags('Motor 1')");

            CollectionAssert.AreEqual(Expected, tokens.Select(token => token.Text).ToArray());
            Assert.AreEqual(UnifiedScriptTokenKind.StringLiteral, tokens.Last(token => token.Text == "Motor 1").Kind);
        }

        [TestMethod]
        public void Tokenize_LinesAfterABlockComment_AreCounted()
        {
            var tokens = UnifiedScriptLexer.Tokenize("a\r\n/* one\r\ntwo */\r\nb");

            Assert.AreEqual(4, tokens.Single(token => token.Text == "b").Line);
        }

        [TestMethod]
        public void Tokenize_RegexAfterAnOperator_IsOneToken()
        {
            var tokens = UnifiedScriptLexer.Tokenize("s = s.replace(/[\"`/]+/g, '');");

            Assert.AreEqual("/[\"`/]+/g", tokens.Single(token => token.Kind == UnifiedScriptTokenKind.RegexLiteral).Text);
        }

        [TestMethod]
        public void Tokenize_SlashAfterAnOperand_IsADivision()
        {
            var tokens = UnifiedScriptLexer.Tokenize("x = a / b / c;");

            Assert.IsFalse(tokens.Any(token => token.Kind == UnifiedScriptTokenKind.RegexLiteral));
        }

        [TestMethod]
        public void Tokenize_TemplateWithASubstitution_IsATemplateNotAString()
        {
            var tokens = UnifiedScriptLexer.Tokenize("Tags(`Station${i}`)");

            Assert.AreEqual(UnifiedScriptTokenKind.TemplateLiteral, tokens.Single(token => token.Text.StartsWith("Station", System.StringComparison.Ordinal)).Kind);
        }

        [TestMethod]
        public void Tokenize_EscapedQuote_StaysInTheString()
        {
            var tokens = UnifiedScriptLexer.Tokenize("Tags(\"It\\\"s\")");

            Assert.AreEqual("It\"s", tokens.Single(token => token.Kind == UnifiedScriptTokenKind.StringLiteral).Text);
        }
    }
}
