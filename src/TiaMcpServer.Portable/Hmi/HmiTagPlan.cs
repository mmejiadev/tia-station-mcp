using System.Collections.Generic;

namespace TiaMcpServer.Hmi
{
    /// <summary>What generating HMI tags from a PLC program would write, and what it leaves alone.</summary>
    public sealed class HmiTagPlan
    {
        /// <summary>Creates a plan.</summary>
        /// <param name="links">The tags the generated table holds.</param>
        /// <param name="alreadyPresent">PLC tags left out because an HMI tag of that name is in another table.</param>
        /// <param name="displaced">Tags in the target table that the generated one would not hold.</param>
        public HmiTagPlan(IReadOnlyList<HmiTagLink> links, IReadOnlyList<string> alreadyPresent, IReadOnlyList<string> displaced)
        {
            Links = links;
            AlreadyPresent = alreadyPresent;
            Displaced = displaced;
        }

        /// <summary>The tags the generated table holds.</summary>
        public IReadOnlyList<HmiTagLink> Links { get; }

        /// <summary>PLC tags left out because an HMI tag of that name is in another table.</summary>
        public IReadOnlyList<string> AlreadyPresent { get; }

        /// <summary>
        /// Tags in the target table that the generated one would not hold. Importing replaces a
        /// table whole, so any of these would be deleted; a plan that has some must not be written.
        /// </summary>
        public IReadOnlyList<string> Displaced { get; }
    }
}
