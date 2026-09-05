using System.Collections.Generic;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Where the previous state goes before a write overwrites it.
    /// </summary>
    /// <remarks>
    /// The caller does not choose the location and cannot ask for the backup to be skipped. That is
    /// the whole point: a backup the caller picks a directory for is a backup nobody can find
    /// afterwards, and one the caller passes as a parameter is one an agent can point at a temp
    /// folder that gets reaped. One configured root, one timestamped directory per change, and a
    /// list of everything that was ever saved.
    /// </remarks>
    public interface IBackupRegistry
    {
        /// <summary>Reserves a directory for one change's previous state.</summary>
        /// <param name="tool">The tool about to write, for example <c>WriteScl</c>.</param>
        /// <param name="target">What it is about to write to.</param>
        /// <returns>The directory to export the previous state into. It exists on return.</returns>
        public string Allocate(string tool, string target);

        /// <summary>Everything the registry holds, newest first.</summary>
        /// <returns>One record per backup taken.</returns>
        public IReadOnlyList<BackupRecord> List();
    }
}
