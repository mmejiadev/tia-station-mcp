using System;
using System.Diagnostics;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The filter several export and listing tools take from the caller. It runs inside the
    /// Openness gate, so what it does badly it does to the whole process: these tests are about
    /// what happens when the expression is hostile or wrong, not about matching names.
    /// </remarks>
    [TestClass]
    public sealed class NameFilterTests
    {
        [TestMethod]
        public void Parse_AnInvalidExpression_IsInvalidParamsRatherThanAnOperationFailure()
        {
            // It used to arrive as ArgumentException, which the portal layer turned into an
            // operation failure - telling the caller the environment broke and to retry, when they
            // had simply typed a bracket wrong.
            var failure = Assert.ThrowsException<PortalException>(() => NameFilter.Parse("FB_["));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "not a valid name filter", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Matches_AnExpressionThatBacktracksForEver_GivesUpInsteadOfHangingTheServer()
        {
            // The reason this class exists. Without a match timeout this call does not return in
            // any useful amount of time, and it holds the Openness gate while it does not: every
            // other tool in the process waits on one bad pattern, with nothing in the log.
            var filter = NameFilter.Parse("(a+)+$");
            var clock = Stopwatch.StartNew();

            var failure = Assert.ThrowsException<PortalException>(
                () => filter.Matches(new string('a', 40) + "!"));

            clock.Stop();

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            Assert.IsTrue(
                clock.Elapsed < TimeSpan.FromSeconds(20),
                $"the match was not bounded: it took {clock.Elapsed}");
        }

        [TestMethod]
        public void Matches_AnEmptyFilter_AcceptsEverything()
        {
            // What every caller already relied on, stated once here instead of as a null check at
            // each of them.
            Assert.IsTrue(NameFilter.Parse(string.Empty).Matches("FB_Station"));
            Assert.IsTrue(NameFilter.Parse("   ").Matches("anything at all"));
        }

        [TestMethod]
        public void Matches_AnOrdinaryFilter_StillFiltersCaseInsensitively()
        {
            // The behaviour the tools had before, unchanged: the hardening must not quietly narrow
            // what a working filter matches.
            var filter = NameFilter.Parse("^fb_");

            Assert.IsTrue(filter.Matches("FB_Station"));
            Assert.IsFalse(filter.Matches("DB_Station"));
        }
    }
}
