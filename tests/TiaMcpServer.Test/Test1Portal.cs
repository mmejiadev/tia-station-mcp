using Microsoft.Extensions.Logging;
using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// The one class that still creates its own <see cref="Portal"/>: it tests the connect and
    /// disconnect lifecycle, so it cannot borrow the shared one. Because a portal is already
    /// running by then — the assembly owns it — these connections attach rather than start a new
    /// process, and releasing them only detaches. That is what makes this class safe to run
    /// alongside the others.
    ///
    /// MSTest creates one instance per test method and disposes it afterwards, which is what keeps
    /// the portal and the logger factory from leaking.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test1Portal : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private Portal? _portal;

        public Test1Portal()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
        }

        [TestInitialize]
        public void TestInit()
        {
            _portal = new Portal(_loggerFactory.CreateLogger<Portal>());
        }

        public void Dispose()
        {
            _portal?.Dispose();
            _portal = null;

            _loggerFactory.Dispose();
        }

        [TestMethod]
        public void ConnectPortal_NotConnected_Connects()
        {
            Assert.IsNotNull(_portal);

            var result = _portal.ConnectPortal();

            Assert.IsTrue(result, "Failed to connect to TIA Portal");
            Assert.IsTrue(_portal.IsConnected(), "Portal reports it is not connected after connecting");
        }

        [TestMethod]
        public void DisconnectPortal_AfterConnecting_Disconnects()
        {
            Assert.IsNotNull(_portal);
            _portal.ConnectPortal();

            var result = _portal.DisconnectPortal();

            Assert.IsTrue(result, "Failed to disconnect from TIA Portal");
            Assert.IsFalse(_portal.IsConnected(), "Portal still reports a connection after disconnecting");
        }

        [TestMethod]
        public void IsConnected_BeforeConnecting_ReturnsFalse()
        {
            Assert.IsNotNull(_portal);

            var result = _portal.IsConnected();

            Assert.IsFalse(result, "A freshly created Portal must not report a connection");
        }
    }
}
