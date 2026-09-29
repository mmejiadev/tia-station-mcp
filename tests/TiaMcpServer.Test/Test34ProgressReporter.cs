using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Progress notifications of the bulk tools.
    /// </summary>
    /// <remarks>
    /// Needs no TIA Portal, and lives here only because the reporter speaks the MCP SDK, which the
    /// portable assembly does not reference.
    /// </remarks>
    [TestClass]
    public sealed class Test34ProgressReporter
    {
        [TestMethod]
        public async Task ReportAsync_CallWithoutProgressToken_SendsNothing()
        {
            // A null server would throw on the first notification, so completing is the assertion.
            var progress = ProgressReporter.For(null!, null, null);

            await progress.ReportAsync(1, 2, "Halfway");
        }

        /// <remarks>
        /// Progress is advisory. A notification that could not be sent used to fail the call, even
        /// after a guarded import had written its blocks; it must be logged and never thrown.
        /// </remarks>
        [TestMethod]
        public async Task ReportAsync_NotificationCannotBeSent_IsLoggedNotThrown()
        {
            var logger = new RecordingLogger();
            var progress = new ProgressReporter(_ => throw new InvalidOperationException("Channel closed"), logger);

            await progress.ReportAsync(3, 3, "Import completed");

            Assert.AreEqual(1, logger.Warnings.Count, "The notification failure was not logged");
            Assert.IsInstanceOfType(logger.Warnings[0], typeof(InvalidOperationException));
        }

        private sealed class RecordingLogger : ILogger
        {
            public List<Exception?> Warnings { get; } = new List<Exception?>();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    Warnings.Add(exception);
                }
            }
        }
    }
}
