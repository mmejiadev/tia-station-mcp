using System;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>One faceplate instance to place on a WinCC Unified screen.</summary>
    public sealed class UnifiedFaceplateDescription
    {
        /// <summary>Creates a description.</summary>
        /// <param name="name">The container's name, unique on the screen.</param>
        /// <param name="type">The library type, and the released version to show or null for the newest.</param>
        /// <param name="frame">Left and top, and the size or null to keep the type's own.</param>
        /// <param name="interfaceValues">What each interface property is given, by property name.</param>
        public UnifiedFaceplateDescription(string name, (string Name, Version? Version) type, UnifiedFaceplateFrame frame, IReadOnlyDictionary<string, UnifiedFaceplateInterfaceValue> interfaceValues)
        {
            Name = name;
            TypeName = type.Name;
            Version = type.Version;
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Interface = interfaceValues ?? throw new ArgumentNullException(nameof(interfaceValues));
        }

        /// <summary>The container's name.</summary>
        public string Name { get; }

        /// <summary>The faceplate's library type.</summary>
        public string TypeName { get; }

        /// <summary>The released version to show, or null for the newest released one.</summary>
        public Version? Version { get; }

        /// <summary>Where the container goes, and its size.</summary>
        public UnifiedFaceplateFrame Frame { get; }

        /// <summary>What each interface property is given, by property name.</summary>
        public IReadOnlyDictionary<string, UnifiedFaceplateInterfaceValue> Interface { get; }
    }
}
