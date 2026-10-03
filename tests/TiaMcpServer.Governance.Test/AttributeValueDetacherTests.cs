using System;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The engineering objects Openness hands back cannot be built without TIA Portal, so the one
    /// that matters is imitated: an object whose property leads back to itself, the shape that made
    /// GetDevices fail on a class project.
    /// </remarks>
    [TestClass]
    public sealed class AttributeValueDetacherTests
    {
        [TestMethod]
        public void Detach_AnObjectThatLeadsBackToItself_BecomesTextThatSerializes()
        {
            var live = new SelfReferencingObject();

            var detached = AttributeValueDetacher.Detach(live);

            Assert.AreEqual(live.ToString(), JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(detached)));
        }

        [TestMethod]
        public void Detach_APlainValue_KeepsItsType()
        {
            var detached = AttributeValueDetacher.Detach(1000000L);

            Assert.AreEqual(1000000L, detached);
        }

        [TestMethod]
        public void Detach_AnEnumeration_KeepsItsType()
        {
            var detached = AttributeValueDetacher.Detach(DayOfWeek.Monday);

            Assert.AreEqual(DayOfWeek.Monday, detached);
        }

        /// <remarks>
        /// A copy rather than the array itself: an array is mutable, and the value is meant to stay
        /// what was read.
        /// </remarks>
        [TestMethod]
        public void Detach_AnArrayOfText_KeepsTheElementsInACopy()
        {
            var read = new[] { "dns-1", "dns-2" };

            var detached = AttributeValueDetacher.Detach(read);

            CollectionAssert.AreEqual(read, (string[])detached!);
            Assert.AreNotSame(read, detached);
        }

        [TestMethod]
        public void Detach_AnArrayOfObjects_BecomesText()
        {
            var detached = AttributeValueDetacher.Detach(new[] { new SelfReferencingObject() });

            Assert.IsInstanceOfType(detached, typeof(string));
        }

        [TestMethod]
        public void Detach_NoValue_ReturnsNull()
        {
            var detached = AttributeValueDetacher.Detach(null);

            Assert.IsNull(detached);
        }

        private sealed class SelfReferencingObject
        {
            public SelfReferencingObject Parent => this;

            public override string ToString()
            {
                return "Siemens.Engineering.HW.DeviceItemImpl";
            }
        }
    }
}
