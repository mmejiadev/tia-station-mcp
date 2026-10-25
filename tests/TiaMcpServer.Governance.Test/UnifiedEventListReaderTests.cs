using System;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Named functions are written as the JavaScript measured on TIA Portal V20; anything ambiguous —
    /// a script mixed with functions, a field nobody reads, a missing argument — is refused.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedEventListReaderTests
    {
        [TestMethod]
        [DataRow(@"{ ""event"": ""down"", ""function"": ""setBit"", ""tag"": ""Motor"" }", "Tags(\"Motor\").Write(1);")]
        [DataRow(@"{ ""event"": ""up"", ""function"": ""resetBit"", ""tag"": ""Motor"" }", "Tags(\"Motor\").Write(0);")]
        [DataRow(@"{ ""event"": ""tapped"", ""function"": ""setTag"", ""tag"": ""Speed"", ""value"": 12.5 }", "Tags(\"Speed\").Write(12.5);")]
        [DataRow(@"{ ""event"": ""tapped"", ""function"": ""changeScreen"", ""screen"": ""Recipes"" }", "HMIRuntime.UI.SysFct.ChangeScreen(\"Recipes\", null);")]
        [DataRow(@"{ ""event"": ""tapped"", ""function"": ""changeScreen"", ""screen"": ""Recipes"", ""window"": ""../Screen window_3"" }", "HMIRuntime.UI.SysFct.ChangeScreen(\"Recipes\", \"../Screen window_3\");")]
        public void Read_Function_IsWrittenAsTheMeasuredJavaScript(string entry, string expected)
        {
            var events = Read($"[ {entry} ]");

            Assert.AreEqual(expected, events[0].Script);
        }

        [TestMethod]
        public void Read_TwoFunctionsOnOneEvent_RunInOrderInOneScript()
        {
            var events = Read(@"[ { ""event"": ""tapped"", ""function"": ""setTag"", ""tag"": ""Mode"", ""value"": 1 }, { ""event"": ""down"", ""function"": ""setBit"", ""tag"": ""M"" }, { ""event"": ""TAPPED"", ""function"": ""changeScreen"", ""screen"": ""Manual"" } ]");

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("tapped", events[0].EventName);
            Assert.AreEqual("Tags(\"Mode\").Write(1);" + Environment.NewLine + "HMIRuntime.UI.SysFct.ChangeScreen(\"Manual\", null);", events[0].Script);
        }

        [TestMethod]
        public void Read_Script_IsKeptAsWritten()
        {
            var events = Read(@"[ { ""event"": ""keyDown"", ""script"": ""Tags('Level').Write(0);"", ""globalDefinitions"": ""let x = 1;"" } ]");

            Assert.AreEqual("Tags('Level').Write(0);", events[0].Script);
            Assert.AreEqual("let x = 1;", events[0].GlobalDefinitions);
        }

        /// <remarks>A name is a JavaScript string literal: a quote in it cannot end the literal and run code.</remarks>
        [TestMethod]
        public void Read_NameWithAQuote_StaysInsideTheLiteral()
        {
            var events = Read(@"[ { ""event"": ""down"", ""function"": ""setBit"", ""tag"": ""A\"");alert(1);//"" } ]");

            Assert.AreEqual("Tags(\"A\\\");alert(1);//\").Write(1);", events[0].Script);
        }

        [TestMethod]
        public void Read_NameWithAControlCharacter_IsEscaped()
        {
            var events = Read(@"[ { ""event"": ""down"", ""function"": ""setBit"", ""tag"": ""A\tB"" } ]");

            Assert.AreEqual("Tags(\"A\\u0009B\").Write(1);", events[0].Script);
        }

        [TestMethod]
        [DataRow(@"[ { ""event"": ""down"", ""function"": ""blink"", ""tag"": ""M"" } ]", DisplayName = "unknown function")]
        [DataRow(@"[ { ""event"": ""down"", ""function"": ""setBit"" } ]", DisplayName = "setBit without a tag")]
        [DataRow(@"[ { ""event"": ""tapped"", ""function"": ""setTag"", ""tag"": ""M"" } ]", DisplayName = "setTag without a value")]
        [DataRow(@"[ { ""event"": ""tapped"", ""function"": ""changeScreen"" } ]", DisplayName = "changeScreen without a screen")]
        [DataRow(@"[ { ""function"": ""setBit"", ""tag"": ""M"" } ]", DisplayName = "no event")]
        [DataRow(@"[ { ""event"": ""on down"", ""function"": ""setBit"", ""tag"": ""M"" } ]", DisplayName = "event that is not a name")]
        [DataRow(@"[ { ""event"": ""tapped"", ""script"": ""a();"", ""function"": ""setBit"", ""tag"": ""M"" } ]", DisplayName = "script and function in one entry")]
        [DataRow(@"[ { ""event"": ""tapped"", ""script"": ""a();"" }, { ""event"": ""tapped"", ""function"": ""setBit"", ""tag"": ""M"" } ]", DisplayName = "script and function on one event")]
        [DataRow(@"[ { ""event"": ""tapped"", ""script"": """" } ]", DisplayName = "empty script")]
        [DataRow(@"[ { ""event"": ""tapped"", ""function"": ""setBit"", ""tag"": ""M"", ""delay"": 5 } ]", DisplayName = "a field the tool does not set")]
        [DataRow(@"{ ""event"": ""tapped"" }", DisplayName = "events not a list")]
        [DataRow(@"[ { ""event"": ""down"", ""function"": ""setBit"", ""tag"": ""M"", ""globalDefinitions"": ""let x = 1;"" } ]", DisplayName = "globalDefinitions on a function")]
        [DataRow(@"[ { ""event"": ""tapped"", ""script"": ""a();"", ""screen"": ""Recipes"" } ]", DisplayName = "a function argument on a script")]
        [DataRow(@"[ { ""event"": ""tapped"", ""script"": ""a();"", ""globalDefinitions"": 5 } ]", DisplayName = "globalDefinitions not text")]
        public void Read_Ambiguous_ThrowsInvalidParams(string events)
        {
            var failure = Assert.ThrowsException<PortalException>(() => Read(events));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        private static System.Collections.Generic.IReadOnlyList<UnifiedEventScript> Read(string events)
        {
            using var document = JsonDocument.Parse($@"{{ ""events"": {events} }}");

            return UnifiedEventListReader.Read(document.RootElement, "Item 'B'");
        }
    }
}
