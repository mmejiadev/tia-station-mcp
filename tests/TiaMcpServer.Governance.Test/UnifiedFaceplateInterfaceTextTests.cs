using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The stored shapes were read back from TIA Portal V20 on 2026-10-30.</remarks>
    [TestClass]
    public sealed class UnifiedFaceplateInterfaceTextTests
    {
        private const string Stored = "<?xml version=\"1.0\" encoding=\"utf-8\"?><ProjectText xmlns=\"http://www.siemens.com/Industry/2009/10/01/Automation/FormattedText\"><body><p>Motor 1</p></body></ProjectText>";

        [TestMethod]
        public void Read_AOneParagraphText_IsThePlainText()
        {
            Assert.AreEqual("Motor 1", UnifiedFaceplateInterfaceText.Read(Stored));
        }

        [TestMethod]
        public void Read_ATagName_IsKeptAsItIs()
        {
            Assert.AreEqual("M1", UnifiedFaceplateInterfaceText.Read("M1"));
        }

        [TestMethod]
        public void Read_AnEmptyText_IsEmpty()
        {
            Assert.AreEqual(string.Empty, UnifiedFaceplateInterfaceText.Read(Stored.Replace("<p>Motor 1</p>", "<p></p>")));
        }
    }
}
