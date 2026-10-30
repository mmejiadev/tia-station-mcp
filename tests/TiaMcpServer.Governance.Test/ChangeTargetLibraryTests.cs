using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The library targets a policy file names: a type is under its own prefix, apart from any PLC or HMI.</remarks>
    [TestClass]
    public sealed class ChangeTargetLibraryTests
    {
        [TestMethod]
        public void LibraryType_AType_IsUnderTheLibraryPrefix()
        {
            Assert.AreEqual("library/FP_Motor", ChangeTarget.LibraryType("FP_Motor"));
        }

        [TestMethod]
        public void LibraryType_APolicyForAPlcNamedLikeTheType_DoesNotMatchIt()
        {
            var plcRule = TargetPattern.Parse("FP_Motor/*");

            Assert.IsFalse(plcRule.Matches(ChangeTarget.LibraryType("FP_Motor")));
        }

        [TestMethod]
        public void ProjectLibrary_ARuleForEveryType_DoesNotAllowCopyingIntoTheLibrary()
        {
            var typeRule = TargetPattern.Parse("library/*");

            Assert.IsFalse(typeRule.Matches(ChangeTarget.ProjectLibrary));
        }
    }
}
