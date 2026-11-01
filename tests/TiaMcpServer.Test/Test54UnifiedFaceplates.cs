using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Test
{
    /// <summary>Releasing faceplate types and placing their instances on a WinCC Unified screen (HMI-COVERAGE C16, ROADMAP U8).</summary>
    /// <remarks>
    /// On the class project 0967_1, whose two faceplate types were left in work. What each refusal stands
    /// for was measured on 2026-10-30 by probes on copies of the same project.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test54UnifiedFaceplates
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Motor = "Faceplate_BOTO_MOTOR_V1";

        private const string MotorAndName = @"{ ""screen"": ""Principal"", ""faceplates"": [
            { ""name"": ""FP_M2"", ""type"": ""Faceplate_BOTO_MOTOR_V1"", ""left"": 200, ""top"": 0,
              ""interface"": { ""Interface_MOTOR"": { ""tag"": ""M2"" }, ""NOM_MOTOR"": ""Motor 2"" } } ] }";

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
        public void ReleaseLibraryType_AFaceplateInWork_IsReleasedUnderItsOwnNumber()
        {
            var released = AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, "first release", Backup());

            var version = released["versions"]!.AsArray().Single()!;
            Assert.AreEqual("0.0.1", version["version"]!.GetValue<string>());
            Assert.AreEqual("released", version["state"]!.GetValue<string>());
            Assert.AreEqual("V0.0.1\\" + Motor, version["faceplateReference"]!.GetValue<string>());
        }

        [TestMethod]
        public void ReleaseLibraryType_ATypeWithNothingInWork_IsRefusedAsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.ReleaseLibraryType("UDT_MOTOR_V1", null, string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "nothing to release", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void PlaceUnifiedFaceplates_AReleasedFaceplate_ReadsBackItsTypeFrameAndInterface()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());

            var placed = (JsonObject)Place(MotorAndName)["faceplates"]!.AsArray().Single()!;

            Assert.AreEqual(Motor, placed["type"]!.GetValue<string>());
            Assert.AreEqual("0.0.1", placed["version"]!.GetValue<string>());
            Assert.AreEqual(200, placed["left"]!.GetValue<int>());
            Assert.AreEqual("M2", placed["interface"]!["Interface_MOTOR"]!.GetValue<string>());
            Assert.AreEqual("Motor 2", placed["interface"]!["NOM_MOTOR"]!.GetValue<string>());
        }

        /// <remarks>The rest of 0967_1 compiled with no error of its own on 2026-10-30; a tag of the wrong type would show here.</remarks>
        [TestMethod]
        public void PlaceUnifiedFaceplates_AFaceplateOnAnExistingUdtTag_CompilesWithNoErrorOfItsOwn()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            Place(MotorAndName);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var faceplateErrors = report.Messages
                .Where(message => message.Severity == CompilationSeverity.Error && (message.Path + message.Description).Contains("FP_M2"))
                .Select(message => message.ToString())
                .ToArray();
            Assert.AreEqual(0, faceplateErrors.Length, string.Join(" | ", faceplateErrors));
        }

        [TestMethod]
        public void PlaceUnifiedFaceplates_AVersionStillInWork_IsRefusedAndPlacesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Place(MotorAndName));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "ReleaseLibraryType", System.StringComparison.Ordinal);
            AssertNothingPlaced();
        }

        [TestMethod]
        public void PlaceUnifiedFaceplates_ATagThatDoesNotExist_IsRefusedAndPlacesNothing()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => Place(MotorAndName.Replace("\"M2\"", "\"No_Such_Tag\"")));

            StringAssert.Contains(failure.Message, "'No_Such_Tag', which does not exist", System.StringComparison.Ordinal);
            AssertNothingPlaced();
        }

        /// <remarks>
        /// The first faceplate is placed before the second fails in TIA Portal, so this is the undo at work,
        /// not the check: the property names a type has are known only once an instance exists.
        /// </remarks>
        [TestMethod]
        public void PlaceUnifiedFaceplates_AnInterfacePropertyTheTypeLacks_RemovesWhatWasPlacedBefore()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            const string Two = @"{ ""screen"": ""Principal"", ""faceplates"": [
                { ""name"": ""FP_M1"", ""type"": ""Faceplate_BOTO_MOTOR_V1"", ""left"": 0, ""top"": 0, ""interface"": { ""Interface_MOTOR"": { ""tag"": ""M1"" } } },
                { ""name"": ""FP_M2"", ""type"": ""Faceplate_BOTO_MOTOR_V1"", ""left"": 200, ""top"": 0, ""interface"": { ""No_Such_Property"": ""x"" } } ] }";

            var failure = Assert.ThrowsException<PortalException>(() => Place(Two));

            StringAssert.Contains(failure.Message, "has no interface property 'No_Such_Property'; it has: Interface_MOTOR, NOM_MOTOR", System.StringComparison.Ordinal);
            StringAssert.Contains(failure.Message, "removed again", System.StringComparison.Ordinal);
            AssertNothingPlaced();
        }

        [TestMethod]
        public void GetLibraryTypes_TheProjectLibrary_TellsAPlainTypeFromAPlcDataType()
        {
            var types = AssemblyHooks.SharedPortal.GetLibraryTypes(string.Empty)["types"]!.AsArray();

            Assert.AreEqual("LibraryType", Kind(types, Motor));
            Assert.AreEqual("PlcTypeLibraryType", Kind(types, "UDT_MOTOR_V1"));
        }

        private static string Kind(JsonArray types, string name)
        {
            return types.Single(type => type!["name"]!.GetValue<string>() == name)!["kind"]!.GetValue<string>();
        }

        private JsonObject Place(string json)
        {
            return AssemblyHooks.SharedPortal.PlaceUnifiedFaceplates(HmiPath, UnifiedFaceplateListReader.Parse(json), Backup());
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup");
        }

        private static void AssertNothingPlaced()
        {
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetUnifiedFaceplates(HmiPath)["faceplates"]!.AsArray().Count);
        }
    }
}
