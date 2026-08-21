using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One entry of a virtual controller's tag list: a name a value can be read or written by.
    /// </summary>
    /// <remarks>
    /// The tag list is read from the program the controller holds, so it exists only after a
    /// download. A controller that has never been downloaded to has an empty list, and that is not
    /// an error: it is the honest answer to "what can I observe here".
    /// </remarks>
    public sealed class SimulationTagInfo
    {
        /// <summary>Creates a tag list entry.</summary>
        /// <param name="name">The symbolic name, as the program declares it.</param>
        /// <param name="area">Input, Output, Marker, Timer, Counter or DataBlock.</param>
        /// <param name="dataType">The declared PLC data type, e.g. Bool, Int, DInt, Real.</param>
        /// <param name="isReadable">Whether this server can read a value for it.</param>
        public SimulationTagInfo(string name, string area, string dataType, bool isReadable)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Area = area ?? throw new ArgumentNullException(nameof(area));
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            IsReadable = isReadable;
        }

        /// <summary>
        /// The symbolic name, as the controller reports it and as a read must spell it. Members of a
        /// data block are fully qualified and carry <b>no quotes</b>:
        /// <c>DB_Cell.Feeder.Step</c> — whatever SCL requires when writing the same name.
        /// </summary>
        public string Name { get; }

        /// <summary>Input, Output, Marker, Timer, Counter or DataBlock.</summary>
        public string Area { get; }

        /// <summary>The declared PLC data type, e.g. Bool, Int, DInt, Real.</summary>
        public string DataType { get; }

        /// <summary>
        /// Whether this server can read a value for it. False for a struct or an array, which have
        /// no value of their own: their members are separate entries in the same list.
        /// </summary>
        public bool IsReadable { get; }
    }
}
