using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseMessage
    {
        public string? Message { get; set; }
        public JsonObject? Meta { get; set; }
    }

    public class ResponseAttributes : ResponseMessage
    {
        public IEnumerable<Attribute>? Attributes { get; set; }
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
        //public string? Path { get; set; }
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
        public BlockGroupInfo? Root { get; set; }
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
    
    public class ResponseCompileSoftware : ResponseMessage
    {
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
