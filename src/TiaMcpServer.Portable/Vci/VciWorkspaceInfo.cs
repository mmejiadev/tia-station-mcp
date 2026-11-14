namespace TiaMcpServer.Vci
{
    /// <summary>A VCI workspace of the open project.</summary>
    public sealed class VciWorkspaceInfo
    {
        /// <summary>Creates the description of a workspace.</summary>
        /// <param name="name">Its name in the project.</param>
        /// <param name="rootPath">The folder its files are in.</param>
        /// <param name="mappedObjects">How many project objects are mapped to its files.</param>
        public VciWorkspaceInfo(string name, string rootPath, int mappedObjects)
        {
            Name = name;
            RootPath = rootPath;
            MappedObjects = mappedObjects;
        }

        /// <summary>Its name in the project.</summary>
        public string Name { get; }

        /// <summary>The folder its files are in.</summary>
        public string RootPath { get; }

        /// <summary>How many project objects are mapped to its files.</summary>
        public int MappedObjects { get; }
    }
}
