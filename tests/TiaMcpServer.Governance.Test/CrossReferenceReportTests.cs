using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Sorting cross references by direction needs no TIA Portal, and it is the half of the read
    /// that can be got wrong silently: "UsedBy" and "Uses" are one letter apart in the source and
    /// opposite in meaning, and a caller that reads the wrong list changes a block believing
    /// nothing depends on it.
    ///
    /// The third bucket is the other rule under test. Openness has thirteen relations; two of them
    /// are the ones above, and a reference filed under any of the remaining eleven must still be
    /// visible. Dropping it would be the read-side version of a silent default.
    /// </remarks>
    [TestClass]
    public sealed class CrossReferenceReportTests
    {
        [TestMethod]
        public void Of_AMixOfRelations_PutsEachOneInItsOwnList()
        {
            var report = CrossReferenceReport.Of(new[]
            {
                Reference("FC_Caller", "UsedBy"),
                Reference("FB_Called", "Uses"),
                Reference("DB_Instance", "InstanceType")
            });

            Assert.AreEqual("FC_Caller", report.Incoming.Single().Name);
            Assert.AreEqual("FB_Called", report.Outgoing.Single().Name);
            Assert.AreEqual("DB_Instance", report.Other.Single().Name);
        }

        /// <remarks>
        /// The rule this class exists for. A relation nobody foresaw is reported, never discarded:
        /// a block used through one of them is used, and an empty Incoming list would say it is
        /// not.
        /// </remarks>
        [TestMethod]
        public void Of_AnUnrecognisedRelation_IsKeptRatherThanDropped()
        {
            var report = CrossReferenceReport.Of(new[] { Reference("FC_Odd", "SomeRelationFromAFutureVersion") });

            Assert.AreEqual(1, report.Count);
            Assert.AreEqual("FC_Odd", report.Other.Single().Name);
        }

        [TestMethod]
        public void Of_NoReferences_IsAnEmptyReportRatherThanAFailure()
        {
            var report = CrossReferenceReport.Of(new List<CrossReferenceInfo>());

            Assert.AreEqual(0, report.Count);
            Assert.AreEqual(0, report.Incoming.Count);
        }

        [TestMethod]
        public void Of_Null_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CrossReferenceReport.Of(null!));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// The summary is what a caller reads before the lists, and it is the one place the three
        /// counts appear together. A bucket missing from it would hide exactly the references the
        /// third list exists to keep.
        /// </remarks>
        [TestMethod]
        public void Summary_EachBucketFilled_CountsAllThree()
        {
            var report = CrossReferenceReport.Of(new[]
            {
                Reference("FC_Caller", "UsedBy"),
                Reference("FB_Called", "Uses"),
                Reference("DB_Instance", "InstanceType")
            });

            Assert.AreEqual("1 use(s) of it, 1 thing(s) it uses, 1 other relation(s)", report.Summary);
        }

        /// <remarks>
        /// One line per place, and every field on it: the direction and the access are what tell a
        /// caller whether a use is a call it must keep working or a read it can move.
        /// </remarks>
        [TestMethod]
        public void Line_AReference_NamesTheObjectTheRelationAndWhere()
        {
            var reference = new CrossReferenceInfo(
                "FC_Caller",
                "Cell/FC_Caller",
                "FC",
                new CrossReferenceUsage("UsedBy", "Call", "NW1", "%FC1"));

            Assert.AreEqual("FC_Caller | Cell/FC_Caller | FC | UsedBy | Call | NW1 | %FC1", reference.Line);
        }

        private static CrossReferenceInfo Reference(string name, string referenceType)
        {
            return new CrossReferenceInfo(
                name,
                $"Cell/{name}",
                "FC",
                new CrossReferenceUsage(referenceType, "Call", "NW1", string.Empty));
        }
    }
}
