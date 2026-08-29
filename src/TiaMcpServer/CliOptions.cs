namespace TiaMcpServer
{
    public class CliOptions
    {
        /// <summary>Where the write policy lives when none is given.</summary>
        /// <remarks>
        /// Beside the project rather than beside the binary: what a session may touch is a property
        /// of the project, not of the machine or the person running it.
        /// </remarks>
        public const string DefaultPolicyPath = ".tia-mcp/policy.json";

        /// <summary>Where the audit trail is written when none is given.</summary>
        public const string DefaultAuditPath = ".tia-mcp/audit.jsonl";

        /// <summary>Where the previous state of anything overwritten is kept when none is given.</summary>
        /// <remarks>
        /// Beside the project for the same reason as the policy: a backup of this project's blocks
        /// belongs with this project, not in a directory shared with every other one.
        /// </remarks>
        public const string DefaultBackupRoot = ".tia-mcp/backups";

        /// <summary>Where the documentation index lives when none is given.</summary>
        /// <remarks>
        /// The path the harness writes its index to. A machine that has never built one simply has
        /// no file here, and every change plan then says the hardware context is unavailable rather
        /// than pretending there is nothing to cite.
        /// </remarks>
        public const string DefaultKnowledgeIndexPath = ".tia-mcp/harness/knowledge.db";

        /// <summary>The program that searches the documentation index, when none is given.</summary>
        /// <remarks>
        /// The index and its ranking are implemented once, in the harness, and this server runs that
        /// program rather than reimplementing BM25 in C#. Relative to the working directory for the
        /// same reason as the policy: it belongs to this checkout, not to the machine.
        /// </remarks>
        public const string DefaultKnowledgeLookupPath = "harness/src/knowledge/hardwareLookup.ts";

        public int? TiaMajorVersion { get; set; }
        public int? Logging { get; set; } // "stdio" or "http"

        /// <summary>Path to the write policy. A missing file denies every write.</summary>
        public string? PolicyPath { get; set; }

        /// <summary>Path to the append-only audit trail.</summary>
        public string? AuditPath { get; set; }

        /// <summary>Root directory every backup is written under.</summary>
        public string? BackupRoot { get; set; }

        /// <summary>Path to the documentation index. A missing file cites nothing and says so.</summary>
        public string? KnowledgeIndexPath { get; set; }

        /// <summary>Path to the program that searches the documentation index.</summary>
        public string? KnowledgeLookupPath { get; set; }

        public static CliOptions ParseArgs(string[] args)
        {
            var options = new CliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-tia-major-version":
                    case "--tia-major-version":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int v))
                        {
                            options.TiaMajorVersion = v;
                            i++;
                        }
                        break;

                    case "-logging":
                    case "--logging":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int l))
                        {
                            options.Logging = l;
                            i++;
                        }
                        break;

                    case "-policy":
                    case "--policy":
                        if (i + 1 < args.Length)
                        {
                            options.PolicyPath = args[i + 1];
                            i++;
                        }
                        break;

                    case "-audit":
                    case "--audit":
                        if (i + 1 < args.Length)
                        {
                            options.AuditPath = args[i + 1];
                            i++;
                        }
                        break;

                    case "-backups":
                    case "--backups":
                        if (i + 1 < args.Length)
                        {
                            options.BackupRoot = args[i + 1];
                            i++;
                        }
                        break;

                    case "-knowledge-index":
                    case "--knowledge-index":
                        if (i + 1 < args.Length)
                        {
                            options.KnowledgeIndexPath = args[i + 1];
                            i++;
                        }
                        break;

                    case "-knowledge-lookup":
                    case "--knowledge-lookup":
                        if (i + 1 < args.Length)
                        {
                            options.KnowledgeLookupPath = args[i + 1];
                            i++;
                        }
                        break;
                }
            }
            return options;
        }
    }
}
