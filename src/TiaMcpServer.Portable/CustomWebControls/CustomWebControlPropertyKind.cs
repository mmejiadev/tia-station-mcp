namespace TiaMcpServer.CustomWebControls
{
    /// <summary>The kinds of value a Custom Web Control's property or event argument holds.</summary>
    /// <remarks>
    /// The ones Siemens' own controls declare in their manifests (TIA Portal V21, <c>Data\Hmi\CustomControls</c>):
    /// JSON schema's boolean, string, number and integer, and WinCC Unified's colour, a <c>$ref</c> to
    /// <c>http://tia.siemens.com/wincc-unified/types/s/color</c> holding an ARGB number.
    /// </remarks>
    public enum CustomWebControlPropertyKind
    {
        /// <summary>True or false; bound to a Bool tag.</summary>
        Boolean,

        /// <summary>A text: <c>string</c> in a manifest.</summary>
        Text,

        /// <summary>A floating-point number.</summary>
        Number,

        /// <summary>A whole number: <c>integer</c> in a manifest.</summary>
        WholeNumber,

        /// <summary>A WinCC Unified colour, an ARGB number in the manifest.</summary>
        Color
    }
}
