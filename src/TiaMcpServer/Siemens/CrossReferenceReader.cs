using Siemens.Engineering;
using Siemens.Engineering.CrossReference;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the cross references of one object out of TIA Portal and flattens them.
    /// </summary>
    /// <remarks>
    /// Openness answers as a three-level tree: the source objects the question was asked about,
    /// the objects each of them is related to, and the locations where each relation happens. Only
    /// the leaves carry the direction and the kind of access, so a caller handed anything above
    /// them would know that two blocks are related without knowing which calls which.
    ///
    /// The walk descends into <see cref="SourceObject.Children"/> as well. Asking a block for its
    /// cross references can answer with the block's members rather than the block alone -- a data
    /// block's tags, for instance -- and a reader that stopped at the top would report a block
    /// nobody uses while its tags are read from everywhere.
    /// </remarks>
    public static class CrossReferenceReader
    {
        /// <summary>Reads every cross reference of an object.</summary>
        /// <param name="provider">The object to ask, which must offer the cross-reference service.</param>
        /// <param name="subjectPath">The object's path, used only to say what failed.</param>
        /// <returns>One entry per location, in the order Openness holds them.</returns>
        /// <exception cref="PortalException">The object does not offer the service.</exception>
        public static IReadOnlyList<CrossReferenceInfo> Read(IEngineeringServiceProvider provider, string subjectPath)
        {
            if (provider == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "provider is required");
            }

            var service = provider.GetService<CrossReferenceService>()
                ?? throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"TIA Portal offers no cross-reference service for '{subjectPath}'");

            var references = new List<CrossReferenceInfo>();

            foreach (var source in service.GetCrossReferences(CrossReferenceFilter.AllObjects).Sources)
            {
                CollectSource(source, references);
            }

            return references;
        }

        private static void CollectSource(SourceObject source, List<CrossReferenceInfo> references)
        {
            foreach (var reference in source.References)
            {
                CollectReference(reference, references);
            }

            foreach (var child in source.Children)
            {
                CollectSource(child, references);
            }
        }

        private static void CollectReference(ReferenceObject reference, List<CrossReferenceInfo> references)
        {
            foreach (var location in reference.Locations)
            {
                var usage = new CrossReferenceUsage(
                    location.ReferenceType.ToString(),
                    location.Access.ToString(),
                    location.ReferenceLocation ?? string.Empty,
                    location.Address ?? string.Empty);

                references.Add(new CrossReferenceInfo(
                    reference.Name ?? string.Empty,
                    reference.Path ?? string.Empty,
                    reference.TypeName ?? string.Empty,
                    usage));
            }
        }
    }
}
