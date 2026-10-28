using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Chooses which running TIA Portal the server attaches to.</summary>
    /// <remarks>
    /// The server used to attach to whichever process Openness listed first. On 2026-10-15 that was
    /// a leftover instance with no window, while the user's window held the project: the server
    /// worked on nothing she could see, and opening her project then failed because it was locked.
    ///
    /// The order below puts the person first. The process holding the requested project wins,
    /// because it is the only one that can open it. Then a window, because that is where somebody
    /// is working and where a change shows up. A headless process is a leftover of an earlier
    /// session, and is taken only when nothing else runs, preferring one that holds a project.
    /// </remarks>
    public static class PortalProcessSelector
    {
        /// <summary>Chooses the process to attach to.</summary>
        /// <param name="processes">Every running TIA Portal, in the order Openness lists them.</param>
        /// <param name="requestedProjectPath">The full path of the project wanted, or empty.</param>
        /// <returns>The process and the reason; no process when none runs.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="processes"/> is null.</exception>
        public static PortalProcessChoice Choose(IReadOnlyList<PortalProcessInfo> processes, string requestedProjectPath)
        {
            if (processes == null)
            {
                throw new ArgumentNullException(nameof(processes));
            }

            var holder = processes.FirstOrDefault(process => ProjectFilePath.AreSame(process.ProjectPath, requestedProjectPath));

            if (holder != null)
            {
                return new PortalProcessChoice(holder, PortalProcessChoiceReason.HoldsRequestedProject);
            }

            var withWindow = FirstPreferringAProject(processes.Where(process => process.HasUserInterface));

            if (withWindow != null)
            {
                return new PortalProcessChoice(withWindow, PortalProcessChoiceReason.HasUserInterface);
            }

            var headless = FirstPreferringAProject(processes);

            if (headless != null)
            {
                return new PortalProcessChoice(headless, PortalProcessChoiceReason.WithoutUserInterface);
            }

            return new PortalProcessChoice(null, PortalProcessChoiceReason.NoneRunning);
        }

        private static PortalProcessInfo? FirstPreferringAProject(IEnumerable<PortalProcessInfo> processes)
        {
            var candidates = processes.ToList();

            return candidates.FirstOrDefault(process => process.HasProject) ?? candidates.FirstOrDefault();
        }
    }
}
