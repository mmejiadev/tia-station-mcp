using System;
using TiaMcpServer.Governance;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The project the guard records with each change: whichever one the portal has open.
    /// </summary>
    /// <remarks>
    /// Takes a way to reach the portal rather than the portal itself, because the guard is built
    /// before anything is connected, and the portal a session ends up using is the one it has when
    /// the change is asked for.
    ///
    /// Reads the path the portal remembered when the project changed, never TIA Portal itself: the
    /// guard asks on every write, including writes that do not hold the Openness gate, and a read
    /// that reached TIA Portal could interleave with a job or fail and turn a refusal into an
    /// operation failure.
    /// </remarks>
    public sealed class PortalProjectContext : IProjectContext
    {
        private readonly Func<Portal?> _portal;

        /// <summary>Creates the context.</summary>
        /// <param name="portal">Returns the portal the session is using, or null before there is one.</param>
        public PortalProjectContext(Func<Portal?> portal)
        {
            _portal = portal ?? throw new ArgumentNullException(nameof(portal));
        }

        /// <inheritdoc />
        public string CurrentProjectPath => _portal()?.OpenProjectPath ?? string.Empty;
    }
}
