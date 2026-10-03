using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseExportTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
        public IEnumerable<ResponseTypeInfo>? Inconsistent { get; set; }

        /// <summary>
        /// One line per item that could not be exported, as <c>Name: reason</c>. An item missing from
        /// <see cref="Items"/> that is not in <see cref="Inconsistent"/> is named here.
        /// </summary>
        public IEnumerable<string>? Failed { get; set; }
    }
}
