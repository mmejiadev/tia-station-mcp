using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Plugs a module into a slot of the rack a device item sits in.
    /// </summary>
    /// <remarks>
    /// This is where the server stops editing a station somebody else built and starts building
    /// one. Openness offers <c>CanPlugNew</c> beside <c>PlugNew</c>, and asking first is not
    /// politeness: <c>PlugNew</c> reports a refusal as a generic failure, which names neither the
    /// slot nor the reason, and the two mistakes behind almost every refusal — a slot that is
    /// taken and a module the rack does not accept — need different answers.
    ///
    /// Nothing here overwrites. A slot with something in it is reported, never replaced, which is
    /// the same rule the tag and subnet writes follow.
    /// </remarks>
    public sealed class ModulePlugger
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a module plugger.</summary>
        /// <param name="logger">Optional logger.</param>
        public ModulePlugger(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Plugs a module into one slot of the rack a device item sits in.</summary>
        /// <param name="anchor">A device item in the rack, typically the CPU.</param>
        /// <param name="module">What to plug, where.</param>
        /// <returns>The name the module ended up with.</returns>
        /// <exception cref="PortalException">
        /// The slot is taken by something else, the rack does not accept the module there, or TIA
        /// Portal refused.
        /// </exception>
        public string Plug(DeviceItem anchor, ModuleToPlug module)
        {
            if (module == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "module is required");
            }

            var container = PlugLocationReader.RequireContainer(anchor);

            var occupant = OccupantOf(container, module.PositionNumber);
            if (occupant != null)
            {
                return SameModuleAgain(occupant, module);
            }

            RequireTheRackAccepts(container, module);

            var plugged = PlugNew(container, module);

            _logger?.LogInformation(
                "{Module} plugged into slot {Position} of the rack holding {Anchor}",
                plugged.Name, module.PositionNumber, anchor.Name);

            return plugged.Name;
        }

        /// <summary>Moves a module that is already in a rack to another slot of it.</summary>
        /// <param name="module">The module to move.</param>
        /// <param name="modulePath">Its path, for the messages.</param>
        /// <param name="positionNumber">The slot to move it to.</param>
        /// <returns>The name the module carries afterwards.</returns>
        /// <exception cref="PortalException">
        /// The target slot is taken, the rack will not take the module there, or TIA refused.
        /// </exception>
        /// <remarks>
        /// A move keeps the module and its parameters and changes only where it sits, which is what
        /// makes it worth having beside unplug-and-plug-again: the pair of those loses everything
        /// that was set on the card. Its addresses do not follow it — a slot does not decide an
        /// address — so GetIoAddresses is still where the answer is afterwards.
        /// </remarks>
        public string Move(DeviceItem module, string modulePath, int positionNumber)
        {
            var container = PlugLocationReader.RequireContainer(module);

            if (module.PositionNumber == positionNumber)
            {
                return module.Name;
            }

            RequireSlotIsFree(container, positionNumber, modulePath);

            if (!container.CanPlugMove(module, positionNumber))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The rack will not take '{modulePath}' in slot {positionNumber}. Free slots: {FreeSlots(container)}.");
            }

            var moved = container.PlugMove(module, positionNumber);

            _logger?.LogInformation("{Module} moved to slot {Slot}", modulePath, positionNumber);

            return moved.Name;
        }

        /// <summary>Copies a module that is already in a rack into a free slot of it.</summary>
        /// <param name="module">The module to copy.</param>
        /// <param name="modulePath">Its path, for the messages.</param>
        /// <param name="positionNumber">The slot to copy it into.</param>
        /// <returns>The name the copy was given.</returns>
        /// <exception cref="PortalException">
        /// The target slot is taken, the rack will not take the module there, or TIA refused.
        /// </exception>
        /// <remarks>
        /// A copy carries the original's parameters with it, which is the whole point: a cell with
        /// four identical stations is configured once and copied three times. The name is TIA's to
        /// choose — it appends a number — so it is read back rather than asked for.
        /// </remarks>
        public string Copy(DeviceItem module, string modulePath, int positionNumber)
        {
            var container = PlugLocationReader.RequireContainer(module);

            RequireSlotIsFree(container, positionNumber, modulePath);

            if (!container.CanPlugCopy(module, positionNumber))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The rack will not take a copy of '{modulePath}' in slot {positionNumber}. Free slots: {FreeSlots(container)}.");
            }

            var copy = container.PlugCopy(module, positionNumber);

            _logger?.LogInformation("{Module} copied into slot {Slot} as {Copy}", modulePath, positionNumber, copy.Name);

            return copy.Name;
        }

        /// <remarks>
        /// Neither a move nor a copy displaces anything. The target slot is checked here rather
        /// than left to CanPlugMove, because "that slot is taken by X" and "this rack does not
        /// accept that module" are the two mistakes behind almost every refusal and Openness
        /// reports them identically.
        /// </remarks>
        private static void RequireSlotIsFree(HardwareObject container, int positionNumber, string modulePath)
        {
            var occupant = OccupantOf(container, positionNumber);

            if (occupant == null)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"Slot {positionNumber} already holds '{occupant.Name}', so '{modulePath}' is not going there. " +
                "Nothing here displaces a module: unplug it first, or pick a free slot.");
        }

        private static string FreeSlots(HardwareObject container)
        {
            var free = string.Join(", ", FreePositions(container));

            return free.Length == 0 ? "none" : free;
        }

        /// <remarks>
        /// Plugging the same module into the same slot twice is the retry every write here has to
        /// survive, so it reports what is there instead of failing. A slot holding something
        /// *different* is a different situation entirely and is refused with both names, because
        /// the alternative — replacing it — would silently discard a module's parameters.
        /// </remarks>
        private static string SameModuleAgain(DeviceItem occupant, ModuleToPlug module)
        {
            var isTheSame = string.Equals(occupant.TypeIdentifier, module.TypeIdentifier, StringComparison.OrdinalIgnoreCase);

            if (isTheSame)
            {
                return occupant.Name;
            }

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"Slot {module.PositionNumber} already holds '{occupant.Name}' ({occupant.TypeIdentifier}). " +
                "Nothing here replaces a module: unplug it first, or pick a free slot.");
        }

        /// <remarks>
        /// Names the free slots when the rack says no. A caller told only "cannot plug there" has
        /// to guess between a slot that is reserved, a module this rack does not take at all, and
        /// an order number with a typo in it.
        /// </remarks>
        private static void RequireTheRackAccepts(HardwareObject container, ModuleToPlug module)
        {
            if (container.CanPlugNew(module.TypeIdentifier, module.Name, module.PositionNumber))
            {
                return;
            }

            var free = string.Join(", ", FreePositions(container));

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"The rack will not take '{module.TypeIdentifier}' in slot {module.PositionNumber}. " +
                $"Free slots: {(free.Length == 0 ? "none" : free)}. A slot can be free and still " +
                "refuse a module the rack does not accept, and an order number TIA does not know " +
                "is refused the same way.");
        }

        private static DeviceItem PlugNew(HardwareObject container, ModuleToPlug module)
        {
            try
            {
                return container.PlugNew(module.TypeIdentifier, module.Name, module.PositionNumber);
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"TIA Portal refused to plug '{module.TypeIdentifier}' into slot {module.PositionNumber}: {failure.Message}",
                    null,
                    failure);
            }
        }

        private static DeviceItem? OccupantOf(HardwareObject container, int positionNumber)
        {
            // Items, not DeviceItems: see PlugLocationReader.Occupied. The read and the write have
            // to be looking at the same rack, and this is the collection that holds it.
            return container.Items.FirstOrDefault(item => item.PositionNumber == positionNumber);
        }

        private static IEnumerable<int> FreePositions(HardwareObject container)
        {
            return container.GetPlugLocations().Select(location => location.PositionNumber).OrderBy(position => position);
        }
    }
}
