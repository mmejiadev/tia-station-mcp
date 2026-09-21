using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Unplugs a module from a rack, and says what it would take to put it back.
    /// </summary>
    /// <remarks>
    /// **The only tool here that destroys something**, which is why what it refuses matters more
    /// than what it does.
    ///
    /// What the backup can and cannot restore, stated plainly because the caller is entitled to
    /// know before it deletes: `hardware/modules.txt` records the module's slot, its order number
    /// and the addresses it occupied, which is exactly what <c>PlugModule</c> and
    /// <c>SetModuleAddress</c> need to put an identical card back. Since `SetDeviceParameter`
    /// exists, the backup also holds the module's parameters — `hardware/parameters/` — so a
    /// filter time or a diagnostic setting can be put back with it. That was not true when this
    /// class was written, and the reason it was not is worth keeping: a record of settings nothing
    /// could restore would have been a promise the tool could not keep.
    ///
    /// What still does not come back is anything Openness does not expose as a writable attribute.
    /// The record says which those were — it keeps the read-only ones too — so the loss is visible
    /// rather than silent.
    ///
    /// The same result is returned to the caller, so putting the module back does not require
    /// finding the backup file at all.
    /// </remarks>
    public sealed class ModuleRemover
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a module remover.</summary>
        /// <param name="logger">Optional logger.</param>
        public ModuleRemover(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Unplugs a module.</summary>
        /// <param name="deviceItem">The module to remove.</param>
        /// <param name="modulePath">Its path, for the record returned.</param>
        /// <returns>What was there, in the shape needed to plug it back.</returns>
        /// <exception cref="PortalException">
        /// The item is built into the device, it is the CPU, or TIA Portal refused to remove it.
        /// </exception>
        public ModuleInfo Remove(DeviceItem deviceItem, string modulePath)
        {
            RequireRemovable(deviceItem, modulePath);

            var removed = Describe(deviceItem, modulePath);

            Delete(deviceItem, modulePath);

            _logger?.LogInformation("{Module} unplugged from slot {Slot}", modulePath, removed.PositionNumber);

            return removed;
        }

        /// <remarks>
        /// Two refusals, and neither is TIA being difficult. A built-in item is part of the device
        /// and cannot be unplugged at all; the CPU can, and removing it takes the program with it,
        /// which is a decision about the whole station rather than about a card. Deleting a station
        /// is not what a tool called "unplug a module" should be capable of doing by accident.
        /// </remarks>
        private static void RequireRemovable(DeviceItem deviceItem, string modulePath)
        {
            if (deviceItem.IsBuiltIn)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{modulePath}' is built into the device, not plugged into it, so it cannot be unplugged.");
            }

            if (deviceItem.Classification == DeviceItemClassifications.CPU)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{modulePath}' is the CPU. Removing it would take the program with it, which is a " +
                    "decision about the whole station and not one this tool makes.");
            }
        }

        /// <remarks>
        /// Read before the delete, obviously, but worth stating: after <c>Delete</c> the object is
        /// gone and every one of these properties throws. The record has to be taken while there is
        /// still something to read.
        /// </remarks>
        private static ModuleInfo Describe(DeviceItem deviceItem, string modulePath)
        {
            var spans = string.Join(
                ", ",
                IoAddressReader.Read(deviceItem, modulePath).Select(range => $"{range.IoType} {range.Span}"));

            return new ModuleInfo(
                modulePath,
                deviceItem.PositionNumber,
                deviceItem.TypeIdentifier ?? string.Empty,
                deviceItem.IsBuiltIn,
                spans);
        }

        private static void Delete(DeviceItem deviceItem, string modulePath)
        {
            try
            {
                deviceItem.Delete();
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"TIA Portal refused to unplug '{modulePath}': {failure.Message}",
                    null,
                    failure);
            }
        }
    }
}
