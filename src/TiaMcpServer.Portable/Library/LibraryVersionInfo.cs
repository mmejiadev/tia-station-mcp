using System;

namespace TiaMcpServer.Library
{
    /// <summary>One version of a library type: its number, and whether it is released or still in work.</summary>
    public sealed class LibraryVersionInfo
    {
        /// <summary>Creates a version.</summary>
        /// <param name="number">The version number, kept in three parts.</param>
        /// <param name="isReleased">True when committed, false when in work.</param>
        public LibraryVersionInfo(Version number, bool isReleased)
            : this(number, isReleased, false)
        {
        }

        /// <summary>Creates a version, saying whether it is the type's default.</summary>
        /// <param name="number">The version number, kept in three parts.</param>
        /// <param name="isReleased">True when committed, false when in work.</param>
        /// <param name="isDefault">True when it is the version new instances take.</param>
        public LibraryVersionInfo(Version number, bool isReleased, bool isDefault)
        {
            Number = LibraryVersionNumber.Normalise(number);
            IsReleased = isReleased;
            IsDefault = isDefault;
        }

        /// <summary>The version number, in three parts.</summary>
        public Version Number { get; }

        /// <summary>True when the version is released (committed), false when it is in work.</summary>
        public bool IsReleased { get; }

        /// <summary>True when it is the type's default, the version new instances take.</summary>
        public bool IsDefault { get; }
    }
}
