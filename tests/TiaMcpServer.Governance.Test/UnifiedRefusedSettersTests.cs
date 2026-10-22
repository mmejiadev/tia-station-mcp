using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>A setter TIA Portal refused is refused before drawing, and only on the type it was measured on.</remarks>
    [TestClass]
    public sealed class UnifiedRefusedSettersTests
    {
        [TestMethod]
        [DataRow("HmiToggleSwitch", "HotKey")]
        [DataRow("HmiTouchArea", "ShowFocusVisual")]
        public void ReasonFor_MeasuredProperty_IsGiven(string ownerType, string property)
        {
            Assert.IsNotNull(UnifiedRefusedSetters.ReasonFor(ownerType, property));
        }

        [TestMethod]
        [DataRow("HmiButton", "HotKey", DisplayName = "same name, another type")]
        [DataRow("HmiToggleSwitch", "Left", DisplayName = "same type, another property")]
        public void ReasonFor_AnythingNotMeasured_IsNull(string ownerType, string property)
        {
            Assert.IsNull(UnifiedRefusedSetters.ReasonFor(ownerType, property));
        }
    }
}