using Microsoft.Extensions.DependencyInjection;
using System;
using TiaMcpServer.Governance;
using TiaMcpServer.History;

namespace TiaMcpServer
{
    /// <summary>
    /// Registers the journals the web platform imports: compilations and the projects opened.
    /// </summary>
    public static class HistoryRegistration
    {
        /// <summary>Registers the journals and the recorder that writes to them.</summary>
        /// <param name="services">Where to register.</param>
        /// <param name="options">Command line options, for the journal paths.</param>
        /// <remarks>
        /// Singletons, because each journal serialises its appends with a lock of its own, and two
        /// instances over one file would be two locks.
        /// </remarks>
        public static void Register(IServiceCollection services, CliOptions? options)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var compilationsPath = options?.CompilationsPath ?? CliOptions.DefaultCompilationsPath;
            var projectsPath = options?.ProjectsPath ?? CliOptions.DefaultProjectsPath;

            services.AddSingleton<IJournal<CompilationRecord>>(_ => new JsonlJournal<CompilationRecord>(compilationsPath));
            services.AddSingleton<IJournal<ProjectRecord>>(_ => new JsonlJournal<ProjectRecord>(projectsPath));
            services.AddSingleton(provider => new HistoryRecorder(
                provider.GetRequiredService<IJournal<CompilationRecord>>(),
                provider.GetRequiredService<IJournal<ProjectRecord>>(),
                provider.GetRequiredService<ISystemClock>()));
        }
    }
}
