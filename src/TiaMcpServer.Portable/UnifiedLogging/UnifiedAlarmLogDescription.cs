using System.Collections.Generic;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>A WinCC Unified alarm log and the alarm classes logged to it.</summary>
    public sealed class UnifiedAlarmLogDescription
    {
        /// <summary>Describes an alarm log.</summary>
        /// <param name="name">The log's name.</param>
        /// <param name="classes">The alarm classes logged to it; at least one, or the compile refuses the log.</param>
        public UnifiedAlarmLogDescription(string name, IReadOnlyList<string> classes)
        {
            Name = name;
            Classes = classes;
        }

        /// <summary>The log's name.</summary>
        public string Name { get; }

        /// <summary>The alarm classes logged to it.</summary>
        public IReadOnlyList<string> Classes { get; }
    }
}
