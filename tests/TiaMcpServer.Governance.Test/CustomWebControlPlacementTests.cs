using System.Collections.Generic;
using System.Drawing;
using TiaMcpServer.CustomWebControls;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Which Custom Web Controls to place, read and checked against the installed control's manifest before the first one
    /// is created, so a list with one wrong name places nothing.
    /// </remarks>
    [TestClass]
    public sealed class CustomWebControlPlacementTests
    {
        private const string Placement = @"{ ""screen"": ""Main"", ""controls"": [ { ""name"": ""Lamp_1"", ""control"": ""TiaStation.PilotLight"", ""left"": 10, ""top"": 20,
            ""properties"": { ""Label"": ""M1"", ""OnColor"": ""0, 0, 255"" }, ""tags"": { ""IsOn"": ""M1.KM"" }, ""events"": { ""Clicked"": ""HMIRuntime.Trace('x');"" } } ] }";

        private static readonly CustomWebControlSummary Pilot = new CustomWebControlSummary(
            ("TiaStation.PilotLight", "Pilot light"),
            new List<(string, CustomWebControlPropertyKind?)>
            {
                ("IsOn", CustomWebControlPropertyKind.Boolean),
                ("Label", CustomWebControlPropertyKind.Text),
                ("OnColor", CustomWebControlPropertyKind.Color),
                ("Data", null)
            },
            new[] { "Clicked" },
            (120, 150));

        [TestMethod]
        public void Read_APlacement_KeepsItsValuesTagsAndEvents()
        {
            var list = CustomWebControlPlacementReader.Read(Placement);

            var lamp = list.Placements[0];
            Assert.AreEqual("Main", list.Screen);
            Assert.AreEqual("M1.KM", lamp.Interface.Tags["IsOn"]);
            Assert.IsTrue(lamp.Interface.Events.ContainsKey("Clicked"));
            Assert.AreEqual(10, lamp.Frame.Left);
        }

        [TestMethod]
        public void Read_APropertyGivenAValueAndATag_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPlacementReader.Read(Placement.Replace(@"""tags"": { ""IsOn""", @"""tags"": { ""Label""")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Read_TheSameNameTwice_ThrowsInvalidParams()
        {
            var twice = Placement.Replace(@"""controls"": [ {", @"""controls"": [ { ""name"": ""lamp_1"", ""control"": ""TiaStation.PilotLight"" }, {");

            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPlacementReader.Read(twice));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Read_APropertyNamedTwice_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPlacementReader.Read(Placement.Replace(@"""Label"": ""M1""", @"""Label"": ""M1"", ""Label"": ""M2""")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>A control installed by somebody else may not follow this server's naming; the lookup among those installed decides.</remarks>
        [TestMethod]
        public void Read_AControlNamedWithUnderscores_IsTaken()
        {
            var list = CustomWebControlPlacementReader.Read(Placement.Replace("TiaStation.PilotLight", "Vendor.Level_Meter"));

            Assert.AreEqual("Vendor.Level_Meter", list.Placements[0].Control);
        }

        [TestMethod]
        public void Check_APlacementTheControlFits_Passes()
        {
            CustomWebControlPlacementRules.Check(CustomWebControlPlacementReader.Read(Placement).Placements[0], Pilot);
        }

        [TestMethod]
        public void Check_APropertyTheControlLacks_ThrowsInvalidParamsNamingWhatItHas()
        {
            var placement = CustomWebControlPlacementReader.Read(Placement.Replace(@"""Label"": ""M1""", @"""Text"": ""M1""")).Placements[0];

            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPlacementRules.Check(placement, Pilot));

            StringAssert.Contains(failure.Message, "OnColor", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_AnEventTheControlDoesNotFire_ThrowsInvalidParams()
        {
            var placement = CustomWebControlPlacementReader.Read(Placement.Replace(@"""Clicked"": ""HMI", @"""Pressed"": ""HMI")).Placements[0];

            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPlacementRules.Check(placement, Pilot));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void ValueOf_AColour_IsASystemDrawingColor()
        {
            var placement = CustomWebControlPlacementReader.Read(Placement).Placements[0];

            var value = CustomWebControlPlacementRules.ValueOf(placement, Pilot, "OnColor");

            Assert.AreEqual(Color.FromArgb(0, 0, 255).ToArgb(), ((Color)value).ToArgb());
        }

        [TestMethod]
        public void Check_ATagOnAPropertyOfAKindNotSet_Passes()
        {
            var placement = CustomWebControlPlacementReader.Read(Placement.Replace(@"""IsOn"": ""M1.KM""", @"""Data"": ""M1.KM""")).Placements[0];

            CustomWebControlPlacementRules.Check(placement, Pilot);
        }

        [TestMethod]
        public void Check_AFixedValueOnAPropertyOfAKindNotSet_ThrowsInvalidParams()
        {
            var placement = CustomWebControlPlacementReader.Read(Placement.Replace(@"""Label"": ""M1""", @"""Data"": ""M1""")).Placements[0];

            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlPlacementRules.Check(placement, Pilot));

            StringAssert.Contains(failure.Message, "tag", System.StringComparison.Ordinal);
        }
    }
}
