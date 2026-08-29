using System;
using System.Collections.ObjectModel;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The cardinal rule of the knowledge layer is that the system cites and does not author, and
    /// that a gap is never filled. Every test here names one way that rule is kept structurally —
    /// by there being no way to build a context that claims something it cannot show.
    /// </remarks>
    [TestClass]
    public class HardwareContextTests
    {
        [TestMethod]
        public void Cited_NoCitations_Refuses()
        {
            // A "cited" answer with nothing to show is exactly the shape a fabricated one would
            // take: an authoritative outcome and no source anybody can open.
            Assert.ThrowsException<ArgumentException>(() => HardwareContext.Cited(Array.Empty<HardwareCitation>()));
        }

        [TestMethod]
        public void Unavailable_NoReason_Refuses()
        {
            // Without a reason, a broken lookup and an unconfigured machine read identically.
            Assert.ThrowsException<ArgumentException>(() => HardwareContext.Unavailable("   "));
        }

        [TestMethod]
        public void NotFound_Always_SaysToOpenTheManual()
        {
            var context = HardwareContext.NotFound();

            Assert.AreEqual(HardwareContextOutcome.NotFound, context.Outcome);
            Assert.AreEqual(0, context.Citations.Count);
            StringAssert.Contains(context.Summarise(), "Open the manufacturer", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Summarise_UnrecognisedOutcome_Throws()
        {
            // The exhaustive-switch rule of the governance layer, asserted rather than assumed. A
            // fourth outcome added without a case here has to break loudly, instead of being
            // described as "no hardware context" — which a reader would take for documented silence.
            var context = HardwareContext.WithOutcome((HardwareContextOutcome)99);

            Assert.ThrowsException<InvalidOperationException>(() => context.Summarise());
        }

        [TestMethod]
        public void Citations_OfACitedContext_AreReadOnly()
        {
            // Evidence attached to a plan that a later caller can append to is not evidence.
            var context = HardwareContext.Cited(new[] { Citation() });

            Assert.IsInstanceOfType(context.Citations, typeof(ReadOnlyCollection<HardwareCitation>));
        }

        [TestMethod]
        public void Summarise_Cited_NamesTheDocumentAndThePage()
        {
            // What the stage is for: a plan a student can follow back to a page of a real manual.
            var summary = HardwareContext.Cited(new[] { Citation() }).Summarise();

            StringAssert.Contains(summary, "Universal Robots e-Series User Manual UR5e", StringComparison.Ordinal);
            StringAssert.Contains(summary, "SW 5.16", StringComparison.Ordinal);
            StringAssert.Contains(summary, "page 47", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Citation_WithoutPage_Refuses()
        {
            // A quote nobody can go and check is the thing this layer exists to avoid producing.
            Assert.ThrowsException<ArgumentException>(
                () => new HardwareCitation(Document(), 0, "configurable I/O can be set as safety-related"));
        }

        [TestMethod]
        public void Citation_WithoutText_Refuses()
        {
            Assert.ThrowsException<ArgumentException>(() => new HardwareCitation(Document(), 47, "  "));
        }

        private static SourceDocument Document()
        {
            return new SourceDocument("UR5e", "Universal Robots e-Series User Manual UR5e", "SW 5.16");
        }

        private static HardwareCitation Citation()
        {
            return new HardwareCitation(Document(), 47, "configurable I/O can be set as safety-related");
        }
    }
}
