using System;

namespace TiaMcpServer.Library
{
    /// <summary>One version of a library type as TIA Portal identifies it: its number, its GUID, and whether it is released.</summary>
    /// <remarks>
    /// The GUID is what tells two versions of one number apart. Measured 2026-11-24 on TIA Portal V21: a version published
    /// from a project library to a global library keeps its GUID there, and so does the type.
    /// </remarks>
    public sealed class LibraryVersionIdentity
    {
        /// <summary>Creates a version identity.</summary>
        /// <param name="number">The version number, kept in three parts.</param>
        /// <param name="id">The version's GUID.</param>
        /// <param name="isReleased">True when committed, false when in work.</param>
        public LibraryVersionIdentity(Version number, Guid id, bool isReleased)
        {
            Number = LibraryVersionNumber.Normalise(number);
            Id = id;
            IsReleased = isReleased;
        }

        /// <summary>The version number, in three parts.</summary>
        public Version Number { get; }

        /// <summary>The version's GUID.</summary>
        public Guid Id { get; }

        /// <summary>True when the version is released (committed), false when it is in work.</summary>
        public bool IsReleased { get; }
    }
}
