namespace TiaMcpServer.Library
{
    /// <summary>One document of a library type version set against its counterpart in another version.</summary>
    public sealed class DocumentComparison
    {
        /// <summary>Creates the comparison of one document.</summary>
        /// <param name="name">The document's file name, e.g. <c>FC_Motor.s7dcl</c>.</param>
        /// <param name="state">How the two compare.</param>
        /// <param name="firstDifferentLine">The first line, counted from 1, where the texts part; 0 when they do not, or one side is missing.</param>
        public DocumentComparison(string name, DocumentComparisonState state, int firstDifferentLine)
        {
            Name = name;
            State = state;
            FirstDifferentLine = firstDifferentLine;
        }

        /// <summary>The document's file name.</summary>
        public string Name { get; }

        /// <summary>How the two versions' documents compare.</summary>
        public DocumentComparisonState State { get; }

        /// <summary>The first line, counted from 1, where the texts part; 0 when they do not, or one side is missing.</summary>
        public int FirstDifferentLine { get; }
    }
}
