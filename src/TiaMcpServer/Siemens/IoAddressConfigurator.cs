using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Moves a module's address range to another start byte.
    /// </summary>
    /// <remarks>
    /// The half of plugging a card that decides what the program reads. A tag bound to
    /// <c>%I0.0</c> is bound to whichever module starts at byte 0, so a card plugged and left at
    /// whatever address TIA picked is a card the program addresses by luck.
    ///
    /// Only the start moves. The length belongs to the module — a 32-channel card occupies four
    /// bytes and no argument changes that — and offering to set it would invite a range that
    /// describes hardware which does not exist.
    /// </remarks>
    public sealed class IoAddressConfigurator
    {
        private readonly ILogger? _logger;

        /// <summary>Creates an address configurator.</summary>
        /// <param name="logger">Optional logger.</param>
        public IoAddressConfigurator(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Moves one range of a module to a new start byte.</summary>
        /// <param name="deviceItem">The module.</param>
        /// <param name="modulePath">Its path, for reporting the range under.</param>
        /// <param name="ioType">Which range: <c>Input</c> or <c>Output</c>.</param>
        /// <param name="startAddress">The byte the range should start at.</param>
        /// <returns>The range as it stands afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// The module has no such range, it has more than one, or TIA Portal refused the address.
        /// </exception>
        public IoAddressInfo Move(DeviceItem deviceItem, string modulePath, string ioType, int startAddress)
        {
            if (startAddress < 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Address {startAddress} does not exist: an address starts at 0 or later");
            }

            var range = TheOnlyRangeOfType(deviceItem, ioType);

            SetStart(range, deviceItem.Name, startAddress);

            _logger?.LogInformation("{Module} {IoType} now starts at {Start}", deviceItem.Name, ioType, range.StartAddress);

            return new IoAddressInfo(modulePath, range.IoType.ToString(), range.StartAddress, range.Length);
        }

        /// <remarks>
        /// A module with two ranges of the same kind is refused rather than guessed at, and the
        /// refusal prints where they start: picking the first would move an address the caller
        /// never named, and the caller can see from the list which module it meant.
        /// </remarks>
        private static Address TheOnlyRangeOfType(DeviceItem deviceItem, string ioType)
        {
            var wanted = ParseIoType(ioType);

            var matching = IoAddressReader.Own(deviceItem).Where(range => range.IoType == wanted).ToList();

            if (matching.Count == 1)
            {
                return matching[0];
            }

            if (matching.Count == 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{deviceItem.Name}' has no {wanted} range. It has: {DescribeRanges(deviceItem)}");
            }

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"'{deviceItem.Name}' has {matching.Count} {wanted} ranges and this would move the wrong one. " +
                $"It has: {DescribeRanges(deviceItem)}");
        }

        /// <remarks>
        /// Diagnosis and substitute ranges are readable but not something a caller asks to move, so
        /// only the two that are get parsed. An unrecognised value is refused with the two that
        /// work rather than defaulting to either of them.
        /// </remarks>
        private static AddressIoType ParseIoType(string ioType)
        {
            if (string.Equals(ioType, "Input", StringComparison.OrdinalIgnoreCase))
            {
                return AddressIoType.Input;
            }

            if (string.Equals(ioType, "Output", StringComparison.OrdinalIgnoreCase))
            {
                return AddressIoType.Output;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{ioType}' is not a range this moves. Write 'Input' or 'Output'.");
        }

        /// <remarks>
        /// TIA refuses an address that overlaps another module's, which is the mistake this tool
        /// exists to make survivable rather than the one it exists to prevent: the message says
        /// what the range holds now, and GetIoAddresses says what the rest of the rack holds.
        /// </remarks>
        private static void SetStart(Address range, string moduleName, int startAddress)
        {
            var before = range.StartAddress;

            if (before == startAddress)
            {
                return;
            }

            try
            {
                range.StartAddress = startAddress;
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"TIA Portal refused {startAddress} for the {range.IoType} range of '{moduleName}'. " +
                    $"It starts at {before} now. An address that overlaps another module's is refused; " +
                    "call GetIoAddresses to see what the rack already occupies.",
                    null,
                    failure);
            }
        }

        private static string DescribeRanges(DeviceItem deviceItem)
        {
            var ranges = IoAddressReader.Own(deviceItem)
                .Select(range => $"{range.IoType} at {range.StartAddress}")
                .ToList();

            return ranges.Count == 0 ? "no address ranges at all" : string.Join(", ", ranges);
        }
    }
}
