using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Vci
{
    /// <summary>
    /// Which mapped objects a synchronisation copies, and when it must copy none.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-13 on TIA Portal V21: a file edited in the workspace reads <c>Unequal</c> /
    /// <c>WorkspaceFileChanged</c>, a block edited in the project <c>Unequal</c> / <c>ProjectObjectChanged</c>, and
    /// synchronising an <c>Equal</c> one is refused by Openness. Synchronising copies one side over the other whatever
    /// changed, so an object changed on the side about to be overwritten would lose that change: that is a conflict,
    /// and with any conflict nothing is copied — the person decides, object by object, which change wins. A state
    /// not listed here is not guessed at either: it refuses.
    /// </remarks>
    public static class VciSyncRule
    {
        private const string Equal = "Equal";
        private const string Unequal = "Unequal";
        private const string ProjectChanged = "ProjectObjectChanged";
        private const string WorkspaceChanged = "WorkspaceFileChanged";

        /// <summary>The objects to copy.</summary>
        /// <param name="states">Every mapped object of the workspace.</param>
        /// <param name="direction">Which way to copy.</param>
        /// <returns>The files of the objects to synchronise, in the order given; empty when all are equal.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming every object that would lose a change or is in a state not decided here.</exception>
        public static IReadOnlyList<string> Plan(IReadOnlyList<VciObjectState> states, VciDirection direction)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            var changedSource = direction switch
            {
                VciDirection.ProjectToWorkspace => ProjectChanged,
                VciDirection.WorkspaceToProject => WorkspaceChanged,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised synchronisation direction: {direction}")
            };

            var blocking = states.Where(state => state.CompareState != Equal && !IsCopiable(state, changedSource)).ToList();

            if (blocking.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Nothing was synchronised. " + string.Join("; ", blocking.Select(state => Explain(state, direction))));
            }

            return states.Where(state => state.CompareState == Unequal).Select(state => state.File).ToList();
        }

        private static bool IsCopiable(VciObjectState state, string changedSource)
        {
            return state.CompareState == Unequal && state.ChangedSide == changedSource;
        }

        private static string Explain(VciObjectState state, VciDirection direction)
        {
            if (state.CompareState == Unequal && (state.ChangedSide == ProjectChanged || state.ChangedSide == WorkspaceChanged))
            {
                var changedWhere = state.ChangedSide == ProjectChanged ? "the project" : "the workspace";

                return $"'{state.File}' changed in {changedWhere}, and {direction} would overwrite that change; synchronise it the other way, or undo one of the two";
            }

            return $"'{state.File}' is {state.CompareState} / {state.ChangedSide}, a state this server does not synchronise; resolve it in TIA Portal's version control view";
        }
    }
}
