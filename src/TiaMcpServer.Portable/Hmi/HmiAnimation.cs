using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A change an item undergoes in runtime when a tag changes: its colour, its visibility.</summary>
    /// <remarks>
    /// Each kind writes its own XML into the item's <c>Animations</c> composition, the shape the class
    /// exports have. Which kinds TIA Portal accepts on which items is checked by the import, not here.
    /// </remarks>
    public abstract class HmiAnimation
    {
        /// <summary>The kind, as a description names it: <c>appearance</c> or <c>visibility</c>.</summary>
        public abstract string Kind { get; }

        /// <summary>Writes the animation.</summary>
        /// <param name="writer">The screen's writer.</param>
        /// <returns>The <c>Animations</c> object.</returns>
        internal abstract XElement Write(HmiScreenWriter writer);
    }
}
