using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>A WinCC Unified text is stored as XHTML: plain text is wrapped going in and unwrapped coming out.</remarks>
    [TestClass]
    public sealed class UnifiedRichTextTests
    {
        [TestMethod]
        [DataRow("Tank 1", "<body><p>Tank 1</p></body>")]
        [DataRow("A < B & C", "<body><p>A &lt; B &amp; C</p></body>")]
        [DataRow("<body><p><b>Bold</b></p></body>", "<body><p><b>Bold</b></p></body>")]
        public void Format_Text_GivesWhatTiaPortalStores(string text, string expected)
        {
            Assert.AreEqual(expected, UnifiedRichText.Format(text));
        }

        [TestMethod]
        [DataRow("Tank 1")]
        [DataRow("A < B & C")]
        [DataRow("")]
        public void Read_FormattedPlainText_GivesThePlainTextBack(string text)
        {
            Assert.AreEqual(text, UnifiedRichText.Read(UnifiedRichText.Format(text)));
        }

        [TestMethod]
        [DataRow("<body><p><b>Bold</b></p></body>")]
        [DataRow("<body><p>One</p><p>Two</p></body>")]
        [DataRow("<body><p>&lt;body&gt;looks formatted</p></body>")]
        public void Read_AnythingButOnePlainParagraph_IsKeptAsStoredSoItIsWrittenBackUnchanged(string stored)
        {
            var read = UnifiedRichText.Read(stored);

            Assert.AreEqual(stored, UnifiedRichText.Format(read));
        }
    }
}
