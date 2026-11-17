using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.CustomWebControls;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>WinCC Unified Custom Web Controls from the catalogue: installed in a project and placed on a screen.</summary>
    /// <remarks>
    /// On the class project 0967_1, as Test58. What installing and placing need was measured on 2026-11-15 with a pilot
    /// light written from scratch: recognised only once the project is opened again, placed by its name, bound on its
    /// interface property, compiled with no error, and working in runtime.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test73CustomWebControls
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Screen = "Principal";

        private static readonly string[] OnlyLamp = { "Lamp_1" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.FaceplateArchivePath, Path.Combine(_testDirectory, "project"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void InstallCustomWebControl_FromTheCatalogue_ReopensTheProjectAndListsIt()
        {
            var installed = AssemblyHooks.SharedPortal.InstallCustomWebControl(CustomWebControlCatalog.Get("Motor"), Backup());

            Assert.IsTrue(installed["reopened"]!.GetValue<bool>());
            CollectionAssert.Contains(Installed(), "TiaStation.Motor");
        }

        /// <remarks>Installing closes and opens the project again, which would drop what is not saved.</remarks>
        [TestMethod]
        public void InstallCustomWebControl_UnsavedChanges_IsRefusedAndWritesNothing()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType("Faceplate_BOTO_MOTOR_V1", null, string.Empty, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.InstallCustomWebControl(CustomWebControlCatalog.Get("Motor"), Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            CollectionAssert.DoesNotContain(Installed(), "TiaStation.Motor");
        }

        [TestMethod]
        public void InstallCustomWebControl_Again_ReplacesItAndKeepsTheOldPackage()
        {
            AssemblyHooks.SharedPortal.InstallCustomWebControl(CustomWebControlCatalog.Get("Valve"), Backup());
            var backup = Backup();

            AssemblyHooks.SharedPortal.InstallCustomWebControl(CustomWebControlCatalog.Get("Valve"), backup);

            Assert.AreEqual(1, Installed().Count(name => name == "TiaStation.Valve"));
            Assert.IsTrue(Directory.EnumerateFiles(backup, "*.zip").Any(), "the replaced package was not kept in " + backup);
        }

        [TestMethod]
        public void PlaceCustomWebControls_AnInstalledControl_IsOnTheScreenAndTheNameIsThenTaken()
        {
            InstallTheLampAndItsTag();

            var placed = AssemblyHooks.SharedPortal.PlaceCustomWebControls(HmiPath, Lamp(), Backup());

            CollectionAssert.AreEqual(OnlyLamp, placed["placed"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray());
            var again = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.PlaceCustomWebControls(HmiPath, Lamp(), Backup()));
            StringAssert.Contains(again.Message, "already has an item", StringComparison.Ordinal);
        }

        /// <remarks>Measured 2026-11-15: a control bound on its interface property and given an event script compiles with no error.</remarks>
        [TestMethod]
        public void PlaceCustomWebControls_ValuesTagAndScript_TheHmiCompilesWithNoErrorAboutIt()
        {
            InstallTheLampAndItsTag();

            AssemblyHooks.SharedPortal.PlaceCustomWebControls(HmiPath, Lamp(), Backup());

            var errors = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath).Errors.Select(message => message.Path + " " + message.Description).ToList();
            Assert.IsFalse(errors.Any(error => error.IndexOf("Lamp_1", StringComparison.Ordinal) >= 0), string.Join(" | ", errors));
        }

        [TestMethod]
        public void PlaceCustomWebControls_AControlNotInstalled_IsRefusedNamingInstall()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.PlaceCustomWebControls(HmiPath, Lamp(), Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "InstallCustomWebControl", StringComparison.Ordinal);
        }

        /// <remarks>TIA Portal itself only finds a missing tag when the HMI is compiled.</remarks>
        [TestMethod]
        public void PlaceCustomWebControls_ATagTheHmiLacks_IsRefusedBeforeAnythingIsPlaced()
        {
            AssemblyHooks.SharedPortal.InstallCustomWebControl(CustomWebControlCatalog.Get("PilotLight"), Backup());
            var list = CustomWebControlPlacementReader.Read(@"{ ""screen"": ""Principal"", ""controls"": [ { ""name"": ""Lamp_1"", ""control"": ""TiaStation.PilotLight"", ""tags"": { ""IsOn"": ""NoSuchTag"" } } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.PlaceCustomWebControls(HmiPath, list, Backup()));

            StringAssert.Contains(failure.Message, "NoSuchTag", StringComparison.Ordinal);
        }

        /// <remarks>Checked before the first is created: the first control of the list is not left on the screen.</remarks>
        [TestMethod]
        public void PlaceCustomWebControls_ASecondControlWithAWrongProperty_PlacesNeither()
        {
            InstallTheLampAndItsTag();
            var list = CustomWebControlPlacementReader.Read(@"{ ""screen"": ""Principal"", ""controls"": [
                { ""name"": ""Lamp_1"", ""control"": ""TiaStation.PilotLight"" },
                { ""name"": ""Lamp_2"", ""control"": ""TiaStation.PilotLight"", ""properties"": { ""Brightness"": 5 } } ] }");

            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.PlaceCustomWebControls(HmiPath, list, Backup()));

            var placed = AssemblyHooks.SharedPortal.PlaceCustomWebControls(HmiPath, Lamp(), Backup());
            Assert.AreEqual(1, placed["placed"]!.AsArray().Count);
        }

        /// <remarks>An internal Bool tag of its own for the lamp: the project's HMI tags are all of a PLC data type.</remarks>
        private void InstallTheLampAndItsTag()
        {
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""LampOn"", ""dataType"": ""Bool"" } ] }"));
            AssemblyHooks.SharedPortal.SaveProject();
            AssemblyHooks.SharedPortal.InstallCustomWebControl(CustomWebControlCatalog.Get("PilotLight"), Backup());
        }

        private static CustomWebControlPlacementList Lamp()
        {

            return CustomWebControlPlacementReader.Read(@"{ ""screen"": """ + Screen + @""", ""controls"": [ { ""name"": ""Lamp_1"", ""control"": ""TiaStation.PilotLight"", ""left"": 40, ""top"": 40,
                ""properties"": { ""Label"": ""M1"", ""OnColor"": ""0, 120, 255"" }, ""tags"": { ""IsOn"": ""LampOn"" },
                ""events"": { ""Clicked"": ""HMIRuntime.Trace('clicked');"" } } ] }");
        }

        private static string[] Installed()
        {
            return AssemblyHooks.SharedPortal.GetCustomWebControls()["controls"]!.AsArray().Select(control => control!["name"]!.GetValue<string>()).ToArray();
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
