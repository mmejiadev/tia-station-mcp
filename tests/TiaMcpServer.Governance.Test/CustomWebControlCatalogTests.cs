using System;
using System.Linq;
using TiaMcpServer.CustomWebControls;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The ready-made controls go through the same reader as an author's: each one is read here, so a catalogue file
    /// that breaks a rule fails a test rather than an installation.
    /// </remarks>
    [TestClass]
    public sealed class CustomWebControlCatalogTests
    {
        private static readonly string[] Expected = { "Conveyor", "Gauge", "Motor", "PilotLight", "PushButton", "Tank", "Valve" };

        [TestMethod]
        public void Names_TheCatalogue_HoldsTheSevenControls()
        {
            CollectionAssert.AreEqual(Expected, CustomWebControlCatalog.Names().ToArray());
        }

        [TestMethod]
        public void Get_EveryControl_ReadsAndIsNamedUnderTiaStation()
        {
            foreach (var name in CustomWebControlCatalog.Names())
            {
                var definition = CustomWebControlCatalog.Get(name);

                Assert.IsTrue(definition.Identity.Name.StartsWith("TiaStation.", StringComparison.Ordinal), name);
                Assert.IsTrue(definition.Content.Html.Length > 0 && definition.Content.Css.Length > 0, name);
            }
        }

        /// <remarks>The page passes the properties to render() by name, so every name the script reads must be declared.</remarks>
        [TestMethod]
        public void Get_EveryControl_ItsScriptReadsOnlyDeclaredProperties()
        {
            foreach (var name in CustomWebControlCatalog.Names())
            {
                var definition = CustomWebControlCatalog.Get(name);
                var declared = definition.Properties.Select(property => property.Name).ToList();
                var read = System.Text.RegularExpressions.Regex.Matches(definition.Content.Script, @"\bp\.([A-Za-z_][A-Za-z0-9_]*)")
                    .Cast<System.Text.RegularExpressions.Match>().Select(match => match.Groups[1].Value).Distinct();

                CollectionAssert.IsSubsetOf(read.ToList(), declared, name);
            }
        }

        /// <remarks>An event fired by the script but missing from the manifest never reaches a screen's script.</remarks>
        [TestMethod]
        public void Get_EveryControl_ItsScriptFiresOnlyDeclaredEvents()
        {
            foreach (var name in CustomWebControlCatalog.Names())
            {
                var definition = CustomWebControlCatalog.Get(name);
                var fired = System.Text.RegularExpressions.Regex.Matches(definition.Content.Script, @"TiaStation\.fire\('([A-Za-z_]+)'")
                    .Cast<System.Text.RegularExpressions.Match>().Select(match => match.Groups[1].Value).Distinct();

                CollectionAssert.IsSubsetOf(fired.ToList(), definition.Events.Select(declared => declared.Name).ToList(), name);
            }
        }

        [TestMethod]
        public void Get_ANameCaseAside_FindsIt()
        {
            Assert.AreEqual("TiaStation.Tank", CustomWebControlCatalog.Get("tank").Identity.Name);
        }

        [TestMethod]
        public void Get_ANameNotInTheCatalogue_ThrowsInvalidParamsNamingThoseThatAre()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlCatalog.Get("Robot"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "PilotLight", StringComparison.Ordinal);
        }

        /// <remarks>A policy allowing 'custom-web-controls/TiaStation.*' means the catalogue, not what a session writes under its name (review of 2026-11-16).</remarks>
        [TestMethod]
        [DataRow("TiaStation.PilotLight")]
        [DataRow("tiastation.Mine")]
        public void RequireWritten_ANameInTheCataloguesNamespace_ThrowsInvalidParams(string name)
        {
            var written = CustomWebControlReader.Read(Written.Replace("Plant.Pump", name));

            var failure = Assert.ThrowsException<PortalException>(() => CustomWebControlCatalog.RequireWritten(written));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void RequireWritten_ANameOfItsOwn_ReturnsIt()
        {
            var written = CustomWebControlReader.Read(Written);

            Assert.AreSame(written, CustomWebControlCatalog.RequireWritten(written));
        }

        private const string Written = @"{ ""name"": ""Plant.Pump"", ""displayName"": ""Pump"", ""width"": 100, ""height"": 80,
            ""html"": ""<div id='root'></div>"", ""css"": ""#root { }"", ""script"": ""function render(p) { }"" }";
    }
}
