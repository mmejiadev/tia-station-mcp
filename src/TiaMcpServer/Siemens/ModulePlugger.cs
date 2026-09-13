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
