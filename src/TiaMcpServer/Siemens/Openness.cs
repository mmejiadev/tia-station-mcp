using Siemens.Collaboration.Net;
using System.Threading.Tasks;

namespace TiaMcpServer.Siemens
{
    public static class Openness
    {
        public static int TiaMajorVersion { get; private set; }

        public static void Initialize(int? tiaMajorVersion = 20)
        {
            // with nuget packages:
            // 2.1 nuget package: Siemens.Collaboration.Net.TiaPortal.Openness.Resolver
            //     & User Environment Variable: TiaPortalLocation=C:\Program Files\Siemens\Automation\Portal V20
            // 2.2 nuget package: Siemens.Collaboration.Net.TiaPortal.Packages.Openness
            // 2.3 Api.Global.Openness().Initialize(tiaMajorVersion: 20); // fixed version 20

            TiaMajorVersion = tiaMajorVersion ?? 20; // Default to TIA Portal V20 if not specified

            // The same version, recorded in both places that hold it. The version-gated tools read
            // Engineering.TiaMajorVersion, which only Program used to set, so any host that
            // initialised Openness without going through Program left it at 0 — and every V20-only
            // tool refused to run on a V20 machine. The test suite is exactly such a host, which is
            // where this surfaced.
            Engineering.TiaMajorVersion = TiaMajorVersion;

            // Initialize the Openness API with the specified TIA Portal major version
            Api.Global.Openness().Initialize(tiaMajorVersion: tiaMajorVersion);
        }

        public static async Task<bool> IsUserInGroup()
        {
            if (Api.Global.Openness().IsUserInGroup())
            {
                // user is in group
                return true;
            }
            else
            {
                return await Api.Global.Openness().AddUserToGroupAsync();
            }
        }
    }
}
