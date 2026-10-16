namespace TiaMcpServer.Hmi
{
    /// <summary>Where a symbolic I/O field takes its text from: a tag's value, looked up in a text list.</summary>
    public sealed class HmiSymbolicSource
    {
        /// <summary>Creates the source.</summary>
        /// <param name="tag">The HMI tag whose value chooses the text.</param>
        /// <param name="textList">The text list on the panel.</param>
        public HmiSymbolicSource(string tag, string textList)
        {
            Tag = tag;
            TextList = textList;
        }

        /// <summary>The HMI tag whose value chooses the text.</summary>
        public string Tag { get; }

        /// <summary>The text list on the panel.</summary>
        public string TextList { get; }
    }
}
