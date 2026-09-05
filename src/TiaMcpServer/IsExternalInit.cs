using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Makes <c>init</c>-only properties compile on .NET Framework.
    /// </summary>
    /// <remarks>
    /// The compiler emits a reference to this type for every <c>init</c> accessor. .NET 5 and later
    /// ship it; .NET Framework 4.8 does not, and without it the language feature simply fails to
    /// build. Declaring it here is the documented way round that, and it costs nothing at run time:
    /// the type is never instantiated, only named in metadata.
    ///
    /// It exists so a DTO can be immutable and still be written with an object initialiser. The
    /// alternative on this target framework is a constructor per field ordering, or public setters
    /// on objects that describe things that already happened, and CLAUDE.md forbids the second.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
