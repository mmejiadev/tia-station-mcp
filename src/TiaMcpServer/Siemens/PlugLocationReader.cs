using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the slots around a device item: what is plugged where, and what is free.
    /// </summary>
    /// <remarks>
    /// Openness answers this question in two halves. <c>HardwareObject.GetPlugLocations</c> returns
    /// the positions that will accept something, and says nothing about the positions that will
    /// not; the modules already in the rack are the device items of the same container. Neither
    /// half is an answer on its own — a caller shown only the free slots cannot tell a rack it has
    /// already filled from a rack that refuses everything — so this puts them back together.
    ///
    /// The anchor is a device item rather than a rack, because a rack is not something a path can
    /// usually name. <c>PLC_0</c> resolves to the CPU, and the slots this reads are the CPU's
    /// neighbours: the same container, which is exactly where a module is plugged.
    /// </remarks>
    public sealed class PlugLocationReader
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a slot reader.</summary>
        /// <param name="logger">Optional logger.</param>
        public PlugLocationReader(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Reads every slot of the rack a device item sits in.</summary>
        /// <param name="anchor">A device item in the rack, typically the CPU.</param>
        /// <returns>The slots, free and occupied, in position order.</returns>
        /// <exception cref="PortalException">The device item is in no container.</exception>
        public IReadOnlyList<PlugLocationInfo> Read(DeviceItem anchor)
        {
            if (anchor == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "anchor is required");
            }

            var container = RequireContainer(anchor);

            var slots = Occupied(container).Concat(Free(container))
                .OrderBy(slot => slot.PositionNumber)
                .ToList();

            _logger?.LogInformation(
                "{Count} slot(s) around {Anchor}, {Free} free", slots.Count, anchor.Name, slots.Count(slot => slot.IsFree));

            return slots;
        }

        /// <summary>The hardware object a module would be plugged into.</summary>
        /// <param name="deviceItem">A device item in the rack.</param>
        /// <returns>Its container.</returns>
        /// <exception cref="PortalException">The device item is in no container.</exception>
        /// <remarks>
        /// Shared with the plugger on purpose: the slots a caller is shown must be the slots the
        /// write can reach. Two ways of finding the same rack is how a read and a write come to
        /// disagree, which is the defect that has now been found twice in this repository.
        /// </remarks>
        public static HardwareObject RequireContainer(DeviceItem deviceItem)
        {
            return deviceItem.Container
                ?? throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{deviceItem.Name}' is in no rack, so there is nothing around it to plug into");
        }

        /// <remarks>
        /// <c>Items</c>, not <c>DeviceItems</c>. Measured on 2026-09-13: the container's
        /// <c>DeviceItems</c> of a station came back empty while <c>GetPlugLocations</c> was
        /// reporting slot 1 as taken, so the first version of this reader printed a rack in which
        /// the CPU itself did not appear. <c>Items</c> is the association of what is actually
        /// plugged into the object, which is the question being asked.
        /// </remarks>
        private static IEnumerable<PlugLocationInfo> Occupied(HardwareObject container)
        {
            return container.Items.Select(item => new PlugLocationInfo(
                item.PositionNumber,
                string.Empty,
                item.Name,
                item.TypeIdentifier ?? string.Empty));
        }

        private static IEnumerable<PlugLocationInfo> Free(HardwareObject container)
        {
            return container.GetPlugLocations().Select(
                location => new PlugLocationInfo(location.PositionNumber, location.Label ?? string.Empty));
        }
    }
}
