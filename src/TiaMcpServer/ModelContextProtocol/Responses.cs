using System;
using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseAttributes : ResponseMessage
    {
        public IEnumerable<ObjectAttribute>? Attributes { get; set; }
    }

    public class ResponseSoftwareInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceItemInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseBlockInfo : ResponseAttributes
    {
        public string? Path { get; set; }
        public string? TypeName { get; set; }
        public string? Name { get; set; }
        public string? Namespace { get; set; }
        public string? ProgrammingLanguage { get; set; }
        public string? MemoryLayout { get; set; }
        public bool? IsConsistent { get; set; }
        public string? HeaderName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }
    public class ResponseBlocksWithHierarchy : ResponseMessage
    {
        public BlockGroupDescription? Root { get; set; }
    }

    public class ResponseTypeInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
        public string? TypeName { get; set; }
        public string? Namespace { get; set; }
        public bool? IsConsistent { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseProjectInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
    }

    public class ResponseConnect : ResponseMessage
    {
    }

    public class ResponseDisconnect : ResponseMessage
    {
    }

    public class ResponseState : ResponseMessage
    {
        public bool? IsConnected { get; set; }
        public string? Project { get; set; }
        public string? Session { get; set; }
    }

    public class ResponseGetProjects : ResponseMessage
    {
        public IEnumerable<ResponseProjectInfo>? Items { get; set; }
    }

    public class ResponseOpenProject : ResponseMessage
    {
    }

    public class ResponseSaveProject : ResponseMessage
    {
    }

    public class ResponseSaveAsProject : ResponseMessage
    {
    }

    public class ResponseCloseProject : ResponseMessage
    {
    }

    public class ResponseTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseProjectTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseSoftwareTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseDevices : ResponseMessage
    {
        public IEnumerable<ResponseDeviceInfo>? Items { get; set; }
    }
    
    /// <summary>Result of compiling a PLC software.</summary>
    public sealed class ResponseCompileSoftware : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="errorCount">Errors reported by the compiler.</param>
        /// <param name="warningCount">Warnings reported by the compiler.</param>
        /// <param name="messages">Every compiler message, one readable line each.</param>
        public ResponseCompileSoftware(int errorCount, int warningCount, IReadOnlyList<string> messages)
        {
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Messages = messages;
        }

        /// <summary>Errors reported by the compiler.</summary>
        public int ErrorCount { get; }

        /// <summary>Warnings reported by the compiler.</summary>
        public int WarningCount { get; }

        /// <summary>
        /// Every compiler message as <c>Severity: path — description</c>. This is what a
        /// generate, compile and fix loop reads to know what to change.
        /// </summary>
        public IReadOnlyList<string> Messages { get; }
    }
    
    public class ResponseBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseExportBlock : ResponseMessage
    {
    }

    public class ResponseImportBlock : ResponseMessage
    {
    }

    public class ResponseExportBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
        public IEnumerable<ResponseBlockInfo>? Inconsistent { get; set; }
    }

    public class ResponseTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
    }

    public class ResponseExportType : ResponseMessage
    {
    }

    public class ResponseImportType : ResponseMessage
    {
    }

    public class ResponseExportTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
        public IEnumerable<ResponseTypeInfo>? Inconsistent { get; set; }
    }

    public class ResponseExportAsDocuments : ResponseMessage
    {
    }

    public class ResponseExportBlocksAsDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseImportFromDocuments : ResponseMessage
    {
    }

    public class ResponseImportBlocksFromDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    // The response objects above are inherited from upstream and carry public setters.
    // New ones are immutable, as CLAUDE.md requires; the data is set through the constructor and
    // only the inherited Message and Meta remain settable.

    /// <summary>Result of retrieving a project archive.</summary>
    public sealed class ResponseRetrieveProject : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="projectPath">Full path of the retrieved project file.</param>
        public ResponseRetrieveProject(string projectPath)
        {
            ProjectPath = projectPath;
        }

        /// <summary>Full path of the retrieved project file, now open in TIA Portal.</summary>
        public string ProjectPath { get; }
    }

    /// <summary>The project's network layout.</summary>
    public sealed class ResponseNetworkTopology : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="nodes">One line per interface: device, interface, type, address, subnet.</param>
        public ResponseNetworkTopology(IReadOnlyList<string> nodes)
        {
            Nodes = nodes;
        }

        /// <summary>
        /// One line per interface, as <c>device | interface | type | address | subnet</c>. An empty
        /// subnet means the interface is wired to nothing, which is a common and otherwise silent
        /// reason a download or an IO connection fails.
        /// </summary>
        public IReadOnlyList<string> Nodes { get; }
    }

    /// <summary>One PLCSIM Advanced virtual controller.</summary>
    public sealed class ResponseSimulationInstance : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="name">The instance name.</param>
        /// <param name="operatingState">Off, Stop, Run and so on.</param>
        /// <param name="cpuType">The CPU being emulated.</param>
        /// <param name="ipAddresses">Addresses the controller answers on.</param>
        public ResponseSimulationInstance(
            string name,
            string operatingState,
            string cpuType,
            IReadOnlyList<string> ipAddresses)
        {
            Name = name;
            OperatingState = operatingState;
            CpuType = cpuType;
            IpAddresses = ipAddresses;
        }

        /// <summary>The instance name.</summary>
        public string Name { get; }

        /// <summary>
        /// Off, Stop, Run and so on. A controller with no program cannot reach Run: download first.
        /// </summary>
        public string OperatingState { get; }

        /// <summary>The CPU being emulated.</summary>
        public string CpuType { get; }

        /// <summary>
        /// Addresses the controller answers on. A new instance reports 0.0.0.0 until an address is
        /// set, and TIA Portal cannot download to it in that state.
        /// </summary>
        public IReadOnlyList<string> IpAddresses { get; }
    }

    /// <summary>The virtual controllers registered with the simulation runtime.</summary>
    public sealed class ResponseSimulationInstances : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One entry per registered instance.</param>
        /// <param name="networkMode">How the runtime is reachable.</param>
        public ResponseSimulationInstances(IReadOnlyList<ResponseSimulationInstance> items, string networkMode)
        {
            Items = items;
            NetworkMode = networkMode;
        }

        /// <summary>One entry per registered instance.</summary>
        public IReadOnlyList<ResponseSimulationInstance> Items { get; }

        /// <summary>
        /// Softbus, TCPIPSingleAdapter, TCPIPMultipleAdapter, or Unavailable. Reported because a
        /// download that cannot connect says nothing about why, and this is the first thing to check.
        /// </summary>
        public string NetworkMode { get; }
    }

    /// <summary>One entry of a virtual controller's tag list.</summary>
    public sealed class ResponseSimulationTag
    {
        /// <summary>Creates the entry.</summary>
        /// <param name="name">The name a read or a write must use.</param>
        /// <param name="area">Input, Output, Marker, Timer, Counter or DataBlock.</param>
        /// <param name="dataType">The declared PLC data type.</param>
        /// <param name="isReadable">Whether this server can read a value for it.</param>
        public ResponseSimulationTag(string name, string area, string dataType, bool isReadable)
        {
            Name = name;
            Area = area;
            DataType = dataType;
            IsReadable = isReadable;
        }

        /// <summary>
        /// The name a read or a write must use, spelled exactly as the controller reports it. Members
        /// of a data block are fully qualified and carry no quotes: <c>DB_Cell.Feeder.Step</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>Input, Output, Marker, Timer, Counter or DataBlock.</summary>
        public string Area { get; }

        /// <summary>The declared PLC data type, e.g. Bool, Int, DInt, Real.</summary>
        public string DataType { get; }

        /// <summary>
        /// Whether this server can read a value for it. False for a struct or an array: read their
        /// members instead, which are separate entries in the same list.
        /// </summary>
        public bool IsReadable { get; }
    }

    /// <summary>A page of a virtual controller's tag list.</summary>
    public sealed class ResponseSimulationTags : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">The tags returned, ordered by name.</param>
        /// <param name="matchCount">How many tags matched the filter, returned or not.</param>
        /// <param name="totalCount">How many tags the program has in total.</param>
        public ResponseSimulationTags(IReadOnlyList<ResponseSimulationTag> items, int matchCount, int totalCount)
        {
            Items = items;
            MatchCount = matchCount;
            TotalCount = totalCount;
        }

        /// <summary>The tags returned, ordered by name.</summary>
        public IReadOnlyList<ResponseSimulationTag> Items { get; }

        /// <summary>How many tags matched the filter, whether returned or not.</summary>
        public int MatchCount { get; }

        /// <summary>
        /// How many tags the program has in total. Zero means the controller holds no program at
        /// all: the tag list is read from the controller, so download before expecting names.
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// Whether matching tags were left out because of the limit. Reported so a filtered list
        /// that happens to be a page is not mistaken for the whole answer.
        /// </summary>
        public bool IsTruncated => Items.Count < MatchCount;
    }

    /// <summary>The value of one tag of a virtual controller.</summary>
    public sealed class ResponseSimulationTagValue : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="name">The tag the value belongs to.</param>
        /// <param name="dataType">The declared PLC data type it was read as.</param>
        /// <param name="value">The value, or null when nothing was read.</param>
        public ResponseSimulationTagValue(string name, string dataType, object? value)
        {
            Name = name;
            DataType = dataType;
            Value = value;
        }

        /// <summary>The tag the value belongs to.</summary>
        public string Name { get; }

        /// <summary>The declared PLC data type it was read as.</summary>
        public string DataType { get; }

        /// <summary>
        /// The value, as a bool or a number rather than as text, so it can be compared without
        /// being parsed first — except a WChar, which is a one-character string. Null when nothing
        /// was read: a write the guard refused reports no value rather than a plausible one,
        /// because a refused Bool write reporting <c>false</c> would read as the tag holding false.
        /// </summary>
        public object? Value { get; }
    }

    /// <summary>The values of several tags of a virtual controller.</summary>
    public sealed class ResponseSimulationTagValues : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One value per tag asked for, in the order asked for.</param>
        public ResponseSimulationTagValues(IReadOnlyList<ResponseSimulationTagValue> items)
        {
            Items = items;
        }

        /// <summary>
        /// One value per tag asked for, in the order asked for. They are read through one handle in
        /// one call, so they come from nearly the same moment — but the controller keeps scanning
        /// while they are read, so this is not a consistent snapshot of a scan.
        /// </summary>
        public IReadOnlyList<ResponseSimulationTagValue> Items { get; }
    }

    /// <summary>Result of writing SCL into a PLC program.</summary>
    public sealed class ResponseWriteScl : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="generatedBlocks">Names of the blocks the source produced.</param>
        public ResponseWriteScl(IReadOnlyList<string> generatedBlocks)
        {
            GeneratedBlocks = generatedBlocks;
        }

        /// <summary>
        /// Names of the blocks the source produced. Generating a block does not mean it compiles:
        /// call CompileSoftware to find that out.
        /// </summary>
        public IReadOnlyList<string> GeneratedBlocks { get; }
    }

    /// <summary>One long operation, as it stood when asked.</summary>
    public sealed class ResponseJob : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="jobId">Which job.</param>
        /// <param name="tool">The tool it runs.</param>
        /// <param name="target">What it runs against.</param>
        /// <param name="state">Queued, Running, Succeeded, Failed or Cancelled.</param>
        /// <param name="detail">Its result, or the reason it failed. Empty while it runs.</param>
        /// <param name="isCancellable">Whether cancelling it would do anything.</param>
        public ResponseJob(string jobId, string tool, string target, string state, string detail, bool isCancellable)
        {
            JobId = jobId;
            Tool = tool;
            Target = target;
            State = state;
            Detail = detail;
            IsCancellable = isCancellable;
        }

        /// <summary>Which job. Poll with this.</summary>
        public string JobId { get; }

        /// <summary>The tool it runs.</summary>
        public string Tool { get; }

        /// <summary>What it runs against.</summary>
        public string Target { get; }

        /// <summary>Queued, Running, Succeeded, Failed or Cancelled.</summary>
        public string State { get; }

        /// <summary>Its result, or the reason it failed. Empty while it runs.</summary>
        public string Detail { get; }

        /// <summary>
        /// Whether cancelling it would do anything. True only while queued: Openness cannot
        /// interrupt a compile or a download that has started.
        /// </summary>
        public bool IsCancellable { get; }
    }

    /// <summary>Every long operation this session has run.</summary>
    public sealed class ResponseJobs : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One entry per job, newest first.</param>
        public ResponseJobs(IReadOnlyList<ResponseJob> items)
        {
            Items = items;
        }

        /// <summary>One entry per job, newest first.</summary>
        public IReadOnlyList<ResponseJob> Items { get; }
    }

    /// <summary>SCL generated for a cell, not yet written anywhere.</summary>
    public sealed class ResponseCellScl : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="cellName">The cell the source describes.</param>
        /// <param name="stationNames">Its stations, in the order a piece visits them.</param>
        /// <param name="scl">The SCL source, station pattern first.</param>
        public ResponseCellScl(string cellName, IReadOnlyList<string> stationNames, string scl)
        {
            CellName = cellName;
            StationNames = stationNames;
            Scl = scl;
        }

        /// <summary>The cell the source describes.</summary>
        public string CellName { get; }

        /// <summary>Its stations, in the order a piece visits them.</summary>
        public IReadOnlyList<string> StationNames { get; }

        /// <summary>The SCL source, station pattern first and coordinator second.</summary>
        /// <remarks>
        /// The order is not cosmetic: the coordinator declares instances of the station, so the
        /// station type has to exist before it. One source rather than two because <c>WriteScl</c>
        /// generates every block a source declares, in the order it reads them.
        /// </remarks>
        public string Scl { get; }
    }

    /// <summary>One backup the registry holds.</summary>
    public sealed class ResponseBackup
    {
        /// <summary>Describes one backup.</summary>
        /// <param name="path">The directory holding it.</param>
        /// <param name="tool">The tool it was taken for.</param>
        /// <param name="target">What that tool was about to write to.</param>
        /// <param name="takenAt">When it was taken, in round-trip UTC.</param>
        /// <param name="fileCount">How many files it holds.</param>
        public ResponseBackup(string path, string tool, string target, string takenAt, int fileCount)
        {
            Path = path;
            Tool = tool;
            Target = target;
            TakenAt = takenAt;
            FileCount = fileCount;
        }

        /// <summary>The directory holding the backup.</summary>
        public string Path { get; }

        /// <summary>The tool it was taken for.</summary>
        public string Tool { get; }

        /// <summary>What that tool was about to write to.</summary>
        public string Target { get; }

        /// <summary>When it was taken, in round-trip UTC.</summary>
        public string TakenAt { get; }

        /// <summary>
        /// How many files it holds. Zero means the change was refused or failed before exporting,
        /// so there is nothing here to restore from.
        /// </summary>
        public int FileCount { get; }
    }

    /// <summary>Everything the backup registry holds.</summary>
    public sealed class ResponseBackups : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One entry per backup, newest first.</param>
        public ResponseBackups(IReadOnlyList<ResponseBackup> items)
        {
            Items = items;
        }

        /// <summary>One entry per backup, newest first.</summary>
        public IReadOnlyList<ResponseBackup> Items { get; }
    }

    /// <summary>Result of exporting a PLC program to text.</summary>
    public sealed class ResponseExportSnapshot : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="exported">Files written, relative to the snapshot root.</param>
        /// <param name="inconsistent">Items skipped because TIA Portal reports them inconsistent.</param>
        /// <param name="unsupported">Blocks whose programming language has no text representation.</param>
        /// <param name="failed">Items that should have exported but did not, each with its reason.</param>
        public ResponseExportSnapshot(
            IReadOnlyList<string> exported,
            IReadOnlyList<string> inconsistent,
            IReadOnlyList<string> unsupported,
            IReadOnlyList<string> failed)
        {
            Exported = exported;
            Inconsistent = inconsistent;
            Unsupported = unsupported;
            Failed = failed;
        }

        /// <summary>Files written, relative to the snapshot root, using forward slashes.</summary>
        public IReadOnlyList<string> Exported { get; }

        /// <summary>Items skipped as inconsistent. Compile the software and export again.</summary>
        public IReadOnlyList<string> Inconsistent { get; }

        /// <summary>
        /// Blocks that cannot appear in a text snapshot at all, because LAD, FBD and GRAPH exist
        /// only as SimaticML XML. A non-empty list means the snapshot does not describe the whole
        /// program, so it is not a backup.
        /// </summary>
        public IReadOnlyList<string> Unsupported { get; }

        /// <summary>Items that should have exported but did not, each with its reason.</summary>
        public IReadOnlyList<string> Failed { get; }
    }
}
