namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One place where another object and the one being asked about meet.
    /// </summary>
    /// <remarks>
    /// The question this answers is "who calls this block", which is the one to ask before
    /// rewriting it. Openness answers as a three-level tree -- source object, reference object,
    /// location -- and this is one leaf of it: the other object, and a single place the two touch.
    /// A block called from four networks produces four of these.
    ///
    /// The direction travels on every entry, in <see cref="CrossReferenceUsage.ReferenceType"/>,
    /// rather than being implied by which list an entry ended up in. Flattening a tree is exactly
    /// what makes "calls" and "is called by" easy to confuse, and the two are not interchangeable.
    /// </remarks>
    public sealed class CrossReferenceInfo
    {
        /// <summary>Creates a cross-reference entry.</summary>
        /// <param name="name">The other object's name, as Openness spells it.</param>
        /// <param name="path">The other object's path in the project, or empty when it has none.</param>
        /// <param name="typeName">What the other object is: a block, a tag, a type.</param>
        /// <param name="usage">Which way the relation runs, how, and where.</param>
        public CrossReferenceInfo(string name, string path, string typeName, CrossReferenceUsage usage)
        {
            Name = name;
            Path = path;
            TypeName = typeName;
            Usage = usage;
        }

        /// <summary>The other object's name.</summary>
        public string Name { get; }

        /// <summary>The other object's path in the project.</summary>
        public string Path { get; }

        /// <summary>What the other object is.</summary>
        public string TypeName { get; }

        /// <summary>Which way the relation runs, how the object is touched, and where.</summary>
        public CrossReferenceUsage Usage { get; }

        /// <summary>
        /// Whether the other object depends on the subject, which is the case a rewrite has to
        /// look at first.
        /// </summary>
        public bool IsIncoming => Usage.ReferenceType == UsedByRelation;

        /// <summary>Whether the subject depends on the other object.</summary>
        public bool IsOutgoing => Usage.ReferenceType == UsesRelation;

        /// <summary>The entry as one line, for a caller that reads rather than parses.</summary>
        public string Line =>
            $"{Name} | {Path} | {TypeName} | {Usage.ReferenceType} | {Usage.Access} | {Usage.Location} | {Usage.Address}";

        /// <summary>Openness' name for "the other object uses this one".</summary>
        internal const string UsedByRelation = "UsedBy";

        /// <summary>Openness' name for "this object uses the other one".</summary>
        internal const string UsesRelation = "Uses";
    }
}
