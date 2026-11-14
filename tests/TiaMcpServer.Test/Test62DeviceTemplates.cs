using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.Secrets;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>A PLC and an HMI with their integrated connection, as a template in a global library.</summary>
    /// <remarks>What each refusal stands for was measured on 2026-11-08 by probes on a copy. The passwords here are made up.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test62DeviceTemplates
    {
        private const string Group = "Group_1";

        private const string Template = "Template_PLC_HMI";

        private const string Secret = "Plc_Test";

        private const string Password = "Made-Up-For-Tests-1";

        private const string WeakSecret = "Plc_Weak";

        private const string WeakPassword = "weakvalue";

        private static string _library = string.Empty;

        private string _testDirectory = string.Empty;

        /// <remarks>The folder is short on purpose: with the archive's name, TIA Portal refuses a path over 143 characters.</remarks>
        [ClassInitialize]
        public static void MakeTheTemplate(TestContext context)
        {
            var directory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.HmiTemplateArchivePath, Path.Combine(directory, "p"));
            _library = AssemblyHooks.SharedPortal.CreateGlobalLibrary(Path.Combine(directory, "libraries"), "Kit")["library"]!.GetValue<string>();
            AssemblyHooks.SharedPortal.CreateDeviceGroupTemplate(Group, _library, Template, Path.Combine(directory, "backup"));
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "FromTemplate");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateDeviceGroupTemplate_AGroup_IsInTheLibraryUnderItsName()
        {
            var names = AssemblyHooks.SharedPortal.GetMasterCopies(_library)["masterCopies"]!.AsArray().Select(copy => copy!["name"]!.GetValue<string>());

            CollectionAssert.Contains(names.ToList(), Template);
        }

        [TestMethod]
        public void CreateDevicesFromTemplate_WithASecret_ConnectsAndCompilesWithNoErrors()
        {
            var placed = Place("PN_Cell", Secret);

            Assert.AreEqual(Group, placed["group"]!.GetValue<string>());
            Assert.AreEqual(2, placed["joined"]!.AsArray().Count);
            Assert.AreEqual(1, placed["protectedPlcs"]!.AsArray().Count);
            Assert.AreEqual(0, Errors(placed), placed.ToJsonString());
        }

        [TestMethod]
        public void CreateDevicesFromTemplate_NoSecret_NamesThePlcAndKeepsWhatWasPlaced()
        {
            var placed = Place("PN_Cell", string.Empty);

            Assert.AreEqual(1, placed["plcsWithoutPassword"]!.AsArray().Count);
            Assert.AreEqual(0, placed["protectedPlcs"]!.AsArray().Count);
            Assert.IsTrue(Errors(placed) > 0, "a PLC with no password compiled");
            Assert.AreEqual(2, AssemblyHooks.SharedPortal.GetSoftwarePaths().Count);
        }

        [TestMethod]
        public void CreateDevicesFromTemplate_ASecretNotStored_IsNotFoundAndPlacesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Place("PN_Cell", "No_Such_Secret"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetSoftwarePaths().Count);
        }

        /// <remarks>TIA Portal refuses it; what was placed goes, and its own message must not carry the value.</remarks>
        [TestMethod]
        public void CreateDevicesFromTemplate_AValueTiaPortalRefuses_LeavesNothingAndDoesNotRepeatIt()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Place("PN_Cell", WeakSecret));

            StringAssert.Contains(failure.Message, "Nothing was created", StringComparison.Ordinal);
            Assert.IsFalse(failure.Message.Contains(WeakPassword), failure.Message);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetSoftwarePaths().Count);
        }

        /// <remarks>TIA Portal repeats a template's addresses on a second placement, without a word (measured 2026-11-08).</remarks>
        [TestMethod]
        public void CreateDevicesFromTemplate_TwiceOnOneSubnet_IsRefusedAndKeepsTheFirst()
        {
            Place("PN_Cell", Secret);

            var failure = Assert.ThrowsException<PortalException>(() => Place("PN_Cell", Secret));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.AreEqual(2, AssemblyHooks.SharedPortal.GetSoftwarePaths().Count);
        }

        [TestMethod]
        public void CreateDevicesFromTemplate_TwiceOnTwoSubnets_SaysTiaPortalRenamedIt()
        {
            Place("PN_One", Secret);

            var second = Place("PN_Two", Secret);

            Assert.IsTrue(second["renamedByTiaPortal"]!.GetValue<bool>());
            Assert.AreEqual("Group_2", second["group"]!.GetValue<string>());
        }

        [TestMethod]
        public void CreateDevicesFromTemplate_ATemplateNotThere_IsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateDevicesFromTemplate(new DeviceTemplatePlacement(_library, "No_Such_Template", "PN", string.Empty), new MadeUpSecrets(), AssemblyHooks.CreateTestDirectory()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
        }

        /// <remarks>
        /// Joining a subnet already in the project changes it, so the network as it stood is kept first: the record holds
        /// the first placement's PLC and not the second's, which TIA Portal names PLC_2.
        /// </remarks>
        [TestMethod]
        public void CreateDevicesFromTemplate_PlacedAgain_RecordsTheNetworkAsItStoodBefore()
        {
            Place("PN_One", Secret);
            var backup = AssemblyHooks.CreateTestDirectory();

            AssemblyHooks.SharedPortal.CreateDevicesFromTemplate(new DeviceTemplatePlacement(_library, Template, "PN_Two", Secret), new MadeUpSecrets(), backup);

            var recorded = File.ReadAllText(Path.Combine(backup, "network", "topology.txt"));
            StringAssert.Contains(recorded, "PLC_1", StringComparison.Ordinal);
            Assert.IsFalse(recorded.Contains("PLC_2"), recorded);
        }

        [TestMethod]
        public void CreateDeviceGroupTemplate_AGroupNotThere_IsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateDeviceGroupTemplate("No_Such_Group", _library, "Another", Path.Combine(_testDirectory, "backup")));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
        }

        /// <remarks>
        /// SetPlcFullAccess's safety rule: a PLC whose confidential data has a password was protected on purpose, and
        /// removing it needs that password. The placement protects the template's PLC with the stored secret.
        /// </remarks>
        [TestMethod]
        public void SetPlcFullAccess_PlcProtectedWithAPassword_ThrowsInvalidState()
        {
            Place("PN_Cell", Secret);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetPlcFullAccess(Group + "/S7-1200 station_1/PLC_1", Path.Combine(_testDirectory, "backup")));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        private static JsonObject Place(string subnet, string secret)
        {
            return AssemblyHooks.SharedPortal.CreateDevicesFromTemplate(new DeviceTemplatePlacement(_library, Template, subnet, secret), new MadeUpSecrets(), AssemblyHooks.CreateTestDirectory());
        }

        private static int Errors(JsonObject placed)
        {
            return placed["compile"]!.AsArray().Sum(device => device!["errors"]!.GetValue<int>());
        }

        private sealed class MadeUpSecrets : ISecretStore
        {
            private static readonly Dictionary<string, string> Values = new Dictionary<string, string>
            {
                [Secret] = Password,
                [WeakSecret] = WeakPassword
            };

            public IReadOnlyList<string> Names()
            {
                return Values.Keys.ToList();
            }

            public void CheckCanStore(string name, bool replace)
            {
                throw new NotSupportedException();
            }

            public void Store(string name, SecureString value, bool replace)
            {
                throw new NotSupportedException();
            }

            public SecureString Reveal(string name)
            {
                if (!Values.TryGetValue(name, out var text))
                {
                    throw new PortalException(PortalErrorCode.NotFound, "There is no secret of that name");
                }

                var value = new SecureString();
                foreach (var character in text)
                {
                    value.AppendChar(character);
                }

                return value;
            }

            public void Delete(string name)
            {
                throw new NotSupportedException();
            }
        }
    }
}
