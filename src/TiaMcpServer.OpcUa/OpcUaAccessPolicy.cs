using System;
using TiaMcpServer.Governance;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// Decides whether this session may contact an OPC UA server at all.
    /// </summary>
    /// <remarks>
    /// Reading changes nothing on the machine, and it is still contact with a machine. A mistyped
    /// address reaches whatever answers there, which in a workshop is somebody else's cell, so the
    /// endpoint has to be listed in <c>policy.json</c> the way a write target is:
    ///
    /// <code>
    /// "study": { "allow": ["opcua/192.168.0.1:4840"] }
    /// </code>
    ///
    /// It asks the same policy the writes ask, rather than a second list of its own, because a
    /// person deciding what a session may touch should find every decision in one file. The
    /// consequence is intended: deny by default, a mode with no rules refuses, and Workshop rules
    /// may not use wildcards — so a real PLC is always named in full.
    ///
    /// A refusal is a decision, not a failure, and it comes back as one. See CLAUDE.md, "Result for
    /// expected failures".
    /// </remarks>
    public sealed class OpcUaAccessPolicy
    {
        private readonly IModeGate _modeGate;
        private readonly IWritePolicy _policy;

        /// <summary>Creates the check.</summary>
        /// <param name="modeGate">The mode this session is in.</param>
        /// <param name="policy">The policy the session was started with.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public OpcUaAccessPolicy(IModeGate modeGate, IWritePolicy policy)
        {
            _modeGate = modeGate ?? throw new ArgumentNullException(nameof(modeGate));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>Whether the server at this endpoint may be contacted.</summary>
        /// <param name="endpoint">The server.</param>
        /// <returns>The decision and its reason.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
        public PolicyDecision Decide(OpcUaEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            return _policy.Decide(_modeGate.Mode, endpoint.PolicyTarget);
        }
    }
}
