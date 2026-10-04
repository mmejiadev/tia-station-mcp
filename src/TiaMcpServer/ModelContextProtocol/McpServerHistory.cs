using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TiaMcpServer.History;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The history the web platform imports: compilations and the projects opened. See
    /// <see cref="HistoryRecorder"/> for why a failure to record never fails the tool.
    /// </remarks>
    public static partial class McpServer
    {
        /// <summary>Where this session records its compilations and the projects it opens.</summary>
        public static HistoryRecorder History =>
            _services != null
                ? _services.GetRequiredService<HistoryRecorder>()
                : HistoryFallback.Recorder;

        /// <summary>Records the project now open, and says so when it could not.</summary>
        /// <param name="projectEvent">How it came to be open.</param>
        /// <param name="message">What the tool would answer anyway.</param>
        /// <returns>The message, with a sentence added when the project was not recorded.</returns>
        private static string RecordOpenProject(ProjectEvent projectEvent, string message)
        {
            var project = Portal.GetProjectSummary();

            if (project == null)
            {
                return message;
            }

            return WithHistoryNote(message, History.RecordProject(projectEvent, project));
        }

        /// <summary>Records a compilation, and says so when it could not.</summary>
        /// <param name="softwarePath">The PLC software compiled.</param>
        /// <param name="report">What the compiler said.</param>
        /// <param name="message">What the tool would answer anyway.</param>
        /// <returns>The message, with a sentence added when the compilation was not recorded.</returns>
        private static string RecordCompilation(string softwarePath, Siemens.CompilationReport report, string message)
        {
            var projectPath = Portal.GetProjectSummary()?.Path ?? string.Empty;

            return WithHistoryNote(message, History.RecordCompilation(projectPath, softwarePath, report));
        }

        private static string WithHistoryNote(string message, string note)
        {
            if (note.Length == 0)
            {
                return message;
            }

            Logger?.LogError("{Note}", note);

            return $"{message}. {note}";
        }

        /// <summary>The history a host that registered none still records to: the default paths.</summary>
        private static class HistoryFallback
        {
            internal static readonly HistoryRecorder Recorder = new HistoryRecorder(
                new JsonlJournal<CompilationRecord>(CliOptions.DefaultCompilationsPath),
                new JsonlJournal<ProjectRecord>(CliOptions.DefaultProjectsPath),
                new Governance.SystemClock());
        }
    }
}
