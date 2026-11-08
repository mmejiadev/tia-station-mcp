using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using TiaMcpServer.Secrets;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The values here are made up for the test.</remarks>
    [TestClass]
    public sealed class SecretCommandTests
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
        [DataRow("secret", true)]
        [DataRow("SECRET", true)]
        [DataRow("--logging", false)]
        public void IsFor_TheFirstArgument_Decides(string first, bool expected)
        {
            Assert.AreEqual(expected, SecretCommand.IsFor(new[] { first }));
        }

        [TestMethod]
        public void IsFor_NoArguments_IsFalse()
        {
            Assert.IsFalse(SecretCommand.IsFor(Array.Empty<string>()));
        }

        [TestMethod]
        public void Set_TheSameValueTwice_StoresItAndNeverWritesIt()
        {
            var console = new FakeConsole(Value, Value);

            var exit = Run(console, "secret", "set", "PLC_1");

            Assert.AreEqual(0, exit);
            CollectionAssert.Contains(_store.Names().ToList(), "PLC_1");
            Assert.IsFalse(console.Written.Any(line => line.Contains(Value)), "the value was written");
        }

        [TestMethod]
        public void Set_TwoDifferentValues_StoresNothing()
        {
            var exit = Run(new FakeConsole(Value, "something-else"), "secret", "set", "PLC_1");

            Assert.AreEqual(1, exit);
            Assert.AreEqual(0, _store.Names().Count);
        }

        [TestMethod]
        public void Set_NoPersonAtTheTerminal_AsksForNothingAndStoresNothing()
        {
            var console = new FakeConsole(Value, Value) { IsInteractive = false };

            var exit = Run(console, "secret", "set", "PLC_1");

            Assert.AreEqual(1, exit);
            Assert.AreEqual(0, console.Reads);
            Assert.AreEqual(0, _store.Names().Count);
        }

        /// <remarks>On the command line it would stay in the shell's history, and it must not be repeated either.</remarks>
        [TestMethod]
        public void Set_AValueOnTheCommandLine_IsRefusedAndNotRepeated()
        {
            var console = new FakeConsole();

            var exit = Run(console, "secret", "set", "PLC_1", Value);

            Assert.AreEqual(1, exit);
            Assert.AreEqual(0, console.Reads);
            Assert.AreEqual(0, _store.Names().Count);
            Assert.IsFalse(console.Written.Any(line => line.Contains(Value)), "the value was repeated");
        }

        [TestMethod]
        public void Set_ANameHeld_IsRefusedUnlessAskedToReplace()
        {
            Run(new FakeConsole(Value, Value), "secret", "set", "PLC_1");

            var refused = Run(new FakeConsole("other-value", "other-value"), "secret", "set", "PLC_1");
            var replaced = Run(new FakeConsole("other-value", "other-value"), "secret", "set", "PLC_1", "--replace");

            Assert.AreEqual(1, refused);
            Assert.AreEqual(0, replaced);
        }

        [TestMethod]
        public void Set_ABadName_IsRefusedAndNotRepeated()
        {
            var console = new FakeConsole(Value, Value);

            var exit = Run(console, "secret", "set", "pass word!");

            Assert.AreEqual(1, exit);
            Assert.AreEqual(0, console.Reads, "a value was asked for a name that cannot be stored");
            Assert.IsFalse(console.Written.Any(line => line.Contains("pass word!")), "the name was repeated");
        }

        [TestMethod]
        public void Set_ANameHeldWithoutReplace_AsksForNoValue()
        {
            Run(new FakeConsole(Value, Value), "secret", "set", "PLC_1");
            var console = new FakeConsole();

            var exit = Run(console, "secret", "set", "PLC_1");

            Assert.AreEqual(1, exit);
            Assert.AreEqual(0, console.Reads);
        }

        /// <remarks>What the person typed in a name's place could be the value.</remarks>
        [TestMethod]
        public void SetAndDelete_DoNotRepeatTheName()
        {
            var console = new FakeConsole(Value, Value);

            Run(console, "secret", "set", "Looks_Like_A_Value");
            Run(console, "secret", "delete", "Looks_Like_A_Value");
            Run(console, "secret", "delete", "Looks_Like_A_Value");

            Assert.IsFalse(console.Written.Any(line => line.Contains("Looks_Like_A_Value")), "the name was repeated");
        }

        [TestMethod]
        public void Run_ADiskFailure_ExitsWithAFailureAndAMessage()
        {
            var console = new FakeConsole();

            var exit = new SecretCommand(new BrokenStore(), console).Run(new[] { "secret", "list" });

            Assert.AreEqual(1, exit);
            StringAssert.Contains(console.Written.Single(), "disk", StringComparison.Ordinal);
        }

        [TestMethod]
        public void List_WritesTheNames()
        {
            Run(new FakeConsole(Value, Value), "secret", "set", "PLC_1");
            var console = new FakeConsole();

            var exit = Run(console, "secret", "list");

            Assert.AreEqual(0, exit);
            CollectionAssert.AreEqual(new[] { "PLC_1" }, console.Written);
        }

        [TestMethod]
        public void Delete_ASecret_TakesItAway()
        {
            Run(new FakeConsole(Value, Value), "secret", "set", "PLC_1");

            var exit = Run(new FakeConsole(), "secret", "delete", "PLC_1");

            Assert.AreEqual(0, exit);
            Assert.AreEqual(0, _store.Names().Count);
        }

        [TestMethod]
        public void Delete_NoSuchSecret_ExitsWithAFailure()
        {
            Assert.AreEqual(1, Run(new FakeConsole(), "secret", "delete", "PLC_1"));
        }

        [TestMethod]
        [DataRow("secret")]
        [DataRow("secret", "nonsense", "typed-by-mistake")]
        public void Run_NotACommand_ShowsTheUsageAndRepeatsNothing(params string[] args)
        {
            var console = new FakeConsole();

            var exit = new SecretCommand(_store, console).Run(args);

            Assert.AreEqual(1, exit);
            StringAssert.Contains(console.Written.Single(), "Usage", StringComparison.Ordinal);
            Assert.IsFalse(console.Written.Single().Contains("typed-by-mistake"), "what was typed was repeated");
        }

        private int Run(FakeConsole console, params string[] args)
        {
            return new SecretCommand(_store, console).Run(args);
        }

        private sealed class BrokenStore : ISecretStore
        {
            public IReadOnlyList<string> Names()
            {
                throw new IOException("the disk failed");
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
                throw new NotSupportedException();
            }

            public void Delete(string name)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeConsole : ISecretConsole
        {
            private readonly Queue<string> _values;

            public FakeConsole(params string[] values)
            {
                _values = new Queue<string>(values);
            }

            public bool IsInteractive { get; set; } = true;

            public int Reads { get; private set; }

            public List<string> Written { get; } = new List<string>();

            public SecureString ReadHidden(string prompt)
            {
                Reads++;

                return DpapiSecretStoreTests.Secure(_values.Dequeue());
            }

            public void WriteLine(string message)
            {
                Written.Add(message);
            }
        }
    }
}
