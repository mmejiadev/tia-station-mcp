using System;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The lookup runs a separate process, and a block path is a string a person chose. Quoting it
    /// wrongly does not fail loudly: it asks a different question and quotes an excerpt about
    /// something else, which is worse than asking nothing. The expectations below are what
    /// <c>CommandLineToArgvW</c> parses back into the original arguments.
    /// </remarks>
    [TestClass]
    public class CommandLineTests
    {
        [TestMethod]
        public void Join_PlainArguments_LeavesThemUnquoted()
        {
            Assert.AreEqual("--format json", CommandLine.Join(new[] { "--format", "json" }));
        }

        [TestMethod]
        public void Join_ArgumentWithASpace_QuotesIt()
        {
            // The everyday case in this repository: TIA groups are named with spaces.
            Assert.AreEqual("\"Program blocks/FB_Station\"", CommandLine.Join(new[] { "Program blocks/FB_Station" }));
        }

        [TestMethod]
        public void Join_ArgumentWithAQuote_EscapesIt()
        {
            Assert.AreEqual("\"say \\\"hello\\\"\"", CommandLine.Join(new[] { "say \"hello\"" }));
        }

        [TestMethod]
        public void Join_TrailingBackslashBeforeTheClosingQuote_DoublesIt()
        {
            // The rule nobody remembers: a backslash is literal except immediately before a quote,
            // where it doubles. It only bites once something else has forced the argument to be
            // quoted, which on Windows is routine — "Program Files" brings both.
            Assert.AreEqual("\"C:\\Program Files\\\\\"", CommandLine.Join(new[] { @"C:\Program Files\" }));
        }

        [TestMethod]
        public void Join_TrailingBackslashWithNothingToQuote_IsLeftAlone()
        {
            // The counterpart, and the reason the test above needs a space in it: an unquoted
            // argument has no closing quote for a backslash to escape, so doubling it there would
            // change the path instead of preserving it.
            Assert.AreEqual(@"C:\backups\", CommandLine.Join(new[] { @"C:\backups\" }));
        }

        [TestMethod]
        public void Join_InteriorBackslashes_AreLeftAlone()
        {
            Assert.AreEqual(@"C:\backups\file.xml", CommandLine.Join(new[] { @"C:\backups\file.xml" }));
        }

        [TestMethod]
        public void Join_EmptyArgument_IsQuotedRatherThanDropped()
        {
            // Dropping it would shift every later argument by one position, which shows up as the
            // lookup asking the wrong question rather than as an error.
            Assert.AreEqual("--query \"\" --format", CommandLine.Join(new[] { "--query", string.Empty, "--format" }));
        }

        [TestMethod]
        public void Join_Null_Refuses()
        {
            Assert.ThrowsException<ArgumentNullException>(() => CommandLine.Join(null!));
        }
    }
}
