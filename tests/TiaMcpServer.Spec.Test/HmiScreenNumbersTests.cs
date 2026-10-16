using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>The API does not give a classic screen's number, so the export is where it is read.</remarks>
    [TestClass]
    public sealed class HmiScreenNumbersTests
    {
        private static string Asset(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "hmi", name));
        }

        [TestMethod]
        public void Read_TeachersPrincipal_IsNumberTwo()
        {
            var numbers = HmiScreenNumbers.Read(new[] { Asset("principal-reference.xml") });

            Assert.AreEqual(2, numbers.Single(entry => entry.Key == "principal").Value);
        }

        /// <remarks>A backup holds tag tables and text lists beside the screens; they are not screens.</remarks>
        [TestMethod]
        public void Read_TextListExport_IsSkipped()
        {
            var numbers = HmiScreenNumbers.Read(new[] { Asset("screennames-reference.xml") });

            Assert.AreEqual(0, numbers.Count);
        }
    }
}
