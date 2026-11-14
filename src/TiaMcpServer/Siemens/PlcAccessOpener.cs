using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Leaves a PLC the way the class uses it: confidential configuration data not protected, and full access
    /// without a password.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-11 on TIA Portal V21: a new CPU 1512C-1 PN V2.9 holds <c>WithoutPassword</c> for its
    /// confidential data and <c>NoAccess</c> as its access level, and its hardware does not compile — three
    /// errors asking for passwords. <c>Unprotect()</c> turns the first into <c>None</c> and two errors go; setting
    /// <c>FullAccess</c> takes the third, leaving two warnings ("does not contain a configured protection level",
    /// "the CPU display does not contain any password protection"). The same done on a class project's PLC_2,
    /// which then compiled and downloaded to PLCSIM Advanced.
    ///
    /// Only the states a new PLC is created in, or that are already open, are opened; anything else is refused.
    /// A password on the confidential data needs that password to remove, and a PLC protected on purpose is not this
    /// server's to open. <c>ReadAccess</c> and <c>HMIAccess</c> are levels somebody chose, as TIA Portal creates a PLC
    /// at <c>NoAccess</c>, so they are refused too. What cannot be told apart is <c>NoAccess</c> as created from
    /// <c>NoAccess</c> behind a password set on purpose: Openness sets and resets access-level passwords
    /// (<c>PlcAccessLevelProvider</c>) and does not report them. That is why the tool says it is not for a PLC running
    /// a machine, and why AddDevice does this in Study Mode only.
    ///
    /// Measured 2026-11-12 on V21: an S7-1200 V4.7 offers the confidential-data service and no access-level one — its
    /// access goes through users and roles — so either service makes a PLC, and each setting is changed where it exists.
    /// </remarks>
    internal static class PlcAccessOpener
    {
        /// <summary>The access level reported for a PLC that has none, its access going through users and roles.</summary>
        internal const string NoAccessLevel = "UsersAndRoles";

        /// <summary>The confidential-data protection reported for a PLC that has no such setting.</summary>
        internal const string NoConfidentialData = "NotOffered";

        /// <summary>Whether a device item is a PLC with a protection setting this changes.</summary>
        /// <param name="item">The item.</param>
        /// <returns>True when it offers either service.</returns>
        internal static bool IsPlc(DeviceItem item)
        {
            return item.GetService<PlcMasterSecretConfigurator>() != null || item.GetService<PlcAccessLevelProvider>() != null;
        }

        /// <summary>Whether a PLC's access goes through users and roles, having no access level of its own.</summary>
        /// <param name="plc">A device item for which <see cref="IsPlc"/> holds.</param>
        /// <returns>True when it offers no access-level service.</returns>
        internal static bool GoesThroughUsers(DeviceItem plc)
        {
            return plc.GetService<PlcAccessLevelProvider>() == null;
        }

        /// <summary>Reads a PLC's protection.</summary>
        /// <param name="plc">A device item for which <see cref="IsPlc"/> holds.</param>
        /// <returns>What it is set to.</returns>
        internal static PlcAccessState Read(DeviceItem plc)
        {
            return new PlcAccessState(
                plc.GetService<PlcMasterSecretConfigurator>()?.MasterSecretConfiguration.ToString() ?? NoConfidentialData,
                plc.GetService<PlcAccessLevelProvider>()?.PlcProtectionAccessLevel.ToString() ?? NoAccessLevel);
        }

        /// <summary>Leaves a PLC at full access with its confidential data not protected.</summary>
        /// <param name="plc">A device item for which <see cref="IsPlc"/> holds.</param>
        /// <returns>Its protection before and after.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when its protection is not one this opens.</exception>
        internal static PlcAccessChange Open(DeviceItem plc)
        {
            RequireOpenable(plc);

            var before = Read(plc);
            var secret = plc.GetService<PlcMasterSecretConfigurator>();
            var levels = plc.GetService<PlcAccessLevelProvider>();

            if (secret != null && secret.MasterSecretConfiguration == MasterSecretConfiguration.WithoutPassword)
            {
                secret.Unprotect();
            }

            if (levels != null && levels.PlcProtectionAccessLevel == PlcProtectionAccessLevel.NoAccess)
            {
                levels.PlcProtectionAccessLevel = PlcProtectionAccessLevel.FullAccess;
            }

            return new PlcAccessChange(plc.Name, before, Read(plc));
        }

        /// <summary>Refuses a PLC whose protection is not one this opens.</summary>
        /// <param name="plc">A device item for which <see cref="IsPlc"/> holds.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when it is refused.</exception>
        /// <remarks>Both switches end in a refusal: a value not listed here is one nobody decided to open.</remarks>
        internal static void RequireOpenable(DeviceItem plc)
        {
            var secret = plc.GetService<PlcMasterSecretConfigurator>();
            var levels = plc.GetService<PlcAccessLevelProvider>();

            if (secret != null)
            {
                RequireOpenableConfidentialData(plc.Name, secret.MasterSecretConfiguration);
            }

            if (levels != null)
            {
                RequireOpenableAccessLevel(plc.Name, levels.PlcProtectionAccessLevel);
            }
        }

        private static void RequireOpenableConfidentialData(string plcName, MasterSecretConfiguration confidentialData)
        {
            var isOpenable = confidentialData switch
            {
                MasterSecretConfiguration.None => true,
                MasterSecretConfiguration.WithoutPassword => true,
                _ => false
            };

            if (!isOpenable)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"PLC '{plcName}' protects its confidential configuration data as {confidentialData}. Removing a password needs "
                    + "that password: do it in TIA Portal (Properties, Protection & Security). Nothing was changed.");
            }
        }

        private static void RequireOpenableAccessLevel(string plcName, PlcProtectionAccessLevel accessLevel)
        {
            var isOpenable = accessLevel switch
            {
                PlcProtectionAccessLevel.FullAccessIncludingFailsafe => true,
                PlcProtectionAccessLevel.FullAccess => true,
                PlcProtectionAccessLevel.NoAccess => true,
                _ => false
            };

            if (!isOpenable)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"PLC '{plcName}' has access level {accessLevel}, which somebody chose: TIA Portal creates a PLC at NoAccess. "
                    + "Change it in TIA Portal (Properties, Protection & Security) if it is meant to be open. Nothing was changed.");
            }
        }
    }
}
