using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Every long operation this session has run.</summary>
    public sealed class ResponseJobs : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One entry per job, newest first.</param>
        public ResponseJobs(IReadOnlyList<ResponseJob> items)
        {
            Items = items;
        }

        /// <summary>One entry per job, newest first.</summary>
        public IReadOnlyList<ResponseJob> Items { get; }
    }
}
