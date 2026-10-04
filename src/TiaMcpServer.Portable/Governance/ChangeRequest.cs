using System;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// What a caller is asking to change, before anything has been decided about it.
    /// </summary>
    /// <remarks>
    /// A parameter object rather than five loose strings threaded through the layer. Besides
    /// reading better, it removes the classic defect of passing two of them in the wrong order —
    /// which here would mean auditing the wrong target.
    /// </remarks>
    public sealed class ChangeRequest
    {
        /// <summary>What a request that never reached a hardware lookup reports.</summary>
        public const string NotLookedUp = "this change was not put through a documentation lookup";

        /// <summary>Describes a requested change.</summary>
        /// <param name="tool">The tool asking, for example <c>WriteScl</c>.</param>
        /// <param name="target">What it would touch, as a full path.</param>
        /// <param name="value">What it would write, summarised for the log.</param>
        /// <param name="origin">Who or what asked: a user, an agent, a command.</param>
        /// <exception cref="ArgumentException"><paramref name="tool"/> or <paramref name="target"/> is empty.</exception>
        public ChangeRequest(string tool, string target, string value = "", string origin = "agent")
        {
            if (string.IsNullOrWhiteSpace(tool))
            {
                throw new ArgumentException("A change must name the tool asking for it", nameof(tool));
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("A change must name what it would touch", nameof(target));
            }

            Tool = tool;
            Target = target;
            Value = value ?? string.Empty;
            Origin = origin ?? string.Empty;
            BackupPath = string.Empty;
            Documentation = HardwareContext.Unavailable(NotLookedUp);
            Project = string.Empty;
        }

        private ChangeRequest(ChangeRequest request, string backupPath, HardwareContext documentation, string project)
        {
            Tool = request.Tool;
            Target = request.Target;
            Value = request.Value;
            Origin = request.Origin;
            BackupPath = backupPath ?? string.Empty;
            Documentation = documentation;
            Project = project ?? string.Empty;
        }

        /// <summary>The tool asking.</summary>
        public string Tool { get; }

        /// <summary>What it would touch, as a full path.</summary>
        public string Target { get; }

        /// <summary>What it would write, summarised.</summary>
        public string Value { get; }

        /// <summary>Who or what asked.</summary>
        public string Origin { get; }

        /// <summary>Where the previous state was saved, or empty when nothing was overwritten.</summary>
        public string BackupPath { get; }

        /// <summary>What the documentation says about the equipment this change touches.</summary>
        /// <remarks>
        /// Never null. A request nobody looked anything up for carries an
        /// <see cref="HardwareContextOutcome.Unavailable"/> saying exactly that, so a plan built
        /// outside the guard is visibly uncited rather than quietly blank.
        /// </remarks>
        public HardwareContext Documentation { get; }

        /// <summary>The TIA Portal project open when the change was asked for, or empty when none was.</summary>
        /// <remarks>
        /// The target names a path inside a project — <c>PLC_1/Blocks/FC_Motor</c> — and not the
        /// project, so without this an audit line could not say which project it changed. Opening a
        /// project is a read and is not audited, so it cannot be inferred from the trail either.
        /// </remarks>
        public string Project { get; }

        /// <summary>The same request, naming where the previous state was saved.</summary>
        /// <param name="backupPath">The directory or file the previous state went to.</param>
        /// <returns>A copy carrying the backup path.</returns>
        /// <remarks>
        /// A copy rather than a setter, because a request that can be edited after a decision was
        /// taken about it describes something other than what was decided. It is also why this is
        /// not a fifth constructor parameter: only the tools that take a backup say anything about
        /// one, and the rest should not have to pass an empty string to say so.
        /// </remarks>
        public ChangeRequest WithBackup(string backupPath)
        {
            return new ChangeRequest(this, backupPath, Documentation, Project);
        }

        /// <summary>The same request, carrying what the documentation says about it.</summary>
        /// <param name="documentation">Excerpts, an honest not-found, or an unavailable with its reason.</param>
        /// <returns>A copy carrying the documentation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="documentation"/> is null.</exception>
        /// <remarks>
        /// A copy, for the same reason as <see cref="WithBackup"/>: the citations shown beside a
        /// decision have to be the ones the decision was taken with. Attaching them to the request
        /// rather than passing them to the plan follows the backup path, which is likewise something
        /// the system discovers rather than something the caller asked for.
        /// </remarks>
        public ChangeRequest WithDocumentation(HardwareContext documentation)
        {
            if (documentation == null)
            {
                throw new ArgumentNullException(nameof(documentation));
            }

            return new ChangeRequest(this, BackupPath, documentation, Project);
        }

        /// <summary>The same request, naming the project open when it was asked for.</summary>
        /// <param name="project">The project file's path, or empty when no project is open.</param>
        /// <returns>A copy carrying the project.</returns>
        /// <remarks>
        /// Attached by the guard rather than by each tool, like the documentation: every write
        /// passes through the guard, and a field set at forty call sites is a field some of them
        /// forget.
        /// </remarks>
        public ChangeRequest WithProject(string project)
        {
            return new ChangeRequest(this, BackupPath, Documentation, project);
        }
    }
}
