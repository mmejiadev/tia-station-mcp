using System;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class PlanIdTests
    {
        /// <remarks>
        /// A struct can always be produced as <c>default</c>, which runs no constructor, and nothing
        /// stops a caller from doing it — a field left unassigned is enough. The identifier that
        /// comes out has to behave like an identifier that matches nothing, not like a null waiting
        /// for the next line to dereference it.
        ///
        /// This test is also what defends the suppression of IDE0032 on the backing field: the
        /// analyzer offers to make it an auto property, and an auto property cannot do this.
        /// </remarks>
        [TestMethod]
        public void Value_DefaultIdentifier_IsEmptyRatherThanNull()
        {
            var identifier = default(PlanId);

            Assert.AreEqual(string.Empty, identifier.Value);
            Assert.AreEqual(string.Empty, identifier.ToString());
        }

        [TestMethod]
        public void Equals_DefaultIdentifier_MatchesNoRealPlan()
        {
            var real = PlanId.Create();

            Assert.AreNotEqual(real, default(PlanId));
            Assert.IsFalse(real == default(PlanId));
        }

        [TestMethod]
        public void Create_TwoIdentifiers_AreNotTheSame()
        {
            var first = PlanId.Create();
            var second = PlanId.Create();

            Assert.AreNotEqual(first, second, "two plans in one session would share a confirmation");
        }

        /// <remarks>
        /// Read off one screen and typed on another, so shift and stray spaces must not decide
        /// whether a change is confirmed.
        /// </remarks>
        [TestMethod]
        public void Parse_SameIdentifierTypedBackCarelessly_IsTheSameIdentifier()
        {
            var asPrinted = PlanId.Parse("K7M-2QX");

            var asTyped = PlanId.Parse("  k7m-2qx  ");

            Assert.AreEqual(asPrinted, asTyped);
        }

        [TestMethod]
        public void Parse_Empty_ThrowsRatherThanReturningAnIdentifierThatMatchesNothing()
        {
            Assert.ThrowsException<ArgumentException>(() => PlanId.Parse("   "));
        }
    }
}
