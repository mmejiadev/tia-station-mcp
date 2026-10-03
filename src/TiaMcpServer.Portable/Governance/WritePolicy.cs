using System;
using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// The whitelist, as configured for this project.
    /// </summary>
    /// <remarks>
    /// Holds one set of rules per mode. A mode with no rules at all denies everything, which is
    /// the correct reading of "nobody has said what this mode may touch" — and is why a missing
    /// policy file is safe rather than convenient.
    /// </remarks>
    public sealed class WritePolicy : IWritePolicy
    {
        private readonly IReadOnlyDictionary<OperationMode, ModeRules> _rules;
        private readonly string? _source;
        private readonly bool _isSourceMissing;

        /// <summary>Creates a policy from per-mode rules.</summary>
        /// <param name="rules">The rules, keyed by the mode they govern.</param>
        /// <param name="source">The policy file they came from; null when built in code.</param>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public WritePolicy(IReadOnlyDictionary<OperationMode, ModeRules> rules, string? source = null)
            : this(rules, source, isSourceMissing: false)
        {
        }

        private WritePolicy(IReadOnlyDictionary<OperationMode, ModeRules> rules, string? source, bool isSourceMissing)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _source = source;
            _isSourceMissing = isSourceMissing;
        }

        /// <summary>A policy that refuses everything.</summary>
        /// <returns>The policy.</returns>
        /// <remarks>
        /// What an absent or unreadable policy file becomes. Refusing every write is loud and
        /// harmless; assuming a permissive default is quiet and not.
        /// </remarks>
        public static WritePolicy DenyEverything()
        {
            return new WritePolicy(new Dictionary<OperationMode, ModeRules>());
        }

        /// <summary>A policy that refuses everything because its file is not there.</summary>
        /// <param name="path">Where the file was looked for.</param>
        /// <returns>The policy; its refusals name the path.</returns>
        public static WritePolicy Missing(string path)
        {
            return new WritePolicy(new Dictionary<OperationMode, ModeRules>(), path, isSourceMissing: true);
        }

        /// <inheritdoc />
        public PolicyDecision Decide(OperationMode mode, string target)
        {
            if (_isSourceMissing)
            {
                return PolicyDecision.Refuse(
                    $"no policy file was found at '{_source}', so nothing is permitted. " +
                    "A person copies .tia-mcp/policy.example.json there, lists what this session may write, and reconnects the server.");
            }

            if (!_rules.TryGetValue(mode, out var rules))
            {
                var where = _source == null ? string.Empty : $" in '{_source}'";
                return PolicyDecision.Refuse(
                    $"no policy is configured for {mode} mode{where}, so nothing is permitted in it");
            }

            return rules.Decide(target);
        }

        /// <summary>Whether this policy says anything at all about a mode.</summary>
        /// <param name="mode">The mode to ask about.</param>
        /// <returns>True when rules exist for it.</returns>
        /// <remarks>
        /// Worth asking before entering Workshop Mode: a session that cannot write anything is
        /// better refused at the door than discovered one refusal at a time, at the machine.
        /// </remarks>
        public bool Governs(OperationMode mode)
        {
            return _rules.ContainsKey(mode);
        }

        /// <summary>Reads a policy from its file.</summary>
        /// <param name="path">Path to <c>policy.json</c>.</param>
        /// <returns>The policy, or one that denies everything when the file is absent.</returns>
        /// <exception cref="PortalException">The file exists but cannot be understood.</exception>
        public static WritePolicy Load(string path)
        {
            return WritePolicyFile.Load(path);
        }
    }
}
