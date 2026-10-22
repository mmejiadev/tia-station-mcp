namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One entry of TIA Portal's hardware catalogue: a product at one firmware version.
    /// </summary>
    /// <remarks>
    /// The point of reading the catalogue is <see cref="TypeIdentifier"/>: it is the exact string
    /// <c>AddDevice</c> and <c>PlugModule</c> take, so a device is added from what this installation
    /// offers rather than from an order number somebody remembered. The same article appears once per
    /// firmware version and, for panels, once more per orientation (<c>/Portrait</c>), which is why
    /// every field is kept and none is merged.
    /// </remarks>
    public sealed class CatalogEntryInfo
    {
        /// <summary>Creates a catalogue entry.</summary>
        /// <param name="articleNumber">The order number, for example <c>6AV2 124-0JC01-0AX0</c>.</param>
        /// <param name="version">The firmware version, for example <c>17.0.0.0</c> or <c>V4.6</c>.</param>
        /// <param name="typeIdentifier">The identifier to create it with.</param>
        /// <param name="catalogPath">Where TIA Portal's catalogue shows it.</param>
        /// <param name="description">The catalogue's description of the product.</param>
        public CatalogEntryInfo(string articleNumber, string version, string typeIdentifier, string catalogPath, string description)
        {
            ArticleNumber = articleNumber;
            Version = version;
            TypeIdentifier = typeIdentifier;
            CatalogPath = catalogPath;
            Description = description;
        }

        /// <summary>The order number.</summary>
        public string ArticleNumber { get; }

        /// <summary>The firmware version.</summary>
        public string Version { get; }

        /// <summary>The identifier <c>AddDevice</c> and <c>PlugModule</c> take, copied as is.</summary>
        public string TypeIdentifier { get; }

        /// <summary>Where the catalogue shows it, for example <c>Root\HMI\SIMATIC Comfort Panel\…</c>.</summary>
        public string CatalogPath { get; }

        /// <summary>The catalogue's description.</summary>
        public string Description { get; }
    }
}
