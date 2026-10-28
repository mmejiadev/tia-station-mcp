using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Read through <see cref="UnifiedTagListReader.Parse"/>, the only way in. What a kind of tag does not
    /// take is refused before TIA Portal is asked, rather than left to fail as a disabled field halfway
    /// through a list.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedTagSettingsReaderTests
    {
        private const string Linked = @"""connection"": ""HMI_Connection_1"", ""plcTag"": ""AI_Pressure""";

        [TestMethod]
        public void Parse_InternalTagWithEverySetting_ReadsThem()
        {
            var tag = Single(@"{ ""name"": ""Level"", ""dataType"": ""Int"", ""comment"": ""Tank level"", ""initialValue"": 7, ""persistent"": true,
                ""limits"": { ""lower"": 0, ""upper"": ""Level_Max"" } }");

            Assert.AreEqual("Tank level", tag.Settings.Options.Comment);
            Assert.AreEqual("7", tag.Settings.Options.InitialValue);
            Assert.IsTrue(tag.Settings.Options.Persistent!.Value);
            Assert.AreEqual("0", tag.Settings.Limits.Lower!.Value);
            Assert.IsFalse(tag.Settings.Limits.Lower.IsTag);
            Assert.AreEqual("Level_Max", tag.Settings.Limits.Upper!.Value);
            Assert.IsTrue(tag.Settings.Limits.Upper.IsTag);
            Assert.IsFalse(tag.Settings.PlcReading.IsSet);
        }

        [TestMethod]
        public void Parse_LinkedTagWithPlcReading_ReadsIt()
        {
            var tag = Single(@"{ ""name"": ""Pressure"", " + Linked + @", ""acquisition"": { ""mode"": ""CYCLICONUSE"", ""cycle"": ""t500ms"" },
                ""scaling"": { ""plc"": [0, 27648], ""hmi"": [0, 10.5] }, ""substitute"": { ""usage"": ""invalidValue"", ""value"": 0 } }");

            Assert.AreEqual(UnifiedAcquisitionMode.CyclicOnUse, tag.Settings.PlcReading.Acquisition!.Mode);
            Assert.AreEqual("T500ms", tag.Settings.PlcReading.Acquisition.Cycle);
            Assert.AreEqual((0d, 27648d), tag.Settings.PlcReading.Scaling!.Plc);
            Assert.AreEqual((0d, 10.5d), tag.Settings.PlcReading.Scaling.Hmi);
            Assert.AreEqual(UnifiedSubstituteUsage.InvalidValue, tag.Settings.PlcReading.Substitute!.Usage);
            Assert.AreEqual("0", tag.Settings.PlcReading.Substitute.Value);
        }

        [TestMethod]
        public void Parse_BoolStartValueGivenAsTrue_IsWrittenAsOne()
        {
            var tag = Single(@"{ ""name"": ""Flag"", ""dataType"": ""Bool"", ""initialValue"": true }");

            Assert.AreEqual("1", tag.Settings.Options.InitialValue);
        }

        [TestMethod]
        public void Parse_OnDemandWithoutCycle_HasNoCycle()
        {
            var tag = Single(@"{ ""name"": ""Pressure"", " + Linked + @", ""acquisition"": { ""mode"": ""onDemand"" } }");

            Assert.AreEqual(UnifiedAcquisitionMode.OnDemand, tag.Settings.PlcReading.Acquisition!.Mode);
            Assert.IsNull(tag.Settings.PlcReading.Acquisition.Cycle);
        }

        /// <remarks>A linked tag's type is TIA Portal's to fill in, so its values wait for it rather than being guessed.</remarks>
        [TestMethod]
        public void Parse_LinkedTagValues_AreKeptForTheWriteToCheck()
        {
            var tag = Single(@"{ ""name"": ""Pressure"", " + Linked + @", ""initialValue"": ""abc"", ""limits"": { ""lower"": 50, ""upper"": 10 } }");

            Assert.AreEqual("abc", tag.Settings.Options.InitialValue);
            Assert.AreEqual("50", tag.Settings.Limits.Lower!.Value);
        }

        [TestMethod]
        public void Parse_NoSettings_HasNone()
        {
            var tag = Single(@"{ ""name"": ""Level"", ""dataType"": ""Int"" }");

            Assert.IsNull(tag.Settings.Options.Comment);
            Assert.IsFalse(tag.Settings.Limits.IsSet);
            Assert.IsFalse(tag.Settings.PlcReading.IsSet);
        }

        [TestMethod]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""acquisition"": { ""mode"": ""onDemand"" } }", DisplayName = "acquisition on an internal tag")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""scaling"": { ""plc"": [0, 1], ""hmi"": [0, 1] } }", DisplayName = "scaling on an internal tag")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""substitute"": { ""usage"": ""invalidValue"", ""value"": 0 } }", DisplayName = "substitute on an internal tag")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""persistent"": true }", DisplayName = "persistent on a linked tag")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""maxLength"": 20 }", DisplayName = "maxLength on a number")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""WString"", ""maxLength"": 0 }", DisplayName = "maxLength of 0")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Bool"", ""limits"": { ""upper"": 1 } }", DisplayName = "limits on a Bool")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""WString"", ""limits"": { ""upper"": 1 } }", DisplayName = "limits on a WString")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Array [0..3] of Int"", ""limits"": { ""upper"": 1 } }", DisplayName = "limits on an array")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Array [0..3] of Int"", ""initialValue"": 1 }", DisplayName = "a start value on an array")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""limits"": { ""lower"": 50, ""upper"": 10 } }", DisplayName = "crossed limits")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""limits"": { ""upper"": 99999 } }", DisplayName = "a limit outside the type")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""limits"": { } }", DisplayName = "empty limits")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""limits"": { ""upper"": "" "" } }", DisplayName = "a blank limit tag")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""limits"": { ""max"": 1 } }", DisplayName = "a limit field the tool does not know")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""initialValue"": 1.5 }", DisplayName = "a fraction for an Int")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Bool"", ""initialValue"": 2 }", DisplayName = "2 for a Bool")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""acquisition"": { ""mode"": ""cyclicContinuous"" } }", DisplayName = "cyclic continuous, which compiled as invalid")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""acquisition"": { ""mode"": ""onDemand"", ""cycle"": ""T1s"" } }", DisplayName = "a cycle on demand")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""acquisition"": { ""mode"": ""cyclicOnUse"", ""cycle"": ""T3s"" } }", DisplayName = "a cycle the HMI does not have")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""acquisition"": { ""cycle"": ""T1s"" } }", DisplayName = "acquisition without a mode")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""scaling"": { ""plc"": [0, 27648], ""hmi"": [5, 5] } }", DisplayName = "a flat HMI range")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""scaling"": { ""plc"": [7, 7], ""hmi"": [0, 100] } }", DisplayName = "a flat PLC range")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""scaling"": { ""plc"": [0, 27648] } }", DisplayName = "scaling without an HMI range")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""scaling"": { ""plc"": [""0"", 1], ""hmi"": [0, 1] } }", DisplayName = "scaling given as text")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""substitute"": { ""usage"": ""always"", ""value"": 0 } }", DisplayName = "a substitute usage there is not")]
        [DataRow(@"{ ""name"": ""A"", " + Linked + @", ""substitute"": { ""usage"": ""invalidValue"" } }", DisplayName = "a substitute without a value")]
        [DataRow(@"{ ""name"": ""A"", ""dataType"": ""Int"", ""persistent"": ""yes"" }", DisplayName = "persistent that is not a boolean")]
        public void Parse_SettingItsTagDoesNotTake_ThrowsInvalidParams(string tag)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTagListReader.Parse(@"{ ""tags"": [ " + tag + " ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        private static UnifiedTagDescription Single(string tag)
        {
            return UnifiedTagListReader.Parse(@"{ ""tags"": [ " + tag + " ] }").Tags[0];
        }
    }
}
