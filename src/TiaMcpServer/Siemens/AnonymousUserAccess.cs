using Siemens.Engineering;
using Siemens.Engineering.Umac;
using System.IO;
using System.Linq;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Gives the project's anonymous user full access to its PLCs, which a PLC whose access goes through users and
    /// roles needs to compile once its confidential data is not protected.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-13 on TIA Portal V21, on an S7-1200 V4.7: with its confidential data unprotected the hardware
    /// still had one error, "At least one user must have the following runtime right: Full access"; activating the
    /// anonymous user (<c>UmacConfigurator.ActivateAnonymousUser</c>) and giving it the system role identified as
    /// <c>PLCAdministrator</c> — HMI, read and full access on the station — left it compiling with one warning, that this
    /// is for testing only. The role is found by its identifier, not its name, which follows TIA Portal's language.
    ///
    /// The anonymous user belongs to the whole project, so this reaches every PLC whose access goes through users and
    /// roles. The tool's plan says so, and its state before is recorded.
    /// </remarks>
    internal sealed class AnonymousUserAccess
    {
        private const string FullAccessRole = "PLCAdministrator";
        private const string RecordFile = "anonymous-user.txt";

        private readonly UmacConfigurator _umac;
        private readonly SystemRole _role;

        private AnonymousUserAccess(UmacConfigurator umac, SystemRole role)
        {
            _umac = umac;
            _role = role;
        }

        /// <summary>Finds what granting needs, before anything is changed.</summary>
        /// <param name="project">The open project.</param>
        /// <returns>What <see cref="GrantFullAccess"/> then changes.</returns>
        /// <exception cref="PortalException">The project offers no users and roles, or no such role.</exception>
        /// <remarks>
        /// Separate from granting so a caller can check it before unprotecting any PLC: a PLC left unprotected for a
        /// grant that then could not happen is a change the caller was told did not happen.
        /// </remarks>
        internal static AnonymousUserAccess Require(ProjectBase project)
        {
            var umac = project.GetService<UmacConfigurator>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, "This project offers no users and roles, which this PLC's access goes through. Nothing was changed.");
            var role = umac.SystemRoles.FirstOrDefault(candidate => candidate.Identifier == FullAccessRole)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"This project has no system role '{FullAccessRole}'. Nothing was changed.");

            RequireNoUsersSetUp(umac, role);

            return new AnonymousUserAccess(umac, role);
        }

        /// <remarks>
        /// The anonymous user reaches every PLC on users and roles, so it is given full access only in a project where
        /// nobody set users up — the state a class project is created in — or where it has that access already. Users,
        /// custom roles or UMC users mean somebody protected the project's PLCs on purpose, and opening them all through
        /// the anonymous user is not this server's to decide.
        /// </remarks>
        private static void RequireNoUsersSetUp(UmacConfigurator umac, SystemRole role)
        {
            var anonymous = umac.AnonymousUser;

            if (anonymous != null && anonymous.IsActive && anonymous.Roles.Contains(role))
            {
                return;
            }

            var setUp = new[]
            {
                umac.ProjectUsers.Any() ? "project users" : null,
                umac.CustomRoles.Any() ? "custom roles" : null,
                umac.UmcUsers.Any() || umac.UmcUserGroups.Any() ? "UMC users" : null
            }.Where(found => found != null).ToList();

            if (setUp.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"This project has {string.Join(", ", setUp)}: somebody set its users and roles up, and giving the Anonymous user full access would open "
                    + "every PLC on users and roles. Give a user full access in TIA Portal (Security settings, Users and roles) instead. Nothing was changed.");
            }
        }

        /// <summary>Activates the anonymous user and gives it the PLC administrator role, where it lacks either.</summary>
        /// <param name="backupDirectory">Where its state before is recorded.</param>
        /// <returns>What it was and what it is now.</returns>
        internal string GrantFullAccess(string backupDirectory)
        {
            var before = Describe(_umac);

            Record(backupDirectory, before);

            if (_umac.AnonymousUser == null || !_umac.AnonymousUser.IsActive)
            {
                _umac.ActivateAnonymousUser();
            }

            if (!_umac.AnonymousUser!.Roles.Contains(_role))
            {
                _umac.AnonymousUser.Roles.Add(_role);
            }

            return $"the project's Anonymous user is active with role '{_role.Name}' (it was {before}); it reaches every PLC whose access goes through users and roles";
        }

        private static string Describe(UmacConfigurator umac)
        {
            var anonymous = umac.AnonymousUser;

            return anonymous == null || !anonymous.IsActive
                ? "inactive"
                : "active with roles " + (anonymous.Roles.Any() ? string.Join(", ", anonymous.Roles.Select(role => role.Name)) : "none");
        }

        private static void Record(string backupDirectory, string before)
        {
            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(Path.Combine(backupDirectory, RecordFile), "# The project's Anonymous user before\n" + before + "\n", new UTF8Encoding(false));
        }
    }
}
