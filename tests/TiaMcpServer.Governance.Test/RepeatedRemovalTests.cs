using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Library;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The undo of a failed library copy: a type another still depends on cannot go first, and the walk order
    /// says nothing of dependencies. The review of 2026-10-30 found one pass leaving the data type behind.
    /// </remarks>
    [TestClass]
    public sealed class RepeatedRemovalTests
    {
        [TestMethod]
        public void Run_AChainWalkedDependencyFirst_RemovesEverything()
        {
            var store = new FakeLibrary(("UDT", null), ("FB", "UDT"), ("FP", "FB"));

            var left = RepeatedRemoval.Run(new[] { "UDT", "FB", "FP" }, store.TryDelete);

            Assert.AreEqual(0, left.Count);
        }

        [TestMethod]
        public void Run_AnItemThatCanNeverGo_IsGivenBackAndTheRestRemoved()
        {
            var store = new FakeLibrary(("UDT", null), ("FP", "UDT"));
            store.Locked.Add("FP");

            var left = RepeatedRemoval.Run(new[] { "UDT", "FP" }, store.TryDelete);

            CollectionAssert.AreEquivalent(new[] { "UDT", "FP" }, left.ToArray());
        }

        [TestMethod]
        public void Run_Nothing_GivesNothingBack()
        {
            Assert.AreEqual(0, RepeatedRemoval.Run(Array.Empty<string>(), _ => true).Count);
        }

        /// <summary>A type is deleted only when no type left depends on it, as TIA Portal refuses otherwise.</summary>
        private sealed class FakeLibrary
        {
            private readonly Dictionary<string, string?> _dependsOn;

            public FakeLibrary(params (string Name, string? DependsOn)[] types)
            {
                _dependsOn = types.ToDictionary(type => type.Name, type => type.DependsOn);
            }

            public HashSet<string> Locked { get; } = new HashSet<string>();

            public bool TryDelete(string name)
            {
                if (Locked.Contains(name) || _dependsOn.ContainsValue(name))
                {
                    return false;
                }

                _dependsOn.Remove(name);
                return true;
            }
        }
    }
}
