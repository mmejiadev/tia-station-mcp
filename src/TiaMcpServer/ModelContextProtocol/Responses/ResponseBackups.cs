using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Everything the backup registry holds.</summary>
    public sealed class ResponseBackups : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One entry per backup, newest first.</param>
        public ResponseBackups(IReadOnlyList<ResponseBackup> items)
        {
            Items = items;
        }

        /// <summary>One entry per backup, newest first.</summary>
        public IReadOnlyList<ResponseBackup> Items { get; }
    }
}
