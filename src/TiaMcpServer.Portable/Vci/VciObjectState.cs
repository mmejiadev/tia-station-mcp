namespace TiaMcpServer.Vci
{
    /// <summary>
    /// One project object mapped to a file of a VCI workspace, and how the two compare.
    /// </summary>
    /// <remarks>The states are Openness's words, so what is reported reads as TIA Portal's own.</remarks>
    public sealed class VciObjectState
    {
        /// <summary>Creates the description of a mapped object.</summary>
        /// <param name="file">The file, relative to the workspace's root, e.g. <c>PLC_1/Blocks/Main.xml</c>.</param>
        /// <param name="compareState">How they compare: <c>Equal</c>, <c>Unequal</c>, <c>Unknown</c> or <c>WorkspaceFileMissing</c>.</param>
        /// <param name="changedSide">Which side changed: <c>None</c>, <c>ProjectObjectChanged</c> or <c>WorkspaceFileChanged</c>.</param>
        public VciObjectState(string file, string compareState, string changedSide)
        {
            File = file;
            CompareState = compareState;
            ChangedSide = changedSide;
        }

        /// <summary>The file, relative to the workspace's root.</summary>
        public string File { get; }

        /// <summary>How the object and its file compare.</summary>
        public string CompareState { get; }

        /// <summary>Which side changed.</summary>
        public string ChangedSide { get; }

        /// <summary>The object as one line.</summary>
        public string Line => $"{File} | {CompareState} | {ChangedSide}";
    }
}
