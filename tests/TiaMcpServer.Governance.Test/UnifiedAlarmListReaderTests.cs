using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedAlarms;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Fail closed: an incomplete alarm, an unknown field or a value out of range is refused before TIA Portal is asked.</remarks>
    [TestClass]
    public sealed class UnifiedAlarmListReaderTests
    {
        private const string Full = @"{
            ""classes"": [ { ""name"": ""Tolva"", ""priority"": 10, ""stateMachine"": ""RAISECLEAR"",
                ""states"": { ""raised"": { ""backColor"": ""255, 128, 0"", ""textColor"": ""black"", ""flashing"": true }, ""cleared"": { ""backColor"": ""#00FF00"" } } } ],
            ""discrete"": [ { ""name"": ""Tolva_Plena"", ""tag"": ""Alarms"", ""bit"": 3, ""trigger"": ""fallingEdge"", ""class"": ""Tolva"", ""priority"": 3,
                ""text"": ""Tolva plena"", ""info"": ""Buidar"", ""origin"": ""Tolva"", ""area"": ""Linia 1"" } ],
            ""analog"": [ { ""name"": ""Pressio_Alta"", ""tag"": ""Pressure"", ""condition"": ""upperLimitOrEqual"", ""value"": 8.5, ""text"": ""Pressio alta"" } ] }";

        [TestMethod]
        public void Parse_FullList_ReadsClassesAndAlarms()
        {
            var list = UnifiedAlarmListReader.Parse(Full);

            var tolva = list.Classes[0];
            Assert.AreEqual((byte)10, tolva.Priority);
            Assert.AreEqual(UnifiedAlarmStateMachine.RaiseClear, tolva.StateMachine);
            Assert.AreEqual("255, 128, 0", tolva.States[UnifiedAlarmState.Raised].BackColor);
            Assert.AreEqual("0, 0, 0", tolva.States[UnifiedAlarmState.Raised].TextColor);
            Assert.AreEqual("0, 255, 0", tolva.States[UnifiedAlarmState.Cleared].BackColor);
            Assert.IsFalse(tolva.States.ContainsKey(UnifiedAlarmState.Acknowledged));
        }

        [TestMethod]
        public void Parse_DiscreteAlarm_ReadsEveryField()
        {
            var alarm = UnifiedAlarmListReader.Parse(Full).Discrete[0];

            Assert.AreEqual("Alarms", alarm.Tag);
            Assert.AreEqual(3u, alarm.Bit);
            Assert.AreEqual(UnifiedAlarmTrigger.FallingEdge, alarm.Trigger);
            Assert.AreEqual("Tolva", alarm.Common.AlarmClass);
            Assert.AreEqual((byte)3, alarm.Common.Priority);
            Assert.AreEqual("Tolva plena", alarm.Common.Texts.Text);
            Assert.AreEqual("Buidar", alarm.Common.Texts.Info);
            Assert.AreEqual("Linia 1", alarm.Common.Texts.Area);
        }

        [TestMethod]
        public void Parse_AnalogAlarmWithoutClass_GetsTheDefaultClass()
        {
            var alarm = UnifiedAlarmListReader.Parse(Full).Analog[0];

            Assert.AreEqual(UnifiedAlarmCondition.UpperLimitOrEqual, alarm.Condition);
            Assert.AreEqual("8.5", alarm.Value);
            Assert.AreEqual(UnifiedAlarmCommon.DefaultClass, alarm.Common.AlarmClass);
            Assert.IsNull(alarm.Common.Priority);
        }

        [TestMethod]
        public void Parse_DiscreteAlarmWithoutBitOrTrigger_TakesBitZeroOnTheRisingEdge()
        {
            var alarm = UnifiedAlarmListReader.Parse(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""Run"", ""text"": ""Run"" } ] }").Discrete[0];

            Assert.AreEqual(0u, alarm.Bit);
            Assert.AreEqual(UnifiedAlarmTrigger.RisingEdge, alarm.Trigger);
        }

        [TestMethod]
        [DataRow(@"{ }", DisplayName = "nothing to create")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"" } ] }", DisplayName = "an alarm without a text")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""text"": ""x"" } ] }", DisplayName = "an alarm without a tag")]
        [DataRow(@"{ ""discrete"": [ { ""tag"": ""T"", ""text"": ""x"" } ] }", DisplayName = "an alarm without a name")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"", ""text"": ""x"", ""bit"": -1 } ] }", DisplayName = "a negative bit")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"", ""text"": ""x"", ""trigger"": ""bothEdges"" } ] }", DisplayName = "a trigger there is not")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"", ""text"": ""x"", ""trigger"": ""1"" } ] }", DisplayName = "a trigger given as a number")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"", ""text"": ""x"", ""priority"": 17 } ] }", DisplayName = "priority 17, which TIA Portal refuses")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"", ""text"": ""x"", ""colour"": ""red"" } ] }", DisplayName = "a field the tool does not set")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"", ""text"": ""x"" }, { ""name"": ""a"", ""tag"": ""U"", ""text"": ""y"" } ] }", DisplayName = "two discrete alarms of one name")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""T"", ""bit"": 2, ""text"": ""x"" }, { ""name"": ""B"", ""tag"": ""t"", ""bit"": 2, ""text"": ""y"" } ] }", DisplayName = "one tag and bit raising two alarms")]
        [DataRow(@"{ ""analog"": [ { ""name"": ""A"", ""tag"": ""T"", ""value"": 1, ""text"": ""x"" } ] }", DisplayName = "an analog alarm without a condition")]
        [DataRow(@"{ ""analog"": [ { ""name"": ""A"", ""tag"": ""T"", ""condition"": ""upperLimit"", ""text"": ""x"" } ] }", DisplayName = "an analog alarm without a value")]
        [DataRow(@"{ ""analog"": [ { ""name"": ""A"", ""tag"": ""T"", ""condition"": ""upperLimit"", ""value"": ""8"", ""text"": ""x"" } ] }", DisplayName = "a value given as text")]
        [DataRow(@"{ ""analog"": [ { ""name"": ""A"", ""tag"": ""T"", ""condition"": ""higher"", ""value"": 8, ""text"": ""x"" } ] }", DisplayName = "a condition there is not")]
        [DataRow(@"{ ""classes"": [ { ""name"": ""C"" }, { ""name"": ""c"" } ] }", DisplayName = "two classes of one name")]
        [DataRow(@"{ ""classes"": [ { ""name"": ""C"", ""stateMachine"": ""always"" } ] }", DisplayName = "a state machine there is not")]
        [DataRow(@"{ ""classes"": [ { ""name"": ""C"", ""states"": { ""blinking"": { ""flashing"": true } } } ] }", DisplayName = "a state there is not")]
        [DataRow(@"{ ""classes"": [ { ""name"": ""C"", ""states"": { ""raised"": { ""backColor"": ""not a colour"" } } } ] }", DisplayName = "a colour that is not one")]
        [DataRow(@"{ ""classes"": [ { ""name"": ""C"", ""states"": { ""raised"": { ""opacity"": 1 } } } ] }", DisplayName = "a state field the tool does not set")]
        [DataRow(@"{ ""classes"": [ { ""name"": ""C"", ""states"": { ""raised"": { ""backColor"": ""255, 0, 0, 300"" } } } ] }", DisplayName = "an alpha past 255")]
        [DataRow(@"{ ""alarms"": [ ] }", DisplayName = "a list field the tool does not know")]
        [DataRow(@"not json", DisplayName = "not JSON")]
        public void Parse_IncompleteOrUnknown_ThrowsInvalidParams(string json)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedAlarmListReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        [TestMethod]
        public void Parse_ColourWithAlpha_KeepsItsFourChannels()
        {
            var list = UnifiedAlarmListReader.Parse(@"{ ""classes"": [ { ""name"": ""C"", ""states"": { ""raised"": { ""backColor"": ""255, 0, 0, 128"" } } } ] }");

            Assert.AreEqual("255, 0, 0, 128", list.Classes[0].States[UnifiedAlarmState.Raised].BackColor);
        }

        [TestMethod]
        public void Parse_TagAndBitRaisingTwoAlarms_NamesBothAlarms()
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedAlarmListReader.Parse(
                @"{ ""discrete"": [ { ""name"": ""First"", ""tag"": ""Alarms"", ""bit"": 3, ""text"": ""x"" }, { ""name"": ""Second"", ""tag"": ""alarms"", ""bit"": 3, ""text"": ""y"" } ] }"));

            StringAssert.Contains(failure.Message, "Alarms bit 3 by First, Second", System.StringComparison.Ordinal);
        }

        /// <remarks>Measured 2026-10-25: TIA Portal V20 creates an analog alarm named like a discrete one.</remarks>
        [TestMethod]
        public void Parse_DiscreteAndAnalogOfOneName_AreBothRead()
        {
            var list = UnifiedAlarmListReader.Parse(@"{ ""discrete"": [ { ""name"": ""Level"", ""tag"": ""Bits"", ""text"": ""x"" } ], ""analog"": [ { ""name"": ""Level"", ""tag"": ""Level"", ""condition"": ""equal"", ""value"": 1, ""text"": ""y"" } ] }");

            Assert.AreEqual(1, list.Discrete.Count);
            Assert.AreEqual(1, list.Analog.Count);
        }
    }
}
