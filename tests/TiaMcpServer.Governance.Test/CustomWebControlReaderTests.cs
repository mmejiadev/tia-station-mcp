using System;
using TiaMcpServer.CustomWebControls;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A Custom Web Control's definition, read and checked before anything is packaged. What is refused here would
    /// otherwise reach TIA Portal as a package it ignores, or a page that breaks in runtime with nothing said.
    /// </remarks>
    [TestClass]
    public sealed class CustomWebControlReaderTests
    {
        private const string Minimal = @"{ ""name"": ""Plant.Pump"", ""displayName"": ""Pump"", ""width"": 100, ""height"": 80,
            ""properties"": [ { ""name"": ""Running"", ""type"": ""boolean"", ""default"": true }, { ""name"": ""Color"", ""type"": ""color"", ""default"": ""0, 200, 83"" } ],
            ""events"": [ { ""name"": ""Clicked"", ""arguments"": [ { ""name"": ""running"", ""type"": ""boolean"" } ] } ],
            ""html"": ""<div id='root'></div>"", ""css"": ""#root { }"", ""script"": ""function render(p) { }"" }";

        [TestMethod]
        public void Read_AMinimalDefinition_KeepsItsPropertiesAndEvents()
        {
            var definition = CustomWebControlReader.Read(Minimal);

            Assert.AreEqual("Plant.Pump", definition.Identity.Name);
            Assert.AreEqual(2, definition.Properties.Count);
            Assert.AreEqual("Clicked", definition.Events[0].Name);
        }

        /// <remarks>0xFF00C853: opaque, red 0, green 200, blue 83 - the ARGB number a manifest holds for a colour.</remarks>
        [TestMethod]
        public void Read_AColourDefault_IsHeldAsItsArgbNumber()
        {
            var definition = CustomWebControlReader.Read(Minimal);

            Assert.AreEqual(4278241363u, definition.Properties[1].Default.GetUInt32());
        }

        [TestMethod]
        public void Read_ANoDefault_TakesTheKindsOwn()
        {
            var definition = CustomWebControlReader.Read(Minimal.Replace(@", ""default"": true", string.Empty));

            Assert.IsFalse(definition.Properties[0].Default.GetBoolean());
        }

        [TestMethod]
        [DataRow("Plant Pump")]
        [DataRow("1Pump")]
        [DataRow("Plant..Pump")]
        [DataRow("")]
        public void Read_AControlNameNotDottedLettersAndDigits_ThrowsInvalidParams(string name)
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("Plant.Pump", name)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Read_APropertyNamedTwiceCaseAside_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace(@"""name"": ""Color""", @"""name"": ""running""")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>Arguments are keys of one object in the manifest (review of 2026-11-16).</remarks>
        [TestMethod]
        public void Read_AnEventArgumentNamedTwiceCaseAside_ThrowsInvalidParams()
        {
            var twice = Minimal.Replace(@"{ ""name"": ""running"", ""type"": ""boolean"" }", @"{ ""name"": ""running"", ""type"": ""boolean"" }, { ""name"": ""Running"", ""type"": ""number"" }");

            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(twice));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Read_ADefaultOfTheWrongKind_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace(@"""default"": true", @"""default"": ""yes""")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Read_AnUnknownType_ThrowsInvalidParamsNamingTheAcceptedOnes()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace(@"""type"": ""boolean"", ""default""", @"""type"": ""date"", ""default""")));

            StringAssert.Contains(failure.Message, CustomWebControlPropertyKinds.Accepted, StringComparison.Ordinal);
        }

        /// <remarks>A closing script tag inside the script would end its element early and run the rest as markup.</remarks>
        [TestMethod]
        public void Read_AScriptHoldingAClosingScriptTag_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("function render(p) { }", "function render(p) { } </SCRIPT><b>")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Read_AStyleHoldingAClosingStyleTag_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("#root { }", "#root { } </style>")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>Markup that opens a raw-text element or a comment swallows the helper and start-up scripts after it.</remarks>
        [TestMethod]
        [DataRow("<div><style>")]
        [DataRow("<!--")]
        [DataRow("<textarea>")]
        [DataRow("<script src='http://elsewhere/x.js'></script>")]
        public void Read_MarkupThatWouldSwallowThePage_ThrowsInvalidParams(string html)
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("<div id='root'></div>", html)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>A control reaches no other machine: plain addresses and network calls are refused (security review of 2026-11-15).</remarks>
        [TestMethod]
        [DataRow("function render(p) { fetch('/x'); }")]
        [DataRow("function render(p) { new WebSocket('ws://h'); }")]
        [DataRow("function render(p) { navigator.sendBeacon('/x'); }")]
        [DataRow("function render(p) { var i = new Image(); i.src = 'https://h/x'; }")]
        [DataRow("function render(p) { localStorage.setItem('a', 1); }")]
        public void Read_AScriptReachingOut_ThrowsInvalidParams(string script)
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("function render(p) { }", script)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("#root { background: url(//h/x.png); }")]
        [DataRow("@import 'x.css';")]
        public void Read_AStyleReachingOut_ThrowsInvalidParams(string css)
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("#root { }", css)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Read_MarkupWithAnImageElsewhere_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("<div id='root'></div>", "<img src='http://h/x.png'>")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>'&lt;!--&lt;script&gt;' in a script stops the author's own closing tag from closing it.</remarks>
        [TestMethod]
        public void Read_AScriptHoldingACommentOpener_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("function render(p) { }", "function render(p) { var s = '<!--<script>'; }")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("__proto__")]
        [DataRow("constructor")]
        public void Read_APropertyNamedLikeAPrototypeMember_ThrowsInvalidParams(string name)
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace(@"""name"": ""Running""", @"""name"": """ + name + @"""")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>The page calls render(): a script that only names it leaves the control blank (review of 2026-11-16).</remarks>
        [TestMethod]
        [DataRow("// render comes later")]
        [DataRow("function f(p) { p.render = 1; }")]
        public void Read_AScriptNamingRenderWithoutDefiningIt_ThrowsInvalidParams(string script)
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("function render(p) { }", script)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("var render = function (p) { };")]
        [DataRow("const render = (p) => { };")]
        public void Read_ARenderAssigned_IsAccepted(string script)
        {
            var definition = CustomWebControlReader.Read(Minimal.Replace("function render(p) { }", script));

            Assert.AreEqual("Plant.Pump", definition.Identity.Name);
        }

        [TestMethod]
        public void Read_AScriptWithoutRender_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace("function render(p) { }", "function draw(p) { }")));

            StringAssert.Contains(failure.Message, "render", StringComparison.Ordinal);
        }

        [TestMethod]
        [DataRow(8)]
        [DataRow(5000)]
        public void Read_ASizeOutOfRange_ThrowsInvalidParams(int width)
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlReader.Read(Minimal.Replace(@"""width"": 100", @"""width"": " + width)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void PackageId_TheSameName_GivesTheSameGuidEveryTime()
        {
            Assert.AreEqual(CustomWebControlGuid.For("Plant.Pump"), CustomWebControlGuid.For("Plant.Pump"));
            Assert.AreNotEqual(CustomWebControlGuid.For("Plant.Pump"), CustomWebControlGuid.For("Plant.Valve"));
        }
    }
}
