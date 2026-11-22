using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.IO;
using System.Reflection;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Test
{
    /// <summary>A screen's own event handlers: what a redraw of its items may and may not do with them.</summary>
    /// <remarks>
    /// On a new PC station with WinCC Unified PC RT. The description of a screen has no events of its own, only its items',
    /// and neither has any MCP tool a way to write one, so the handler is created through Openness here. The review of
    /// 2026-11-22 feared that a redraw with no event in any item would write TIA Portal's default over the area the screen's
    /// own handlers share. Measured the same day: it never gets that far, because the backup does not keep a screen's own
    /// event handlers and the redraw is refused before it deletes or writes anything.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test77UnifiedScreenOwnEvents
    {
        private const string StationName = "PC-System_1";
        private const string HmiPath = "PC-System_1/HMI_RT_1";
        private const string ScreenName = "Own";
        private const string OwnDefinitions = "let own = 1;";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedScreenOwnEvents");
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPcV21);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void DrawUnifiedScreen_ScreenWithItsOwnEvent_IsRefusedAndItsDefinitionsStay()
        {
            Draw(@"{ ""type"": ""button"", ""name"": ""First"" }");
            GiveTheScreenAnEventHolding(OwnDefinitions);

            var refused = Assert.ThrowsException<PortalException>(() => Draw(@"{ ""type"": ""button"", ""name"": ""Second"" }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, refused.Code);
            StringAssert.Contains(refused.Message, "EventHandlers", StringComparison.Ordinal);
            Assert.AreEqual(OwnDefinitions, ScreenEventDefinitions());
        }

        private static void GiveTheScreenAnEventHolding(string definitions)
        {
            var handler = Screen().EventHandlers.Create(HmiScreenEventType.Loaded);

            handler.Script.ScriptCode = "HMIRuntime.Trace('loaded');";
            handler.Script.GlobalDefinitionAreaScriptCode = definitions;
        }

        private static string ScreenEventDefinitions()
        {
            foreach (var handler in Screen().EventHandlers)
            {
                return handler.Script.GlobalDefinitionAreaScriptCode.Trim();
            }

            throw new AssertFailedException("The screen lost its own event handler.");
        }

        /// <remarks>The server's own lookup, reached by name: the test project holds no other way to the HMI's software.</remarks>
        private static HmiScreen Screen()
        {
            var lookup = typeof(Portal).GetMethod("RequireUnifiedSoftware", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new AssertFailedException("Portal.RequireUnifiedSoftware is gone: this test reaches the HMI's software through it.");
            var software = (UnifiedHmiSoftware)lookup.Invoke(AssemblyHooks.SharedPortal, new object[] { HmiPath })!;

            return software.Screens.Find(ScreenName) ?? throw new AssertFailedException($"The screen '{ScreenName}' is not there.");
        }

        private void Draw(string items)
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": """ + ScreenName + @""", ""startScreen"": true, ""items"": [ " + items + " ] }");

            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
        }
    }
}
