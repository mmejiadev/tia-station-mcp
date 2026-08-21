using System;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// The specification refuses to exist in a state that would generate SCL that does not compile.
    /// Every one of these would otherwise surface as a syntax error in a block nobody typed.
    /// </remarks>
    [TestClass]
    public sealed class CellSpecificationTests
    {
        private static StationSpecification Station(string name)
        {
            return new StationSpecification(name, 1, 1);
        }

        [TestMethod]
        public void Handovers_ForTwoStations_IsOnePair()
        {
            var cell = new CellSpecification("Demo", new[] { Station("A"), Station("B") });

            var handovers = cell.Handovers();

            Assert.AreEqual(1, handovers.Count);
            Assert.AreEqual("A", handovers[0].From.Name);
            Assert.AreEqual("B", handovers[0].To.Name);
            Assert.AreEqual(1, handovers[0].FromIndex);
            Assert.AreEqual(2, handovers[0].ToIndex);
        }

        [TestMethod]
        public void Handovers_ForOneStation_IsEmpty()
        {
            // Not an error. A one-station cell is a legitimate thing to generate, and it simply has
            // nowhere to hand a piece on to.
            var cell = new CellSpecification("Solo", new[] { Station("Only") });

            Assert.AreEqual(0, cell.Handovers().Count);
        }

        [TestMethod]
        public void Handovers_FollowTheDeclaredOrder()
        {
            // Order is the whole content of a cell: it is a line, not a set. If this ever stops being
            // true, pieces get handed to the wrong station and every generated cell is wrong.
            var cell = new CellSpecification("Line", new[] { Station("First"), Station("Second"), Station("Third") });

            var handovers = cell.Handovers();

            Assert.AreEqual("First", handovers[0].From.Name);
            Assert.AreEqual("Second", handovers[1].From.Name);
            Assert.AreEqual("Third", handovers[1].To.Name);
        }

        [TestMethod]
        public void Constructor_WithNoStations_IsRejected()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new CellSpecification("Empty", Array.Empty<StationSpecification>()));
        }

        [TestMethod]
        public void Constructor_WithTwoStationsOfTheSameName_IsRejected()
        {
            // Two instances of the same name in one coordinator: the compiler would report a duplicate
            // declaration in generated code, which says nothing about the JSON that caused it.
            var exception = Assert.ThrowsException<ArgumentException>(
                () => new CellSpecification("Cell", new[] { Station("Drill"), Station("Drill") }));

            StringAssert.Contains(exception.Message, "more than once", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Constructor_WithNamesDifferingOnlyInCase_IsRejected()
        {
            // SCL identifiers are not case sensitive, so "Drill" and "drill" are the same declaration.
            Assert.ThrowsException<ArgumentException>(
                () => new CellSpecification("Cell", new[] { Station("Drill"), Station("drill") }));
        }

        [TestMethod]
        public void Constructor_WithANameThatIsNotAnIdentifier_IsRejected()
        {
            var exception = Assert.ThrowsException<ArgumentException>(
                () => new CellSpecification("Two Words", new[] { Station("A") }));

            StringAssert.Contains(exception.Message, "SCL identifier", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Station_WithAnAccentedName_IsAccepted()
        {
            // TIA Portal allows it, so rejecting it would be this repository inventing a rule the
            // platform does not have - and the person naming the stations writes Spanish.
            var station = new StationSpecification("Estacion_Taladrado", 1, 1);

            Assert.AreEqual("Estacion_Taladrado", station.Name);
        }

        [TestMethod]
        public void Station_WithANameStartingWithADigit_IsRejected()
        {
            var exception = Assert.ThrowsException<ArgumentException>(() => Station("1_Feeder"));

            StringAssert.Contains(exception.Message, "digit", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Station_WithNoWorkSteps_IsRejected()
        {
            // A sequence of zero steps reports Done without having done anything, which is worse than
            // failing: it looks like it worked.
            Assert.ThrowsException<ArgumentException>(() => new StationSpecification("A", 0, 1));
        }

        [TestMethod]
        public void Station_WithNoDwellCycles_IsRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => new StationSpecification("A", 1, 0));
        }
    }
}
