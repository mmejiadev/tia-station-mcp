using System;
using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.OpcUa.Test
{
    [TestClass]
    public class ServerCertificatePinsTests
    {
        private const string Target = "opcua/192.168.0.1:4840";

        private string _directory = string.Empty;

        [TestInitialize]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "tia-mcp-pins-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void DeleteDirectory()
        {
            Directory.Delete(_directory, true);
        }

        [TestMethod]
        public void Verify_NothingOnFile_RecordsTheCertificateAsAFirstUse()
        {
            var pins = new ServerCertificatePins(Path.Combine(_directory, "pins.json"));

            var verdict = pins.Verify(Target, "ab12");

            Assert.AreEqual(PinVerdict.FirstUse, verdict);
            Assert.AreEqual("AB12", pins.Pinned(Target));
        }

        [TestMethod]
        public void Verify_TheSameCertificateAgain_Matches()
        {
            var pins = new ServerCertificatePins(Path.Combine(_directory, "pins.json"));
            pins.Verify(Target, "AB12");

            var verdict = pins.Verify(Target, "ab12");

            Assert.AreEqual(PinVerdict.Matches, verdict);
        }

        [TestMethod]
        public void Verify_ADifferentCertificateLater_IsReportedAsChangedAndNotRecorded()
        {
            var pins = new ServerCertificatePins(Path.Combine(_directory, "pins.json"));
            pins.Verify(Target, "AB12");

            var verdict = pins.Verify(Target, "CD34");

            Assert.AreEqual(PinVerdict.Changed, verdict);
            Assert.AreEqual("AB12", pins.Pinned(Target));
        }

        [TestMethod]
        public void Verify_AFileWrittenByAnotherInstance_IsHonoured()
        {
            var path = Path.Combine(_directory, "pins.json");
            new ServerCertificatePins(path).Verify(Target, "AB12");

            var verdict = new ServerCertificatePins(path).Verify(Target, "CD34");

            Assert.AreEqual(PinVerdict.Changed, verdict);
        }

        [TestMethod]
        public void Verify_AnUnreadablePinFile_RefusesRatherThanStartingAgainFromNothing()
        {
            var path = Path.Combine(_directory, "pins.json");
            File.WriteAllText(path, "{ this is not json");
            var pins = new ServerCertificatePins(path);

            var exception = Assert.ThrowsException<PortalException>(() => pins.Verify(Target, "AB12"));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }

        [TestMethod]
        public void Verify_TwoServers_ArePinnedIndependently()
        {
            var pins = new ServerCertificatePins(Path.Combine(_directory, "pins.json"));
            pins.Verify(Target, "AB12");

            var verdict = pins.Verify("opcua/192.168.0.2:4840", "CD34");

            Assert.AreEqual(PinVerdict.FirstUse, verdict);
        }
    }
}
