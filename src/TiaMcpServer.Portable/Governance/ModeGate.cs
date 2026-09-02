#if WORKSHOP_MODE
// Only the Workshop build compares the confirmation phrase, so this is the only build that needs
// StringComparison. Guarding it keeps the default build free of a using nobody uses.
using System;
#endif
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// The single source of truth for what this session may act on.
    /// </summary>
    /// <remarks>
    /// Layers 0 and 1 of the defence described in <c>docs/ROADMAP.md</c>.
    ///
    /// **Layer 0 — compile time.** Workshop Mode is compiled out unless the build defines
    /// <c>WORKSHOP_MODE</c>, which the default build does not. The binary used day to day does not
    /// contain the capability at all, so no configuration mistake can reach a machine with it.
    /// The precedent is <c>PLCSIM_AVAILABLE</c> in the csproj, which already gates a capability
    /// this way and degrades cleanly when it is absent.
    ///
    /// **Layer 1 — startup.** Even the build that carries the capability starts in Study Mode.
    /// Reaching Workshop Mode needs an explicit call *and* a confirmation phrase typed by a person
    /// in this session. Nothing about it is persisted, so it cannot be left switched on from
    /// yesterday — the exact failure mode an environment variable would have.
    ///
    /// A gate is immutable: the mode is fixed when it is created and there is no way to change it.
    /// A mode that can change under a running operation is a mode nobody can reason about.
    /// </remarks>
    public sealed class ModeGate : IModeGate
    {
        /// <summary>
        /// What a person has to type to reach Workshop Mode.
        /// </summary>
        /// <remarks>
        /// Long and specific on purpose. A short token gets muscle-memoried and typed without
        /// reading; a sentence naming the emergency stop is one you cannot enter absent-mindedly.
        /// It is a speed bump for a human, not a secret — it is in the source, and its job is to
        /// make the step deliberate rather than to keep anyone out.
        /// </remarks>
        public const string WorkshopConfirmationPhrase =
            "I am at the machine and the emergency stop is within reach";

#if !WORKSHOP_MODE
        private const string WorkshopUnavailable =
            "Workshop Mode is not present in this build. It is compiled out unless WORKSHOP_MODE is " +
            "defined, which is deliberate: the binary used for everyday work cannot command physical " +
            "hardware at all. See docs/ROADMAP.md for the conditions under which that build is made.";
#endif

        private ModeGate(OperationMode mode)
        {
            Mode = mode;
        }

        /// <summary>What this session may act on.</summary>
        public OperationMode Mode { get; }

        /// <summary>Who confirms a planned change in this session.</summary>
        public Confirmation RequiredConfirmation => ConfirmationFor(Mode);

        /// <summary>True when this session can only reach simulated controllers.</summary>
        public bool IsSimulationOnly => Mode == OperationMode.Study;

        /// <summary>Opens a session against PLCSIM Advanced. This is the default and needs nothing.</summary>
        /// <returns>A gate in <see cref="OperationMode.Study"/>.</returns>
        public static ModeGate ForStudy()
        {
            return new ModeGate(OperationMode.Study);
        }

        /// <summary>
        /// Opens a session against physical hardware. Refused unless this build carries the
        /// capability and a person types the confirmation phrase.
        /// </summary>
        /// <param name="confirmationPhrase">Must equal <see cref="WorkshopConfirmationPhrase"/> exactly.</param>
        /// <returns>A gate in <see cref="OperationMode.Workshop"/>.</returns>
        /// <exception cref="PortalException">
        /// Always, in the default build. In a <c>WORKSHOP_MODE</c> build, when the phrase does not
        /// match.
        /// </exception>
        public static ModeGate ForWorkshop(string confirmationPhrase)
        {
#if WORKSHOP_MODE
            if (!string.Equals(confirmationPhrase, WorkshopConfirmationPhrase, StringComparison.Ordinal))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "Workshop Mode refused: the confirmation phrase does not match. It must be typed exactly.");
            }

            return new ModeGate(OperationMode.Workshop);
#else
            // The parameter is deliberately unused here: in this build there is nothing it could
            // unlock, and pretending to check it would suggest otherwise.
            _ = confirmationPhrase;

            throw new PortalException(PortalErrorCode.InvalidState, WorkshopUnavailable);
#endif
        }

        /// <summary>Who confirms a planned change in a given mode.</summary>
        /// <param name="mode">The mode to decide for.</param>
        /// <returns>Automatic in Study, Manual in Workshop.</returns>
        /// <remarks>
        /// Exhaustive on purpose, with no silent default. A mode this method does not recognise is
        /// refused rather than treated as Study: the absence of a decision is a refusal, never a
        /// permission. Adding a member to <see cref="OperationMode"/> without deciding how it
        /// confirms will throw here rather than quietly inherit the most permissive behaviour.
        /// </remarks>
        /// <exception cref="PortalException">The mode is not one this method decides for.</exception>
        public static Confirmation ConfirmationFor(OperationMode mode)
        {
            switch (mode)
            {
                case OperationMode.Study:
                    return Confirmation.Automatic;

                case OperationMode.Workshop:
                    return Confirmation.Manual;

                default:
                    throw new PortalException(
                        PortalErrorCode.InvalidState,
                        $"Unrecognised operation mode: {mode}. Refusing rather than assuming the permissive one.");
            }
        }
    }
}
