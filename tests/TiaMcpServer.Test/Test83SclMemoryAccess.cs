using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>The PEEK and POKE rules that need the program: which DBs exist, and which of those named are optimized.</summary>
    /// <remarks>
    /// On a copy of the fixture. <c>CheckSclMemoryAccess</c> only reads; the DBs it judges against are written first with
    /// <c>WriteScl</c>, whose generated DB numbers are read back rather than assumed.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test83SclMemoryAccess
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;
        private const string OptimizedDb = "TiaMcpPeekOptimized";
        private const string StandardDb = "TiaMcpPeekStandard";
        private const int NumberNoDbHas = 60000;

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>Measured 2026-11-18 (M6): a DB that does not exist, read by number, returns 0 with ENO FALSE.</remarks>
        [TestMethod]
        public void CheckSclMemoryAccess_ADbNumberNoBlockHas_IsReportedMissing()
        {
            var findings = AssemblyHooks.SharedPortal.CheckSclMemoryAccess(Software, Peek(NumberNoDbHas));

            Assert.AreEqual("peek-poke-missing-db", findings.Single().Rule);
        }

        [TestMethod]
        public void CheckSclMemoryAccess_AnOptimizedDbByNumber_IsReportedOptimized()
        {
            var number = WriteDataBlock(OptimizedDb, string.Empty);

            var findings = AssemblyHooks.SharedPortal.CheckSclMemoryAccess(Software, Peek(number));

            Assert.AreEqual("peek-poke-optimized-db", findings.Single().Rule);
        }

        [TestMethod]
        public void CheckSclMemoryAccess_AStandardDbByNumber_FindsNothing()
        {
            var number = WriteDataBlock(StandardDb, "{ S7_Optimized_Access := 'FALSE' }");

            var findings = AssemblyHooks.SharedPortal.CheckSclMemoryAccess(Software, Peek(number));

            Assert.AreEqual(0, findings.Count, string.Join("; ", findings));
        }

        private static string Peek(int number)
        {
            return $"FUNCTION \"TiaMcpPeekCaller\" : Void\nVAR_TEMP\n    b : Byte;\nEND_VAR\nBEGIN\n    #b := PEEK(AREA := 16#84, DBNUMBER := {number}, BYTEOFFSET := 0);\nEND_FUNCTION";
        }

        private int WriteDataBlock(string name, string attributes)
        {
            var source = $"DATA_BLOCK \"{name}\"\n{attributes}\nVERSION : 0.1\n   VAR\n      Value : Int;\n   END_VAR\nBEGIN\nEND_DATA_BLOCK";
            AssemblyHooks.SharedPortal.WriteScl(Software, source, Path.Combine(_testDirectory, "backup-" + name));

            var block = AssemblyHooks.SharedPortal.GetBlock(Software, name) ?? throw new AssertFailedException($"'{name}' was not written");

            return Convert.ToInt32(block.Attributes.Single(attribute => attribute.Name == "Number").Value, CultureInfo.InvariantCulture);
        }
    }
}
