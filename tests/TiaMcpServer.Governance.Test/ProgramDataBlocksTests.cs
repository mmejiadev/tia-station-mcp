using TiaMcpServer.Scl;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>What the PEEK and POKE rules may say of a DB number depends on whether the program's DBs were read at all.</remarks>
    [TestClass]
    public sealed class ProgramDataBlocksTests
    {
        [TestMethod]
        public void IsMissing_WhenNothingWasRead_IsFalse()
        {
            Assert.IsFalse(ProgramDataBlocks.Unknown.IsMissing(7));
        }

        [TestMethod]
        [DataRow(7L, true)]
        [DataRow(2L, false)]
        [DataRow(4294967295L, true)]
        public void IsMissing_WhenTheProgramWasRead_IsWhetherNoDbHasTheNumber(long number, bool expected)
        {
            var dataBlocks = ProgramDataBlocks.Of(new[] { 2, 3 }, new[] { 3 });

            Assert.AreEqual(expected, dataBlocks.IsMissing(number));
        }

        [TestMethod]
        [DataRow(3L, true)]
        [DataRow(2L, false)]
        [DataRow(4294967299L, false)]
        public void IsOptimized_OnlyANumberTheProjectNames_IsTrue(long number, bool expected)
        {
            var dataBlocks = ProgramDataBlocks.Of(new[] { 2, 3 }, new[] { 3 });

            Assert.AreEqual(expected, dataBlocks.IsOptimized(number));
        }
    }
}
