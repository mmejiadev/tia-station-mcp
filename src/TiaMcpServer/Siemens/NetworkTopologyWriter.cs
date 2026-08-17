using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Writes the network layout into a snapshot, as text built for diffing.
    /// </summary>
    /// <remarks>
    /// Openness V20 exposes no hardware export — there is no AML or device export in the public
    /// API — so the network is reconstructed from what can be read and written as a table. It does
    /// not capture module part numbers or rack layout; it does capture what changes and matters
    /// between revisions: which device sits on which subnet, at which address.
    ///
    /// The rows are sorted. A snapshot whose line order depends on the order TIA happened to
    /// enumerate devices produces phantom diffs, which trains everyone to ignore them.
    /// </remarks>
    internal static class NetworkTopologyWriter
    {
        private const string FileName = "network/topology.txt";
        private const string Separator = " | ";
        private const string Unconnected = "<not connected>";

        /// <summary>Writes the topology under a snapshot root.</summary>
        /// <param name="rootDirectory">The snapshot root.</param>
        /// <param name="nodes">The interfaces to record.</param>
        /// <returns>The path written, relative to the root, using forward slashes.</returns>
        internal static string Write(string rootDirectory, IReadOnlyList<NetworkNodeInfo> nodes)
        {
            var path = Path.Combine(rootDirectory, FileName.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            File.WriteAllText(path, Render(nodes), new UTF8Encoding(false));

            return FileName;
        }

        private static string Render(IReadOnlyList<NetworkNodeInfo> nodes)
        {
            var builder = new StringBuilder();

            builder.AppendLine("# device | interface | type | address | subnet");

            var ordered = nodes
                .OrderBy(node => node.DevicePath, System.StringComparer.Ordinal)
                .ThenBy(node => node.InterfaceName, System.StringComparer.Ordinal);

            foreach (var node in ordered)
            {
                builder.AppendLine(string.Join(
                    Separator,
                    node.DevicePath,
                    node.InterfaceName,
                    node.NetworkType,
                    node.Address,
                    node.IsConnected ? node.SubnetName : Unconnected));
            }

            builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "# {0} interface(s)", nodes.Count));

            return builder.ToString();
        }
    }
}
