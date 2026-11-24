namespace TiaMcpServer.Library
{
    /// <summary>How one document of a library type version compares with its counterpart in another version.</summary>
    public enum DocumentComparisonState
    {
        /// <summary>Both versions hold it, with the same text once line endings and trailing blanks are set aside.</summary>
        Identical,

        /// <summary>Both versions hold it, and its text differs.</summary>
        Different,

        /// <summary>Only the first version holds it.</summary>
        OnlyLeft,

        /// <summary>Only the second version holds it.</summary>
        OnlyRight
    }
}
