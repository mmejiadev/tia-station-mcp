namespace TiaMcpServer.Library
{
    /// <summary>A device group template to bring into the open project, and how to connect it.</summary>
    public sealed class DeviceTemplatePlacement
    {
        /// <summary>Creates the placement.</summary>
        /// <param name="globalLibraryPath">The global library file holding the template.</param>
        /// <param name="templateName">The template's name.</param>
        /// <param name="subnetName">The subnet to join its devices to; created when there is none of that name.</param>
        /// <param name="plcSecretName">The stored secret to protect its PLCs with, by name; empty for none.</param>
        public DeviceTemplatePlacement(string globalLibraryPath, string templateName, string subnetName, string plcSecretName)
        {
            GlobalLibraryPath = globalLibraryPath;
            TemplateName = templateName;
            SubnetName = subnetName;
            PlcSecretName = plcSecretName;
        }

        /// <summary>The global library file holding the template.</summary>
        public string GlobalLibraryPath { get; }

        /// <summary>The template's name.</summary>
        public string TemplateName { get; }

        /// <summary>The subnet to join its devices to.</summary>
        public string SubnetName { get; }

        /// <summary>The stored secret to protect its PLCs with, by name; never its value.</summary>
        public string PlcSecretName { get; }
    }
}
