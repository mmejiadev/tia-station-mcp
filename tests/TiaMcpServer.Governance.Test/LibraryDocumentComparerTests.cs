using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Library;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// What two versions of a library type hold, compared from their SIMATIC SD documents: TIA Portal's own comparison of versions
    /// looks at nothing of their content (measured 2026-11-24).
    /// </remarks>
    [TestClass]
    public sealed class LibraryDocumentComparerTests
    {
        private const string Version1 = "FUNCTION \"FC_Motor\" : Void\r\n    // version one\r\nEND_FUNCTION\r\n";
        private const string Version2 = "FUNCTION \"FC_Motor\" : Void\r\n    // version two\r\nEND_FUNCTION\r\n";

        private static Dictionary<string, string> Documents(params (string Name, string Text)[] documents)
        {
            return documents.ToDictionary(document => document.Name, document => document.Text);
        }

        [TestMethod]
        public void Compare_TheSameTextWrittenDifferently_IsIdentical()
        {
            var left = Documents(("FC_Motor.s7dcl", Version1));
            var right = Documents(("fc_motor.s7dcl", Version1.Replace("\r\n", "\n").Replace("Void", "Void   ") + "\n\n"));

            var comparison = LibraryDocumentComparer.Compare(left, right);

            Assert.AreEqual(DocumentComparisonState.Identical, comparison.Single().State);
            Assert.IsTrue(LibraryDocumentComparer.AreIdentical(comparison));
        }

        [TestMethod]
        public void Compare_AChangedComment_IsDifferentAtItsLine()
        {
            var comparison = LibraryDocumentComparer.Compare(Documents(("FC_Motor.s7dcl", Version1)), Documents(("FC_Motor.s7dcl", Version2)));

            Assert.AreEqual(DocumentComparisonState.Different, comparison.Single().State);
            Assert.AreEqual(2, comparison.Single().FirstDifferentLine);
        }

        [TestMethod]
        public void Compare_ALineAddedAtTheEnd_IsDifferentAtTheFirstLineMissing()
        {
            var comparison = LibraryDocumentComparer.Compare(Documents(("A.s7dcl", "one")), Documents(("A.s7dcl", "one\r\ntwo")));

            Assert.AreEqual(2, comparison.Single().FirstDifferentLine);
        }

        [TestMethod]
        public void Compare_ADocumentOnOneSideOnly_SaysWhichSide()
        {
            var comparison = LibraryDocumentComparer.Compare(Documents(("A.s7dcl", "a"), ("B.s7res", "b")), Documents(("A.s7dcl", "a"), ("C.s7res", "c")));

            CollectionAssert.AreEqual(
                new[] { DocumentComparisonState.Identical, DocumentComparisonState.OnlyLeft, DocumentComparisonState.OnlyRight },
                comparison.Select(document => document.State).ToArray());
            Assert.IsFalse(LibraryDocumentComparer.AreIdentical(comparison));
        }
    }
}
