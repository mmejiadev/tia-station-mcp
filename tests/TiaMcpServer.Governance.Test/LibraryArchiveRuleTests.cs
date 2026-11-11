using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class LibraryArchiveRuleTests
    {
        [TestMethod]
        public void ArchiveFileName_ALibrary_GetsThePortalsExtension()
        {
            var fileName = LibraryArchiveRule.ArchiveFileName(" Kit ", 21);

            Assert.AreEqual("Kit.zal21", fileName);
        }

        /// <remarks>A version never read would name the backup 'Kit.zal0', which no TIA Portal retrieves.</remarks>
        [TestMethod]
        public void ArchiveFileName_AnUnknownVersion_IsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryArchiveRule.ArchiveFileName("Kit", 0));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }
    }
}
