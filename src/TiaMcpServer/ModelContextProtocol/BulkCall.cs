using System;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// What one bulk export or import was asked to do, and when it started.
    /// </summary>
    /// <remarks>
    /// The four bulk tools carry the same four values into every message, log line and response
    /// they build; this keeps the methods that build them under the parameter limit.
    /// </remarks>
    public sealed class BulkCall
    {
        /// <summary>Records a call starting now.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="directory">The directory exported to or imported from.</param>
        /// <param name="regexName">Name or regular expression selecting the items; empty for all.</param>
        public BulkCall(string softwarePath, string directory, string regexName)
        {
            SoftwarePath = softwarePath;
            Directory = directory;
            RegexName = regexName;
            StartTime = DateTime.Now;
        }

        /// <summary>Full path to the PLC software.</summary>
        public string SoftwarePath { get; }

        /// <summary>The directory exported to or imported from.</summary>
        public string Directory { get; }

        /// <summary>Name or regular expression selecting the items; empty for all.</summary>
        public string RegexName { get; }

        /// <summary>When the call started.</summary>
        public DateTime StartTime { get; }

        /// <summary>Seconds since the call started.</summary>
        public double ElapsedSeconds => (DateTime.Now - StartTime).TotalSeconds;
    }
}
