using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Threading.Tasks;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Reports the progress of one long tool call to the client that asked for it.
    /// </summary>
    /// <remarks>
    /// A client asks for progress by sending a token with the call; one that sends none gets no
    /// notifications, and the tool does its work all the same.
    ///
    /// The four bulk tools used to write this out by hand, a dozen lines per notification and three
    /// empty catch blocks between them.
    ///
    /// Progress is advisory, so a notification that cannot be sent never fails the call: it is
    /// logged, because a progress channel that has quietly died is worth knowing about, and the
    /// call goes on. The catch that does this is deliberate and the only one of its kind here. The
    /// tools used to await their notifications bare, so a channel that died after a guarded import
    /// had written its blocks turned that success into an error — inviting a retry of a write that
    /// had already happened.
    /// </remarks>
    public sealed class ProgressReporter
    {
        private readonly Func<ProgressNotificationValue, Task>? _send;
        private readonly ILogger? _logger;

        /// <summary>Creates a reporter that sends through <paramref name="send"/>.</summary>
        /// <param name="send">Sends one notification; null when the caller asked for no progress.</param>
        /// <param name="logger">Where a notification that could not be sent during a failure is recorded.</param>
        public ProgressReporter(Func<ProgressNotificationValue, Task>? send, ILogger? logger)
        {
            _send = send;
            _logger = logger;
        }

        /// <summary>Creates the reporter for one tool call.</summary>
        /// <param name="server">The server the call arrived on.</param>
        /// <param name="context">The call; its progress token decides whether anything is sent.</param>
        /// <param name="logger">Where a notification that could not be sent during a failure is recorded.</param>
        /// <returns>A reporter that sends nothing when the call carries no progress token.</returns>
        /// <remarks>
        /// A null context is a call without a token, not an error: RequestContext cannot be built
        /// without a live server, so a caller with no server to notify — a test — has no context to
        /// pass either, and the tool still does its work.
        /// </remarks>
        public static ProgressReporter For(IMcpServer server, RequestContext<CallToolRequestParams>? context, ILogger? logger)
        {
            var token = context?.Params?.ProgressToken;

            if (token is not ProgressToken progressToken)
            {
                return new ProgressReporter(null, logger);
            }

            return new ProgressReporter(value => server.NotifyProgressAsync(progressToken, value), logger);
        }

        /// <summary>
        /// Sends how far the call has got. A notification that cannot be sent is logged, never
        /// thrown.
        /// </summary>
        /// <param name="progress">Items processed so far.</param>
        /// <param name="total">Items to process in all.</param>
        /// <param name="message">What is happening, in a sentence.</param>
        /// <returns>A task that completes when the notification has been sent or given up on.</returns>
        public async Task ReportAsync(int progress, int total, string message)
        {
            if (_send == null)
            {
                return;
            }

            try
            {
                await _send(new ProgressNotificationValue { Progress = progress, Total = total, Message = message }).ConfigureAwait(false);
            }
            catch (Exception notifyFailure)
            {
                _logger?.LogWarning(notifyFailure, "Could not send a progress notification: {Message}", message);
            }
        }
    }
}
