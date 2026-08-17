using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One OPC UA server interface published by a CPU.
    /// </summary>
    /// <remarks>
    /// A server interface is the contract between the PLC and anything that talks to it over
    /// OPC UA: it names which tags are exposed and under what node names. It is configuration
    /// rather than code, which is exactly why it belongs under version control — a change here
    /// breaks every client without touching a single line of SCL.
    /// </remarks>
    public sealed class OpcUaInterfaceInfo
    {
        /// <summary>Creates an interface description.</summary>
        /// <param name="name">The interface name.</param>
        /// <param name="isEnabled">Whether the CPU actually publishes it.</param>
        /// <param name="author">Who last authored it.</param>
        /// <param name="lastModified">When it last changed.</param>
        public OpcUaInterfaceInfo(string name, bool isEnabled, string author, DateTime lastModified)
        {
            Name = name;
            IsEnabled = isEnabled;
            Author = author;
            LastModified = lastModified;
        }

        /// <summary>The interface name.</summary>
        public string Name { get; }

        /// <summary>
        /// Whether the CPU actually publishes it. A disabled interface still exists in the project
        /// and still exports, so a client failing to connect is often just this.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>Who last authored it.</summary>
        public string Author { get; }

        /// <summary>When it last changed.</summary>
        public DateTime LastModified { get; }
    }
}
