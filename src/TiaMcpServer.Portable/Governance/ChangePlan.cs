using System;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// One change, decided but not yet executed.
    /// </summary>
    /// <remarks>
    /// **Exactly one action.** There is no plan covering several writes, and that is deliberate:
    /// a confirmation that covers more than what the person read is not a confirmation. In
    /// Workshop Mode this is the unit a human approves.
    ///
    /// Immutable. A plan is a statement about what will happen, and one that can be edited after
    /// being shown for approval is worth nothing.
    /// </remarks>
    public sealed class ChangePlan
    {
        /// <summary>Creates a plan.</summary>
        /// <param name="id">Identifier a person can read and type back.</param>
        /// <param name="request">What is being asked for.</param>
        /// <param name="mode">The mode the session is in.</param>
        /// <param name="expiry">When this plan stops being confirmable, in UTC.</param>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        public ChangePlan(PlanId id, ChangeRequest request, OperationMode mode, DateTimeOffset expiry)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Id = id;
            Mode = mode;
            Expiry = expiry;
            Tool = request.Tool;
            Target = request.Target;
            Value = request.Value;
            Origin = request.Origin;
            BackupPath = request.BackupPath;
            Documentation = request.Documentation;
        }

        /// <summary>Identifier a person can read and type back.</summary>
        public PlanId Id { get; }

        /// <summary>The mode this plan was made in. Confirming it in another is refused.</summary>
        public OperationMode Mode { get; }

        /// <summary>
        /// When this plan stops being confirmable, in UTC.
        /// </summary>
        /// <remarks>
        /// A plan that never expires is a standing permission nobody remembers granting. If the
        /// project has moved on since, the confirmation no longer describes what would happen.
        /// </remarks>
        public DateTimeOffset Expiry { get; }

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

        /// <summary>
        /// What the manufacturer's documentation says about the equipment this change touches.
        /// </summary>
        /// <remarks>
        /// The point of the stage that added this: a plan says **what** will change, and a student
        /// reading *"per the UR5e manual, page 47, configurable I/O can be set as safety-related"*
        /// learns something a plan that only named a block path cannot teach.
        ///
        /// It is evidence, never a condition. A plan whose documentation is
        /// <see cref="HardwareContextOutcome.NotFound"/> or
        /// <see cref="HardwareContextOutcome.Unavailable"/> is confirmable exactly as any other; the
        /// citations inform the person deciding, they do not decide.
        /// </remarks>
        public HardwareContext Documentation { get; }

        /// <summary>Whether this plan can still be confirmed.</summary>
        /// <param name="now">The current moment, in UTC.</param>
        /// <returns>True while the plan is still within its expiry.</returns>
        public bool IsConfirmableAt(DateTimeOffset now)
        {
            return now < Expiry;
        }

        /// <summary>A one-line description, for a person deciding whether to confirm.</summary>
        /// <returns>The description.</returns>
        public override string ToString()
        {
            var value = string.IsNullOrEmpty(Value) ? string.Empty : $" = {Value}";

            return $"[{Id}] {Mode}: {Tool} on '{Target}'{value} | {Documentation.Summarise()}";
        }
    }
}
