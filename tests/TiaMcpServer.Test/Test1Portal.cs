using Microsoft.Extensions.Logging;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test1Portal
    {
        private Portal? _portal;

        [TestInitialize]
        public void TestInit()
        {
            Openness.Initialize();

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            _portal = new Portal(loggerFactory.CreateLogger<Portal>());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // MSTest creates one instance of this class per test method, so state is never shared
            // between tests: each one must release its own portal. A test that connects and does
            // not dispose leaves TIA Portal running and holding the licence.
            _portal?.Dispose();
            _portal = null;
        }

        [TestMethod]
        public void Test_101_ConnectPortal()
        {
            Assert.IsNotNull(_portal, "TIA-Portal instance is not initialized");

            var result = _portal.ConnectPortal();

            Assert.IsTrue(result, "Failed to connect to TIA-Portal");
            Assert.IsTrue(_portal.IsConnected(), "Portal reports it is not connected after connecting");
        }

        [TestMethod]
        public void Test_102_DisconnectPortal()
        {
            Assert.IsNotNull(_portal, "TIA-Portal instance is not initialized");
            _portal.ConnectPortal();

            var result = _portal.DisconnectPortal();

            Assert.IsTrue(result, "Failed to disconnect from TIA-Portal");
            Assert.IsFalse(_portal.IsConnected(), "Portal still reports a connection after disconnecting");
        }

        [TestMethod]
        public void Test_103_IsConnectedBeforeConnecting()
        {
            Assert.IsNotNull(_portal, "TIA-Portal instance is not initialized");

            var result = _portal.IsConnected();

            Assert.IsFalse(result, "A freshly created Portal must not report a connection");
        }
    }
}
