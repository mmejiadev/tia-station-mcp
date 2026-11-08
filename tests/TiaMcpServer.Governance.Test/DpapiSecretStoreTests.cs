using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using TiaMcpServer.Secrets;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The rules that keep a secret out of the open. The values here are made up for the test.</remarks>
    [TestClass]
    public sealed class DpapiSecretStoreTests
    {
        private const string Value = "not-a-real-secret-42";

        private string _root = string.Empty;

        private DpapiSecretStore _store = null!;

        [TestInitialize]
        public void TestInit()
        {
            _root = Path.Combine(Path.GetTempPath(), "TiaMcpSecrets" + Guid.NewGuid().ToString("N"));
            _store = new DpapiSecretStore(_root);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [TestMethod]
        public void Set_ThenReveal_GivesTheValueBack()
        {
            Store("PLC_1");

            using var revealed = _store.Reveal("PLC_1");

            Assert.AreEqual(Value, Plain(revealed));
        }

        [TestMethod]
        public void Set_TheFile_HoldsNothingOfTheValue()
        {
            Store("PLC_1");

            var file = File.ReadAllBytes(Path.Combine(_root, "PLC_1.secret"));

            Assert.IsFalse(Contains(file, Encoding.Unicode.GetBytes(Value)), "the value is in the file as UTF-16");
            Assert.IsFalse(Contains(file, Encoding.UTF8.GetBytes(Value)), "the value is in the file as UTF-8");
        }

        [TestMethod]
        public void Reveal_AFileRenamedToAnotherSecret_IsRefused()
        {
            Store("PLC_1");
            File.Move(Path.Combine(_root, "PLC_1.secret"), Path.Combine(_root, "PLC_2.secret"));

            var refusal = Assert.ThrowsException<PortalException>(() => _store.Reveal("PLC_2"));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void Reveal_AFileThatIsNotEncrypted_IsRefusedAndSaysNothingOfIt()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "PLC_1.secret"), "garbage-content");

            var refusal = Assert.ThrowsException<PortalException>(() => _store.Reveal("PLC_1"));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            Assert.IsFalse(refusal.Message.Contains("garbage-content"), refusal.Message);
        }

        [TestMethod]
        public void Reveal_NoSuchSecret_IsNotFound()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => _store.Reveal("PLC_1"));

            Assert.AreEqual(PortalErrorCode.NotFound, refusal.Code);
        }

        [TestMethod]
        public void Set_ANameHeld_IsRefusedAndKeepsTheValue()
        {
            Store("PLC_1");
            using var other = Secure("another-made-up-value");

            var refusal = Assert.ThrowsException<PortalException>(() => _store.Store("PLC_1", other, replace: false));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            using var revealed = _store.Reveal("PLC_1");
            Assert.AreEqual(Value, Plain(revealed));
        }

        [TestMethod]
        public void Set_ReplaceAskedFor_ReplacesTheValue()
        {
            Store("PLC_1");
            using var other = Secure("another-made-up-value");

            _store.Store("PLC_1", other, replace: true);

            using var revealed = _store.Reveal("PLC_1");
            Assert.AreEqual("another-made-up-value", Plain(revealed));
            Assert.AreEqual(1, Directory.GetFiles(_root).Length);
        }

        /// <remarks>A name becomes a file name, and a refusal must not echo what may be a value typed in the wrong place.</remarks>
        [TestMethod]
        [DataRow("")]
        [DataRow("1PLC")]
        [DataRow("../PLC")]
        [DataRow("..\\PLC")]
        [DataRow("PLC\n")]
        [DataRow("PLC 1")]
        [DataRow("CON")]
        [DataRow("lpt1")]
        public void EveryOperation_ANameThatCannotBeAFile_IsRefusedWithoutRepeatingIt(string name)
        {
            using var value = Secure(Value);

            var onSet = Assert.ThrowsException<PortalException>(() => _store.Store(name, value, replace: false));
            var onReveal = Assert.ThrowsException<PortalException>(() => _store.Reveal(name));
            var onDelete = Assert.ThrowsException<PortalException>(() => _store.Delete(name));

            Assert.AreEqual(PortalErrorCode.InvalidParams, onSet.Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, onReveal.Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, onDelete.Code);
            Assert.IsTrue(name.Length == 0 || !onSet.Message.Contains(name), onSet.Message);
        }

        [TestMethod]
        public void Delete_NoSuchSecret_DoesNotRepeatTheName()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => _store.Delete("Looks_Like_A_Value"));

            Assert.IsFalse(refusal.Message.Contains("Looks_Like_A_Value"), refusal.Message);
        }

        [TestMethod]
        public void CheckCanStore_ANameHeld_IsRefusedUnlessAskedToReplace()
        {
            Store("PLC_1");

            var refusal = Assert.ThrowsException<PortalException>(() => _store.CheckCanStore("PLC_1", replace: false));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            _store.CheckCanStore("PLC_1", replace: true);
        }

        [TestMethod]
        public void Store_LeavesOnlyTheSecretsFile()
        {
            Store("PLC_1");

            CollectionAssert.AreEqual(new[] { "PLC_1.secret" }, Directory.GetFiles(_root).Select(Path.GetFileName).ToArray());
        }

        [TestMethod]
        public void Set_AnEmptyValue_IsRefused()
        {
            using var empty = new SecureString();

            var refusal = Assert.ThrowsException<PortalException>(() => _store.Store("PLC_1", empty, replace: false));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void Set_AValueOverTheLimit_IsRefused()
        {
            using var tooLong = Secure(new string('a', DpapiSecretStore.MaxValueLength + 1));

            var refusal = Assert.ThrowsException<PortalException>(() => _store.Store("PLC_1", tooLong, replace: false));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void Names_NothingStored_IsEmpty()
        {
            Assert.AreEqual(0, _store.Names().Count);
        }

        [TestMethod]
        public void Names_TwoSecrets_AreListedInOrderAndNothingElse()
        {
            Store("PLC_2");
            Store("PLC_1");
            File.WriteAllText(Path.Combine(_root, "not secret.secret"), "x");
            File.WriteAllText(Path.Combine(_root, "other.txt"), "x");

            CollectionAssert.AreEqual(new[] { "PLC_1", "PLC_2" }, _store.Names().ToArray());
        }

        [TestMethod]
        public void Delete_ASecret_TakesItAway()
        {
            Store("PLC_1");

            _store.Delete("PLC_1");

            Assert.AreEqual(0, _store.Names().Count);
        }

        [TestMethod]
        public void Delete_NoSuchSecret_IsNotFound()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => _store.Delete("PLC_1"));

            Assert.AreEqual(PortalErrorCode.NotFound, refusal.Code);
        }

        [TestMethod]
        public void DefaultRoot_IsUnderTheAccountsOwnDataAndNotTheWorkingFolder()
        {
            StringAssert.StartsWith(DpapiSecretStore.GetDefaultRoot(), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase);
        }

        private void Store(string name)
        {
            using var value = Secure(Value);
            _store.Store(name, value, replace: false);
        }

        internal static SecureString Secure(string text)
        {
            var value = new SecureString();
            foreach (var character in text)
            {
                value.AppendChar(character);
            }

            return value;
        }

        private static string Plain(SecureString value)
        {
            var pointer = Marshal.SecureStringToGlobalAllocUnicode(value);

            try
            {
                return Marshal.PtrToStringUni(pointer)!;
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(pointer);
            }
        }

        private static bool Contains(byte[] haystack, byte[] needle)
        {
            return Enumerable.Range(0, haystack.Length - needle.Length + 1).Any(start => haystack.Skip(start).Take(needle.Length).SequenceEqual(needle));
        }
    }
}
