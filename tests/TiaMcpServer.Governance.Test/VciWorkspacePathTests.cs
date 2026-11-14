using System;
using TiaMcpServer.Vci;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>V21 takes a workspace folder only written from a leading backslash (measured 2026-11-13).</remarks>
    [TestClass]
    public sealed class VciWorkspacePathTests
    {
        [TestMethod]
        public void ForOpenness_ThreeNames_StartsWithABackslash()
        {
            Assert.AreEqual(@"\PLC_1\Blocks\Motors", VciWorkspacePath.ForOpenness(new[] { "PLC_1", "Blocks", "Motors" }));
        }

        [TestMethod]
        public void ForOpenness_NoName_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => VciWorkspacePath.ForOpenness(Array.Empty<string>()));
        }

        [TestMethod]
        [DataRow("Motor/Pump", "Motor_Pump")]
        [DataRow("A:B*C?", "A_B_C_")]
        [DataRow(" Main ", "Main")]
        [DataRow("..", "_")]
        [DataRow("", "_")]
        public void SafeName_ANameAFolderCannotHold_IsMadeHoldable(string name, string expected)
        {
            Assert.AreEqual(expected, VciWorkspacePath.SafeName(name));
        }

        /// <remarks>A root with a closing separator is longer than an object's folder at the root: cutting by length threw.</remarks>
        [TestMethod]
        [DataRow("C:\\Repos\\Cell\\", "C:\\Repos\\Cell", "")]
        [DataRow("C:\\Repos\\Cell\\", "C:\\Repos\\Cell\\PLC_1\\Blocks", "PLC_1/Blocks")]
        [DataRow("C:/Repos/Cell", "C:\\Repos\\Cell\\PLC_1", "PLC_1")]
        [DataRow("C:\\Repos\\Cell", "C:\\Repos\\Cellar\\PLC_1", "C:/Repos/Cellar/PLC_1")]
        public void RelativeFolder_AnObjectFolder_IsGivenBelowTheRoot(string root, string folder, string expected)
        {
            Assert.AreEqual(expected, VciWorkspacePath.RelativeFolder(root, folder));
        }
    }
}
