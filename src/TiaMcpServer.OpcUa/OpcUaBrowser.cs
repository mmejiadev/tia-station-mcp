using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// Lists the children of one node, following continuation points up to a bound.
    /// </summary>
    /// <remarks>
    /// A server returns a browse in pages and says so with a continuation point. A loop that ignored
    /// it would report the first page as the whole folder, and a data block with more members than
    /// fit in one page would look shorter than it is. The bound stops a server that keeps answering
    /// from turning a read into an unbounded one.
    /// </remarks>
    internal static class OpcUaBrowser
    {
        internal static async Task<IReadOnlyList<OpcUaNode>> BrowseAsync(
            ISession session,
            NodeId parent,
            uint limit,
            CancellationToken cancellationToken)
        {
            var request = new BrowseDescriptionCollection
            {
                new BrowseDescription
                {
                    NodeId = parent,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                    ResultMask = (uint)BrowseResultMask.All
                }
            };

            var response = await session.BrowseAsync(null, null, limit, request, cancellationToken).ConfigureAwait(false);
            var result = response.Results[0];

            RequireGood(parent, result.StatusCode);

            var references = new List<ReferenceDescription>(result.References);
            await FollowContinuationAsync(session, result.ContinuationPoint, references, limit, cancellationToken).ConfigureAwait(false);

            return references
                .Take((int)limit)
                .Select(reference => Describe(session, reference))
                .ToList();
        }

        private static async Task FollowContinuationAsync(
            ISession session,
            byte[]? continuationPoint,
            List<ReferenceDescription> references,
            uint limit,
            CancellationToken cancellationToken)
        {
            while (continuationPoint != null && continuationPoint.Length > 0 && references.Count < limit)
            {
                var next = await session.BrowseNextAsync(
                    null,
                    false,
                    new ByteStringCollection { continuationPoint },
                    cancellationToken).ConfigureAwait(false);

                references.AddRange(next.Results[0].References);
                continuationPoint = next.Results[0].ContinuationPoint;
            }

            // Stopping at the bound leaves a continuation point open on the server. Releasing it is
            // courtesy on a PC and a real limit on a CPU, which holds only a handful.
            if (continuationPoint != null && continuationPoint.Length > 0)
            {
                await session.BrowseNextAsync(null, true, new ByteStringCollection { continuationPoint }, cancellationToken).ConfigureAwait(false);
            }
        }

        private static OpcUaNode Describe(ISession session, ReferenceDescription reference)
        {
            var nodeId = ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);

            return new OpcUaNode(
                nodeId?.ToString() ?? reference.NodeId.ToString(),
                reference.BrowseName.ToString(),
                reference.DisplayName.Text ?? string.Empty,
                reference.NodeClass.ToString());
        }

        private static void RequireGood(NodeId parent, StatusCode status)
        {
            if (StatusCode.IsGood(status))
            {
                return;
            }

            throw new TiaMcpServer.Siemens.PortalException(
                TiaMcpServer.Siemens.PortalErrorCode.NotFound,
                $"The server could not browse '{parent}': {OpcUaValueFormatter.DescribeStatus(status)}.");
        }
    }
}
