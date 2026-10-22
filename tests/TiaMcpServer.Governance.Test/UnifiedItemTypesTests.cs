using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Fail closed: only the item types read from Openness V20 are drawn, and nothing is guessed.</remarks>
    [TestClass]
    public sealed class UnifiedItemTypesTests
    {
        [TestMethod]
        [DataRow("rectangle", "Siemens.Engineering.HmiUnified.UI.Shapes.HmiRectangle")]
        [DataRow("HmiIOField", "Siemens.Engineering.HmiUnified.UI.Widgets.HmiIOField")]
        [DataRow("ALARMCONTROL", "Siemens.Engineering.HmiUnified.UI.Controls.HmiAlarmControl")]
        [DataRow(" screenWindow ", "Siemens.Engineering.HmiUnified.UI.Screens.HmiScreenWindow")]
        public void Resolve_KnownName_GivesTheOpennessType(string name, string expected)
        {
            Assert.AreEqual(expected, UnifiedItemTypes.Resolve(name));
        }

        [TestMethod]
        [DataRow("hologram")]
        [DataRow("")]
        [DataRow("HmiScreenItemBase")]
        [DataRow("customWidgetContainer")]
        [DataRow("label")]
        public void Resolve_AnythingElse_ThrowsInvalidParams(string name)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedItemTypes.Resolve(name));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("Siemens.Engineering.HmiUnified.UI.Shapes.HmiRectangle", true)]
        [DataRow("HmiIOField", true)]
        [DataRow("Siemens.Engineering.HmiUnified.UI.Widgets.HmiLabel", false)]
        [DataRow("HmiFaceplateContainer", false)]
        [DataRow("", false)]
        public void IsDrawable_FoundOnAScreen_IsTrueOnlyForTypesThisToolDrawsBack(string name, bool expected)
        {
            Assert.AreEqual(expected, UnifiedItemTypes.IsDrawable(name));
        }

        [TestMethod]
        public void All_ListsEveryShapeWidgetControlAndWindowOnce()
        {
            Assert.AreEqual(35, UnifiedItemTypes.All.Count);
            Assert.AreEqual(UnifiedItemTypes.All.Count, UnifiedItemTypes.All.Distinct().Count());
        }

        [TestMethod]
        public void ShortName_RoundTripsThroughResolve()
        {
            foreach (var fullName in UnifiedItemTypes.All)
            {
                Assert.AreEqual(fullName, UnifiedItemTypes.Resolve(UnifiedItemTypes.ShortName(fullName)));
            }
        }

        [TestMethod]
        public void IsPointBased_OnlyPolylineAndPolygon()
        {
            var pointBased = UnifiedItemTypes.All.Where(UnifiedItemTypes.IsPointBased).Select(UnifiedItemTypes.ShortName).ToArray();

            CollectionAssert.AreEquivalent(new[] { "polygon", "polyline" }, pointBased);
        }
    }
}
