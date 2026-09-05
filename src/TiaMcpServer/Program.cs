using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Governance;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer
{
    public class Program
    {
        /// <summary>
        /// How long a documentation lookup may take before the change proceeds without citations.
        /// </summary>
        /// <remarks>
        /// Generous enough for a cold Node start on a machine that is also running TIA Portal, and
        /// bounded because the lookup sits in front of a write: a lookup that hung would turn
        /// enrichment into a stall. When it expires the plan says so and the write goes ahead — the
        /// citations inform the change, they do not gate it.
        /// </remarks>
        private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(15);


        /// <summary>Starts the server.</summary>
        /// <param name="args">Command line arguments. See <see cref="CliOptions"/>.</param>
        /// <remarks>
        /// **MTA is declared, not inherited.** A console entry point is MTA unless marked otherwise,
        /// so this used to be true by accident, and the accident was load-bearing: measured on
        /// 2026-08-18, Openness objects created in an STA apartment are thread-affine and every call
        /// from another thread throws <c>Cross-thread operation is not valid in Openness within STA</c>.
        /// Asynchronous jobs and the document import both hand Openness work to a worker thread, so an
        /// STA entry point would break them. Saying it out loud means nobody removes it by adding an
        /// attribute for an unrelated reason.
        ///
        /// MTA does **not** serialise anything, which is what <see cref="Siemens.OpennessGate"/> is for.
        /// </remarks>
        [MTAThread]
        public static async Task Main(string[] args)
        {
            RequireMultiThreadedApartment();

            var options = CliOptions.ParseArgs(args);

            Engineering.TiaMajorVersion = options.TiaMajorVersion ?? 20;

            if (Engineering.TiaMajorVersion < 20)
            {
                AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;
            }
            else
            {
                Openness.Initialize(Engineering.TiaMajorVersion);
            }

            // Ensure user is in user group 'Siemens TIA Openness'
            if (await Openness.IsUserInGroup())
            {
                await RunStdioHost(options);
            }
            else
            {
                RefuseWithoutGroupMembership();
            }
        }

        /// <summary>Says why the server will not start, on the only channel that may carry prose.</summary>
        /// <remarks>
        /// **stderr, not stdout.** With the stdio transport, stdout *is* the JSON-RPC channel, so a
        /// plain sentence written there is not a message to the user — it is a malformed frame, and
        /// the host reports a protocol error instead of the reason. This is the first thing a new
        /// installation hits, because Windows only grants the group's token at the next sign-in, so
        /// it was also the one message most likely to be destroyed by the way it was sent.
        ///
        /// The exit code is set too: a host that starts this server can tell that it refused rather
        /// than that it vanished.
        /// </remarks>
        private static void RefuseWithoutGroupMembership()
        {
            Console.Error.WriteLine(
                "This account is not in the Windows group 'Siemens TIA Openness', which Openness " +
                "requires. Add it with: net localgroup \"Siemens TIA Openness\" \"%USERNAME%\" /add " +
                "(as an administrator), then sign out of Windows and back in — the group is only " +
                "granted to a new sign-in, so this will keep failing until you do.");

            Environment.ExitCode = 1;
        }

        public static async Task RunStdioHost(CliOptions? options)
        {
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            if (builder != null)
            {
                if (options != null && options.Logging != null)
                {
                    switch (options.Logging)
                    {
                        case 1:
                            // ATTENTION: For STDIO, logs must go to stderr!
                            builder.Logging.AddConsole(options =>
                            {
                                options.LogToStandardErrorThreshold = LogLevel.Trace;
                            });
                            break;

                        case 2:
                            // Visual Studio Debug Output / Sysinternals.DebugView
                            builder.Logging.AddDebug();
                            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
                            builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Information);
                            builder.Logging.AddFilter("TiaMcpServer", LogLevel.Debug);

                            // Log Level for Debug Output
                            builder.Logging.SetMinimumLevel(LogLevel.Debug);
                            break;

                        case 3:
                            // Windows Event Log
                            builder.Logging.AddEventLog();
                            break;

                        default:
                            // no logging
                            break;
                    }
                }

                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly()
                    .WithPromptsFromAssembly();

                // Register the Portal service for dependency injection
                builder.Services.AddSingleton<Portal>();

                // Singleton on purpose: a PLCSIM Advanced controller stays registered only while a
                // handle to it is open, so a per-call runtime would unregister every controller it
                // created as soon as the tool returned.
                builder.Services.AddSingleton<SimulationRuntime>();

                RegisterGovernance(builder.Services, options);

                var host = builder.Build();

                // Set the service provider for the MCP server, to retrieve Portal with injected logger
                McpServer.SetServiceProvider(host.Services);

                // Set the logger for the MCP server
                McpServer.Logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpServer");

                // log a bit of information about the server start
                if (options != null && options.Logging != null && options.Logging > 0)
                {
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();

                    logger.LogInformation($"=== TIA Portal MCP Server '{DateTime.Now.ToShortTimeString()}' ===");

                    switch (options.Logging)
                    {
                        case 1:
                            logger.LogInformation("Logging to stderr");
                            break;
                        case 2:
                            logger.LogInformation("Logging to debug output");
                            break;
                        case 3:
                            logger.LogInformation("Logging to Windows event log");
                            break;
                    }
                }

                await host.RunAsync();
            }

        }

        /// <summary>Refuses to start in a single-threaded apartment.</summary>
        /// <exception cref="PortalException">The entry point is not MTA.</exception>
        /// <remarks>
        /// Belt as well as braces. <c>[MTAThread]</c> declares the intent, and this catches the case
        /// where a host starts the server some other way — a different entry point, a test harness,
        /// a future HTTP transport. Failing at startup with a reason beats failing later with
        /// <c>Cross-thread operation is not valid in Openness within STA</c> from inside a job, which
        /// says nothing about the cause.
        /// </remarks>
        private static void RequireMultiThreadedApartment()
        {
            var apartment = Thread.CurrentThread.GetApartmentState();

            if (apartment == ApartmentState.MTA)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"The server must start in a multi-threaded apartment; this thread is {apartment}. "
                + "Openness objects created in an STA are bound to the creating thread, so every "
                + "background operation would fail. See the remarks on Main.");
        }

        /// <summary>
        /// Registers the governance layer, in Study Mode.
        /// </summary>
        /// <param name="services">Where to register.</param>
        /// <param name="options">Command line options, for the policy and audit paths.</param>
        /// <remarks>
        /// The session always starts in Study Mode, and this build cannot leave it: Workshop Mode
        /// is compiled out unless WorkshopMode=true, which is not how the shipped binary is built.
        ///
        /// Everything here is a singleton and that is load-bearing. Plans waiting for confirmation
        /// live in the store, so a per-call store would lose them between proposing a change and
        /// confirming it — the same defect the simulation runtime had.
        /// </remarks>
        public static void RegisterGovernance(IServiceCollection services, CliOptions? options)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var policyPath = options?.PolicyPath ?? CliOptions.DefaultPolicyPath;
            var auditPath = options?.AuditPath ?? CliOptions.DefaultAuditPath;
            var backupRoot = options?.BackupRoot ?? CliOptions.DefaultBackupRoot;
            var knowledgeIndex = options?.KnowledgeIndexPath ?? CliOptions.DefaultKnowledgeIndexPath;
            var knowledgeLookup = options?.KnowledgeLookupPath ?? CliOptions.DefaultKnowledgeLookupPath;

            services.AddSingleton<ISystemClock, SystemClock>();
            services.AddSingleton<IModeGate>(_ => ModeGate.ForStudy());

            // A missing policy denies every write. That is deliberate: see WritePolicyFile.
            services.AddSingleton<IWritePolicy>(_ => WritePolicy.Load(policyPath));
            services.AddSingleton<IAuditTrail>(_ => new JsonlAuditTrail(auditPath));
            services.AddSingleton<IBackupRegistry>(provider =>
                new BackupRegistry(backupRoot, provider.GetRequiredService<ISystemClock>()));
            services.AddSingleton<ChangePlanStore>();

            // A machine with no index is not a special case that needs its own registration: the
            // lookup reports the missing file as an unavailable context, and every plan says so.
            services.AddSingleton<Knowledge.IHardwareLookup>(_ => new Knowledge.HarnessHardwareLookup(
                knowledgeLookup,
                knowledgeIndex,
                LookupTimeout));

            services.AddSingleton<GuardedWrite>();

            // Long operations, so a compile or a download does not block the caller. A singleton
            // because a job outlives the call that started it: the caller comes back for it later.
            services.AddSingleton<Jobs.IJobDispatcher, Jobs.ThreadPoolJobDispatcher>();
            services.AddSingleton<Jobs.IJobStore>(provider => new Jobs.JobStore(
                provider.GetRequiredService<ISystemClock>(),
                provider.GetRequiredService<Jobs.IJobDispatcher>()));
        }
    }
}
