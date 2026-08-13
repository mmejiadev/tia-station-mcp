using Siemens.Engineering.Download;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns the nested <see cref="DownloadResult"/> TIA Portal returns into a flat
    /// <see cref="CompilationReport"/>.
    /// </summary>
    /// <remarks>
    /// A download result has the same shape as a compiler result — a tree where branches carry the
    /// path and leaves carry the description — and a caller needs the same thing from both: what
    /// went wrong and where. Reporting them through one type means an agent has one format to
    /// understand rather than two.
    /// </remarks>
    internal static class DownloadResultReader
    {
        private const string PathSeparator = "/";

        /// <summary>Reads a download result into a report.</summary>
        /// <param name="result">The result TIA Portal returned, or null if it returned nothing.</param>
        /// <returns>The flattened report.</returns>
        internal static CompilationReport Read(DownloadResult? result)
        {
            if (result == null)
            {
                return new CompilationReport(
                    CompilationSeverity.Error,
                    errorCount: 1,
                    warningCount: 0,
                    new[] { new CompilationMessage(CompilationSeverity.Error, string.Empty, "TIA Portal returned no download result") });
            }

            var messages = new List<CompilationMessage>();
            Collect(result.Messages, string.Empty, messages);

            return new CompilationReport(ToSeverity(result.State), result.ErrorCount, result.WarningCount, messages);
        }

        private static void Collect(DownloadResultMessageComposition source, string parentPath, List<CompilationMessage> messages)
        {
            foreach (var message in source)
            {
                var path = Combine(parentPath, message.Message);

                messages.Add(new CompilationMessage(ToSeverity(message.State), parentPath, message.Message));

                Collect(message.Messages, path, messages);
            }
        }

        private static string Combine(string parentPath, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return parentPath;
            }

            if (string.IsNullOrEmpty(parentPath))
            {
                return path;
            }

            return parentPath + PathSeparator + path;
        }

        private static CompilationSeverity ToSeverity(DownloadResultState state)
        {
            switch (state)
            {
                case DownloadResultState.Error:
                    return CompilationSeverity.Error;

                case DownloadResultState.Warning:
                    return CompilationSeverity.Warning;

                case DownloadResultState.Information:
                    return CompilationSeverity.Information;

                default:
                    return CompilationSeverity.Success;
            }
        }
    }
}
