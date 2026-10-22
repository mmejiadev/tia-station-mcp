using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// PC stations carrying an HMI runtime: WinCC Unified PC RT and WinCC RT Advanced. See
    /// <see cref="PcHmiRuntime"/> for what was measured and why only those two are accepted.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Adds a PC station with an HMI runtime plugged into it.</summary>
        /// <param name="stationName">Name for the station, for example <c>PC-System_1</c>.</param>
        /// <param name="runtimeTypeIdentifier">The runtime, exactly as <see cref="FindCatalogEntries"/> gives it.</param>
        /// <returns>The station's name.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the name is taken, the runtime is not a measured HMI runtime or not in
        /// this installation's catalogue, no slot accepts it, or TIA Portal failed.
        /// </exception>
        /// <remarks>
        /// Everything that can be checked is checked before the station exists — the runtime against
        /// the catalogue, the slot with <c>CanPlugNew</c> — because a failed plug is the step that once
        /// took TIA Portal down. The runtime's own name is chosen by TIA Portal (<c>HMI_RT_1</c>) and
        /// cannot be changed; the software path to use afterwards is read with
        /// <see cref="GetSoftwarePaths"/>.
        /// </remarks>
        public string AddPcStation(string stationName, string runtimeTypeIdentifier)
        {
            _logger?.LogInformation("Adding PC station {StationName} with {Runtime}...", stationName, runtimeTypeIdentifier);

            try
            {
                RequireNewStation(stationName, runtimeTypeIdentifier);

                var runtime = RequireCatalogued(runtimeTypeIdentifier);
                var device = CurrentProject!.Devices.CreateWithItem(PcHmiRuntime.PcStationTypeIdentifier, stationName, stationName);

                PlugRuntimeOrRemoveStation(device, runtime);

                return device.Name;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Adding the PC station failed: {ex.Message}", null, ex);

                pex.Data["stationName"] = stationName;
                pex.Data["runtimeTypeIdentifier"] = runtimeTypeIdentifier;

                _logger?.LogError(pex, "AddPcStation failed for {StationName} ({Runtime})", stationName, runtimeTypeIdentifier);
                throw pex;
            }
        }

        private void RequireNewStation(string stationName, string runtimeTypeIdentifier)
        {
            if (string.IsNullOrWhiteSpace(stationName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "stationName is required");
            }

            PcHmiRuntime.RequireMeasured(runtimeTypeIdentifier);

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open or create a project first");
            }

            if (IsDeviceNameTaken(stationName))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"A device or device item named '{stationName}' already exists; nothing was added");
            }
        }

        /// <returns>The identifier as the catalogue spells it, which is what PlugNew is given.</returns>
        private string RequireCatalogued(string runtimeTypeIdentifier)
        {
            var identifier = runtimeTypeIdentifier.Trim();
            var entry = _portal!.HardwareCatalog.Find(identifier)
                .FirstOrDefault(candidate => string.Equals(candidate.TypeIdentifier, identifier, StringComparison.OrdinalIgnoreCase));

            return entry?.TypeIdentifier ?? throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{runtimeTypeIdentifier}' is not in this TIA Portal's hardware catalogue; copy a typeIdentifier from FindCatalogEntries");
        }

        private static void PlugRuntimeOrRemoveStation(Device device, string runtime)
        {
            try
            {
                var rack = device.DeviceItems.First(item => item.TypeIdentifier == PcHmiRuntime.PcStationTypeIdentifier);

                // An empty name, and neither a real one nor null (measured 2026-10-20): a name fails
                // with "The name could not be set", null with "The argument 'name' may not be null".
                // TIA Portal names the runtime itself.
                var slot = PlugLocationReader.ReadPlugLocations(rack)
                    .OrderBy(location => location.PositionNumber)
                    .FirstOrDefault(location => rack.CanPlugNew(runtime, string.Empty, location.PositionNumber))
                    ?? throw new PortalException(PortalErrorCode.InvalidState, $"No slot of the PC station's rack accepts '{runtime}'");

                rack.PlugNew(runtime, string.Empty, slot.PositionNumber);
            }
            catch (Exception plugFailure)
            {
                throw RemoveStationAfter(device, plugFailure);
            }
        }

        /// <remarks>
        /// Either way the caller learns what is left in the project: nothing, so the same call can be
        /// tried again once the cause is fixed, or an empty station to delete first. A delete that fails
        /// as well must not replace the plug failure, which is the one that explains what went wrong.
        /// </remarks>
        private static PortalException RemoveStationAfter(Device device, Exception plugFailure)
        {
            var stationName = device.Name;

            try
            {
                device.Delete();

                return new PortalException(
                    (plugFailure as PortalException)?.Code ?? PortalErrorCode.WriteFailed,
                    $"Plugging the runtime failed ({plugFailure.Message}). The empty station '{stationName}' was removed, "
                    + "so nothing was added.",
                    null,
                    plugFailure);
            }
            catch (Exception deleteFailure)
            {
                return new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"Plugging the runtime failed ({plugFailure.Message}), and removing the empty station '{stationName}' failed too "
                    + $"({deleteFailure.Message}). Delete '{stationName}' in TIA Portal before trying again.",
                    null,
                    new AggregateException(plugFailure, deleteFailure));
            }
        }
    }
}
