using System;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-10-30 on TIA Portal V20: a container is created with "V0.0.1\FACEPLATE_MOTOR_V1"; "V 0.0.1\..." is refused.</remarks>
    [TestClass]
    public sealed class FaceplateTypeReferenceTests
    {
        [TestMethod]
        public void Format_ATypeAndVersion_WritesTheMeasuredShape()
        {
            var reference = FaceplateTypeReference.Format("FACEPLATE_MOTOR_V1", new Version(0, 0, 1));

            Assert.AreEqual("V0.0.1\\FACEPLATE_MOTOR_V1", reference);
        }

        [TestMethod]
        public void Parse_AContainedType_ReadsTheTypeAndVersionBack()
        {
            var parsed = FaceplateTypeReference.Parse("V0.0.2\\FP_Motor");

            Assert.IsNotNull(parsed);
            Assert.AreEqual("FP_Motor", parsed.Value.TypeName);
            Assert.AreEqual(new Version(0, 0, 2), parsed.Value.Version);
        }

        [TestMethod]
        public void Parse_TextOfAnotherShape_IsNull()
        {
            Assert.IsNull(FaceplateTypeReference.Parse("FP_Motor"));
            Assert.IsNull(FaceplateTypeReference.Parse("V 0.0.1\\FP_Motor"));
            Assert.IsNull(FaceplateTypeReference.Parse("V0.0.1\\"));
            Assert.IsNull(FaceplateTypeReference.Parse(string.Empty));
        }
    }
}
