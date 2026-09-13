using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One subnet of the project: the wire several interfaces share.
    /// </summary>
    /// <remarks>
    /// A subnet is the thing <see cref="NetworkNodeInfo"/> names but does not describe. The topology
    /// says which subnet an interface sits on; this says what that subnet is, how many interfaces it
    /// carries and which IO systems run over it -- which is what has to be known before connecting
    /// anything to it, because a node can only join a subnet of its own network type.
    ///
    /// A subnet with one node is worth seeing: it is a wire with nothing at the other end, and the
    /// usual reason two devices that look connected in the project cannot reach each other.
    /// </remarks>
    public sealed class SubnetInfo
    {
        /// <summary>Creates a subnet description.</summary>
        /// <param name="name">The subnet's name, as it is referred to when connecting a node.</param>
        /// <param name="networkType">Ethernet, Profibus and so on.</param>
        /// <param name="nodeNames">The interfaces attached to it, by node name.</param>
        /// <param name="ioSystemNames">The IO systems running over it.</param>
        public SubnetInfo(
            string name,
            string networkType,
            IReadOnlyList<string> nodeNames,
            IReadOnlyList<string> ioSystemNames)
        {
            Name = name;
            NetworkType = networkType;
            NodeNames = nodeNames;
            IoSystemNames = ioSystemNames;
        }

        /// <summary>The subnet's name. This is what a node is connected to it by.</summary>
        public string Name { get; }

        /// <summary>Ethernet, Profibus and so on. A node can only join a subnet of its own type.</summary>
        public string NetworkType { get; }

        /// <summary>The interfaces attached to it, by node name.</summary>
        public IReadOnlyList<string> NodeNames { get; }

        /// <summary>The IO systems running over it, if any.</summary>
        public IReadOnlyList<string> IoSystemNames { get; }

        /// <summary>True when nothing but a single interface sits on it: a wire to nowhere.</summary>
        public bool IsDeadEnd => NodeNames.Count < 2;
    }
}
