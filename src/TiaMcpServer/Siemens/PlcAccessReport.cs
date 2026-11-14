using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What leaving PLCs without passwords changed: each PLC, and the project's anonymous user where one needed it.
    /// </summary>
    public sealed class PlcAccessReport
    {
        /// <summary>Creates the report.</summary>
        /// <param name="plcs">Each PLC's protection before and after.</param>
        /// <param name="anonymousUser">What became of the project's anonymous user, or empty when it was not touched.</param>
        public PlcAccessReport(IReadOnlyList<PlcAccessChange> plcs, string anonymousUser)
        {
            Plcs = plcs;
            AnonymousUser = anonymousUser;
        }

        /// <summary>Each PLC's protection before and after.</summary>
        public IReadOnlyList<PlcAccessChange> Plcs { get; }

        /// <summary>What became of the project's anonymous user, or empty when it was not touched.</summary>
        public string AnonymousUser { get; }
    }
}
