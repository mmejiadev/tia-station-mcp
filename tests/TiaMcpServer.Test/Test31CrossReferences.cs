using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Who calls what: the question to ask before rewriting a block.
    /// </summary>
    /// <remarks>
    /// The relation under test is built by the test rather than looked for in the fixture. A test
    /// that asserted "something in TestProject1 calls FC_Block_1" would pass or fail on what the
    /// sample project happens to contain, which is not what is being measured; two blocks written
    /// here, one calling the other, make the expected answer known before the question is asked.
    ///
    /// The program is compiled before the references are read. Cross-reference data is derived
    /// rather than stored, and a compile is the state in which TIA Portal certainly has it.
    /// Whether it is strictly required was not isolated, so it is done rather than assumed away.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test31CrossReferences
    {
        private const string CalleeName = "FC_CalleeByTest";
        private const string CallerName = "FC_CallerByTest";
        private const string LonelyName = "FC_LonelyByTest";

        private const string CallerAndCallee = @"FUNCTION ""FC_CalleeByTest"" : Void
VERSION : 0.1
BEGIN
    ; // deliberately empty body
END_FUNCTION

FUNCTION ""FC_CallerByTest"" : Void
VERSION : 0.1
BEGIN
    ""FC_CalleeByTest""();
END_FUNCTION
";

        private const string NobodyCallsIt = @"FUNCTION ""FC_LonelyByTest"" : Void
VERSION : 0.1
BEGIN
    ; // deliberately empty body
END_FUNCTION
";

        private string _backupDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _backupDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>
        /// The whole point of the tool. A model handed the callee's source sees an empty function
        /// and no reason not to change its signature; this is the answer that says otherwise.
        /// </remarks>
        [TestMethod]
        public void GetCrossReferences_ABlockCalledByAnother_NamesTheCaller()
        {
            var report = ReferencesOf(CallerAndCallee, CalleeName);

            Assert.IsTrue(
                report.Incoming.Any(reference => reference.Name == CallerName),
                $"The caller is not among what uses the callee: {Describe(report)}");
        }

        /// <remarks>
        /// The same relation read from the other end. Both directions come out of one Openness
        /// call, and the flattening that produces them is exactly what could get them the wrong
        /// way round, so the opposite end is asserted rather than assumed.
        /// </remarks>
        [TestMethod]
        public void GetCrossReferences_TheCallingBlock_NamesWhatItCalls()
        {
            var report = ReferencesOf(CallerAndCallee, CallerName);

            Assert.IsTrue(
                report.Outgoing.Any(reference => reference.Name == CalleeName),
                $"The callee is not among what the caller uses: {Describe(report)}");
        }

        /// <remarks>
        /// A column that is always blank is worse than no column: it reads as "TIA does not know"
        /// rather than as "this reader asked for the wrong field". Openness offers two properties
        /// that could be the location of a use and their documentation does not separate them;
        /// this is what makes the choice a measurement.
        /// </remarks>
        [TestMethod]
        public void GetCrossReferences_ACall_SaysWhereItHappens()
        {
            var report = ReferencesOf(CallerAndCallee, CalleeName);

            var call = report.Incoming.FirstOrDefault(reference => reference.Name == CallerName);

            Assert.IsNotNull(call, $"The caller is not among what uses the callee: {Describe(report)}");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(call.Usage.Location),
                $"The use carries no location: {call.Line}");
        }

        /// <remarks>
        /// An empty answer is an answer. This is the one a caller about to delete a block wants,
        /// and reporting it as a failure would make the safe case look like a broken one.
        /// </remarks>
        [TestMethod]
        public void GetCrossReferences_ABlockNobodyCalls_ReportsNoUseRatherThanFailing()
        {
            var report = ReferencesOf(NobodyCallsIt, LonelyName);

            Assert.AreEqual(0, report.Incoming.Count, $"Something uses a block nothing calls: {Describe(report)}");
        }

        [TestMethod]
        public void GetCrossReferences_AnUnknownBlock_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetCrossReferences(Settings.Project1PlcSoftwarePath0, "NoSuchBlock"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        private CrossReferenceReport ReferencesOf(string scl, string blockPath)
        {
            AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, scl, _backupDirectory);

            var compiled = AssemblyHooks.SharedPortal.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(
                compiled.IsSuccessful,
                $"The fixture did not compile, so nothing can be read from it:\n{string.Join("\n", compiled.Errors)}");

            return AssemblyHooks.SharedPortal.GetCrossReferences(Settings.Project1PlcSoftwarePath0, blockPath);
        }

        /// <remarks>
        /// Every line of the report, not a count. A failure here is about what TIA reported, and
        /// the only way to learn it from a test run is to have it printed.
        /// </remarks>
        private static string Describe(CrossReferenceReport report)
        {
            var lines = report.Incoming
                .Concat(report.Outgoing)
                .Concat(report.Other)
                .Select(reference => reference.Line);

            return $"{report.Summary}\n{string.Join("\n", lines)}";
        }
    }
}
