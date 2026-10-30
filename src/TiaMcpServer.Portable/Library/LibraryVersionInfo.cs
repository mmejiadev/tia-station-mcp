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
        {
            Number = LibraryVersionNumber.Normalise(number);
            IsReleased = isReleased;
        }

        /// <summary>The version number, in three parts.</summary>
        public Version Number { get; }

        /// <summary>True when the version is released (committed), false when it is in work.</summary>
        public bool IsReleased { get; }
    }
}
