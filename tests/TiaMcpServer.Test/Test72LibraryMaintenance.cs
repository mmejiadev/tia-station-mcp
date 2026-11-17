using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Keeping the project library tidy: discard a version in work, choose the default, delete a version or a type, clean
    /// up, and see where the types are used.
    /// </summary>
    /// <remarks>
    /// On the class project 0967_1, as Test58. What each operation does and what TIA Portal refuses was measured on
    /// 2026-11-15 by a probe on a copy of another class project with six types.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test72LibraryMaintenance
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Motor = "Faceplate_BOTO_MOTOR_V1";

        private const string InWorkOnly = "FACEPLATE_MOTOR_V1";

        private static readonly Version First = new Version(0, 0, 1);

        private static readonly Version Second = new Version(0, 0, 2);

        private static readonly string[] OnlyFirst = { "0.0.1" };

        private static readonly string[] OnlySecond = { "0.0.2" };

        private static readonly string[] FirstAndSecond = { "0.0.1", "0.0.2" };

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
        public void DiscardLibraryTypeEdit_AVersionInWork_LeavesOnlyTheReleasedOnes()
        {
            ReleaseFirstAndStartSecond();

            var type = AssemblyHooks.SharedPortal.DiscardLibraryTypeEdit(Motor, Backup());

            CollectionAssert.AreEqual(OnlyFirst, Versions(type));
        }

        [TestMethod]
        public void DiscardLibraryTypeEdit_Discarded_ArchivesTheProjectFirst()
        {
            ReleaseFirstAndStartSecond();
            var backup = Backup();

            AssemblyHooks.SharedPortal.DiscardLibraryTypeEdit(Motor, backup);

            Assert.IsTrue(Directory.EnumerateFiles(backup, "*.zap*").Any(), "no project archive in " + backup);
        }

        /// <remarks>TIA Portal archives only a saved project, and the archive is the backup; nothing is touched without it.</remarks>
        [TestMethod]
        public void DiscardLibraryTypeEdit_UnsavedChanges_IsRefusedAndKeepsTheVersionInWork()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            AssemblyHooks.SharedPortal.EditLibraryType(Motor, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DiscardLibraryTypeEdit(Motor, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "SaveProject", StringComparison.Ordinal);
            CollectionAssert.AreEqual(FirstAndSecond, Versions(TypeNamed(Motor)));
        }

        [TestMethod]
        public void DiscardLibraryTypeEdit_NothingInWork_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            AssemblyHooks.SharedPortal.SaveProject();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DiscardLibraryTypeEdit(Motor, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void SetDefaultLibraryTypeVersion_TheOlderVersion_BecomesTheDefault()
        {
            ReleaseTwo();

            var type = AssemblyHooks.SharedPortal.SetDefaultLibraryTypeVersion(Motor, First, Backup());

            Assert.AreEqual("0.0.1", DefaultOf(type));
        }

        [TestMethod]
        public void DeleteLibraryTypeVersion_NotTheDefault_LeavesTheOtherVersion()
        {
            ReleaseTwo();
            AssemblyHooks.SharedPortal.SaveProject();

            var type = AssemblyHooks.SharedPortal.DeleteLibraryTypeVersion(Motor, First, Backup());

            CollectionAssert.AreEqual(OnlySecond, Versions(type));
        }

        [TestMethod]
        public void DeleteLibraryTypeVersion_TheDefault_IsRefusedBeforeTheArchive()
        {
            ReleaseTwo();
            AssemblyHooks.SharedPortal.SaveProject();
            var backup = Backup();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DeleteLibraryTypeVersion(Motor, Second, backup));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            Assert.IsFalse(Directory.Exists(backup), "a refused delete left an archive in " + backup);
        }

        /// <remarks>TIA Portal's own refusal, passed on as an invalid state with its reason.</remarks>
        [TestMethod]
        public void DeleteLibraryType_WithAnInstanceOnAScreen_IsRefusedByTiaPortal()
        {
            PlaceOnVersionOne();
            AssemblyHooks.SharedPortal.SaveProject();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DeleteLibraryType(Motor, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "TIA Portal refused", StringComparison.Ordinal);
        }

        /// <remarks>The answer is built from what was read before the delete: the type itself is gone by then.</remarks>
        [TestMethod]
        public void DeleteLibraryType_AnUnusedReleasedType_IsGoneAndNamedInTheAnswer()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(InWorkOnly, null, string.Empty, Backup());
            AssemblyHooks.SharedPortal.SaveProject();

            var result = AssemblyHooks.SharedPortal.DeleteLibraryType(InWorkOnly, Backup());

            Assert.AreEqual(InWorkOnly, result["deleted"]!.GetValue<string>());
            Assert.IsFalse(AssemblyHooks.SharedPortal.GetLibraryTypes(string.Empty)["types"]!.AsArray().Any(type => type!["name"]!.GetValue<string>() == InWorkOnly));
        }

        /// <remarks>Measured 2026-11-15: TIA Portal refuses it too; refused here first so nothing is archived.</remarks>
        [TestMethod]
        public void DeleteLibraryType_AVersionInWork_IsRefusedNamingDiscard()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DeleteLibraryType(InWorkOnly, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "DiscardLibraryTypeEdit", StringComparison.Ordinal);
        }

        /// <remarks>Measured 2026-11-15 on this project: a never-released faceplate goes with its only version.</remarks>
        [TestMethod]
        public void DiscardLibraryTypeEdit_TheOnlyVersionInWork_RemovesTheType()
        {
            var result = AssemblyHooks.SharedPortal.DiscardLibraryTypeEdit(InWorkOnly, Backup());

            Assert.AreEqual(InWorkOnly, result["deleted"]!.GetValue<string>());
            Assert.IsFalse(AssemblyHooks.SharedPortal.GetLibraryTypes(string.Empty)["types"]!.AsArray().Any(type => type!["name"]!.GetValue<string>() == InWorkOnly));
        }

        [TestMethod]
        public void GetLibraryTypeUsage_AnInstanceOnAScreen_IsListedWithItsVersion()
        {
            PlaceOnVersionOne();

            var usages = AssemblyHooks.SharedPortal.GetLibraryTypeUsage()["usages"]!.AsArray();

            var row = usages.Single(usage => usage!["type"]!.GetValue<string>() == Motor)!;
            StringAssert.EndsWith(row["instance"]!.GetValue<string>(), "FP_M2", StringComparison.Ordinal);
            Assert.AreEqual("0.0.1", row["instanceVersion"]!.GetValue<string>());
        }

        [TestMethod]
        public void CleanUpProjectLibrary_AnUnusedOlderVersion_IsRemovedAndNamed()
        {
            ReleaseTwo();
            AssemblyHooks.SharedPortal.SaveProject();

            var result = AssemblyHooks.SharedPortal.CleanUpProjectLibrary(false, Backup());

            var removed = result["removed"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
            CollectionAssert.Contains(removed, Motor + " 0.0.1");
            CollectionAssert.AreEqual(OnlySecond, Versions(TypeNamed(Motor)));
        }

        private void ReleaseFirstAndStartSecond()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            AssemblyHooks.SharedPortal.EditLibraryType(Motor, Backup());
            AssemblyHooks.SharedPortal.SaveProject();
        }

        private void ReleaseTwo()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            AssemblyHooks.SharedPortal.EditLibraryType(Motor, Backup());
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
        }

        private void PlaceOnVersionOne()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            AssemblyHooks.SharedPortal.PlaceUnifiedFaceplates(HmiPath, UnifiedFaceplateListReader.Parse(@"{ ""screen"": ""Principal"", ""faceplates"": [
                { ""name"": ""FP_M2"", ""type"": ""Faceplate_BOTO_MOTOR_V1"", ""left"": 0, ""top"": 0, ""interface"": { ""Interface_MOTOR"": { ""tag"": ""M2"" } } } ] }"), Backup());
        }

        private static JsonObject TypeNamed(string name)
        {
            return (JsonObject)AssemblyHooks.SharedPortal.GetLibraryTypes(string.Empty)["types"]!.AsArray().Single(type => type!["name"]!.GetValue<string>() == name)!;
        }

        private static string[] Versions(JsonObject type)
        {
            return type["versions"]!.AsArray().Select(version => version!["version"]!.GetValue<string>()).ToArray();
        }

        private static string DefaultOf(JsonObject type)
        {
            return type["versions"]!.AsArray().Single(version => version!["isDefault"]!.GetValue<bool>())!["version"]!.GetValue<string>();
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
