using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The generated cell runs on a virtual controller, and a piece moves through it.
    /// </summary>
    /// <remarks>
    /// <see cref="Test19CellPattern"/> says the SCL compiles. Compiling is not running: the
    /// handshake between one station and the next was asserted nowhere but in the compiler, and a
    /// coordinator that hands a piece to the wrong station compiles perfectly. This is the test
    /// that watches it happen.
    ///
    /// It observes the piece two ways, in two tests, because no single run can do both. In
    /// **automatic** mode the cell finishes in tens of scans — milliseconds — so an intermediate
    /// state is not something a test can reliably catch; what it can assert is that a numbered
    /// piece left the line. In **manual** mode a station advances one step per rising edge of
    /// Start, and the coordinator holds Start high for as long as the piece is the station's, so
    /// exactly one edge arrives and the piece stops inside the first station where it can be read
    /// at leisure. That is a deterministic observation of "the piece is at station 1 and station 2
    /// is still empty".
    ///
    /// Both halves matter: holding is what makes traceability possible, and completing is what
    /// makes the cell a cell. Neither test changes mode while a piece is in the cell — see the
    /// remarks on the automatic one for why that is not a test detail but a property of the
    /// pattern.
    ///
    /// A third test covers the coordinator's one refusal: a piece with no number is not admitted.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test20CellRuns : IDisposable
    {
        // From the project: PLC_0's PROFINET interface is at this address, and a controller a
        // download can find has to be at the same one.
        private const string ControllerAddress = "192.168.0.1";
        private const string ControllerSubnetMask = "255.255.255.0";
        private const string CellFile = "two-station-demo.json";
        private const string CoordinatorBlock = "FB_TwoStationDemo";
        private const string InstanceDataBlock = "DB_TwoStationDemo";
        private const int PieceId = 17;
        private const int StepWorking = 10;
        private const int StepFault = 90;
        private const int ObservationTimeoutMilliseconds = 20000;
        private const int PollIntervalMilliseconds = 50;

        private static string _repositoryRoot = string.Empty;

        private SimulationRuntime _runtime = new SimulationRuntime();
        private string _instanceName = string.Empty;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _repositoryRoot = FindRepositoryRoot();
        }

        [TestInitialize]
        public void TestInit()
        {
            // No runtime is constructed here: MSTest builds one instance of this class per test
            // method, so the field initialiser has already made a fresh one. Assigning a second
            // would drop the first undisposed.
            _instanceName = "TiaMcpCell_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // In this process and before any instance exists. Setting it from a separate script
            // reads back as applied and then has no effect here.
            SimulationRuntime.UseTcpIpNetworkMode();

            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        /// <summary>Releases the controller handle this test held.</summary>
        /// <remarks>
        /// A held handle is what keeps a virtual controller registered, so one left behind leaves a
        /// controller running after the suite. MSTest builds one instance of the class per test
        /// method, so this runs per test.
        /// </remarks>
        public void Dispose()
        {
            _runtime.Dispose();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Close the project before removing the controller, so TIA Portal is not left holding a
            // connection to something disappearing underneath it.
            AssemblyHooks.SharedPortal.CloseProject();

            try
            {
                _runtime.DeleteInstance(_instanceName);
            }
            catch (PortalException exception)
            {
                // Reported, not swallowed. The instance may never have been created, so this must
                // not fail the test and hide the real failure — but a controller that genuinely
                // refused to go away stays registered after the run, and that is the zombie-handle
                // failure this class's remarks warn about. Silence would leave it invisible.
                Console.Error.WriteLine($"Cleanup of '{_instanceName}' did not remove it: {exception.Message}");
            }
        }

        [TestMethod]
        public void TwoStationDemo_StartedInManualMode_HoldsThePieceInTheFirstStation()
        {
            DownloadTheCellAndRunIt();

            var tags = ResolveCellTags();

            // Manual mode, so the piece stops where it can be seen: the station advances one step
            // per rising edge of Start, and the coordinator holds Start high for as long as the
            // piece is the station's, so exactly one edge ever arrives.
            //
            // Enable before the mode and the mode before the start. A station admitted with no
            // mode selected faults, and choosing one afterwards does not clear the fault.
            WriteTag(tags, "Enable", "true");
            WriteTag(tags, "ModeManual", "true");
            WriteTag(tags, "ModeAuto", "false");
            WriteTag(tags, "NextPieceId", PieceId.ToString());
            WriteTag(tags, "CellStart", "true");

            WaitUntil(
                () => ReadInteger(tags, "Feeder.PieceId") == PieceId,
                () => DescribeTheCell(tags),
                $"piece {PieceId} reaches the first station");

            Assert.AreEqual(StepWorking, ReadInteger(tags, "Feeder.Step"), DescribeTheCell(tags));

            // And the second station is untouched. A coordinator that handed the piece straight on
            // would show this as anything but zero, and the cell would still have "worked".
            Assert.AreEqual(0, ReadInteger(tags, "Driller.PieceId"), DescribeTheCell(tags));
            Assert.AreEqual(0, ReadInteger(tags, "BlockedAtStation"), DescribeTheCell(tags));
        }

        [TestMethod]
        public void TwoStationDemo_StartedInAutomaticMode_MovesAPieceThroughBothStations()
        {
            // Automatic from the start, and never switched. The first version of this test held the
            // piece in manual mode and then changed to automatic to watch it finish, and the second
            // station faulted every time: a mode change is two tag writes with the controller
            // scanning between them, so for a moment ModeAuto and ModeManual hold the same value,
            // which FB_Station treats as a wiring fault. Dropping CellStart does not help — that
            // gates admission at the first station, and a handover to the second is not gated at
            // all. So the cell is never asked to change mode while a piece is in it, which is also
            // the honest thing to assert: one concept per test.
            DownloadTheCellAndRunIt();

            var tags = ResolveCellTags();

            WriteTag(tags, "Enable", "true");
            WriteTag(tags, "ModeAuto", "true");
            WriteTag(tags, "ModeManual", "false");
            WriteTag(tags, "NextPieceId", PieceId.ToString());
            WriteTag(tags, "CellStart", "true");

            // The piece has to have been in the second station to leave from it, and
            // CompletedPieceId is written from that station's PieceId. So this one assertion is the
            // whole traversal: no other path through the coordinator can put 17 there.
            WaitUntil(
                () => ReadInteger(tags, "CompletedPieceId") == PieceId,
                () => DescribeTheCell(tags),
                $"piece {PieceId} leaves the line");

            // Neither station faulted on the way. A cell that reported a completed piece while a
            // station sat at step 90 would have finished the traversal by accident.
            Assert.AreNotEqual(StepFault, ReadInteger(tags, "Feeder.Step"), DescribeTheCell(tags));
            Assert.AreNotEqual(StepFault, ReadInteger(tags, "Driller.Step"), DescribeTheCell(tags));
        }

        [TestMethod]
        public void TwoStationDemo_StartedWithNoPieceNumber_RefusesToAdmitThePiece()
        {
            // The coordinator's one refusal, and the reason it exists: a piece with no number
            // cannot be traced, which is the only thing this cell is for. It reports where it is
            // blocked rather than admitting the piece as piece zero.
            DownloadTheCellAndRunIt();

            var tags = ResolveCellTags();

            WriteTag(tags, "Enable", "true");
            WriteTag(tags, "ModeAuto", "true");
            WriteTag(tags, "NextPieceId", "0");
            WriteTag(tags, "CellStart", "true");

            WaitUntil(
                () => ReadInteger(tags, "BlockedAtStation") == 1,
                () => $"BlockedAtStation={ReadInteger(tags, "BlockedAtStation")}, Feeder.Step={ReadInteger(tags, "Feeder.Step")}",
                "the cell reports it is blocked at station 1");

            Assert.AreEqual(0, ReadInteger(tags, "Feeder.PieceId"), "a piece with no number was admitted anyway");
        }

        /// <summary>Writes the cell into the project, compiles it, downloads it and starts the CPU.</summary>
        private void DownloadTheCellAndRunIt()
        {
            _runtime.CreateInstance(_instanceName, "CPU1511");

            var addressed = _runtime.SetInstanceAddress(_instanceName, ControllerAddress, ControllerSubnetMask);
            CollectionAssert.Contains(addressed.IpAddresses.ToList(), ControllerAddress);

            var expanded = McpServer.ExpandCellScl(
                Path.Combine(_repositoryRoot, "spec", "cells", CellFile),
                Path.Combine(_repositoryRoot, "spec", "patterns"),
                includeEntryPoint: true);

            var written = McpServer.WriteScl(Settings.Project1PlcSoftwarePath0, expanded.Scl);

            // By name rather than by count. Main is the one that matters here: without it the
            // coordinator is a block nothing calls, and every assertion below would time out
            // waiting for a cell that is downloaded and never executed.
            CollectionAssert.Contains(written.GeneratedBlocks.ToList(), CoordinatorBlock, written.Message);
            CollectionAssert.Contains(written.GeneratedBlocks.ToList(), "Main", written.Message);

            CompileAndDownload();

            var running = _runtime.StartInstance(_instanceName);
            StringAssert.Contains(running.OperatingState, "Run", "the controller did not reach RUN after the download");
        }

        private static void CompileAndDownload()
        {
            // Before compiling, not after: the flag governs compilation, so blocks built without it
            // stay unsimulatable however many times they are downloaded.
            AssemblyHooks.SharedPortal.EnableSimulationSupport();

            var software = AssemblyHooks.SharedPortal.CompileSoftware(Settings.Project1PlcSoftwarePath0);
            Assert.IsTrue(software.IsSuccessful, $"the cell does not compile:\n{string.Join("\n", software.Errors)}");

            // Hardware too, and after enabling simulation support: that setting invalidates the
            // compiled hardware configuration, and downloading a stale one fails with an error that
            // blames the target rather than the project.
            var hardware = AssemblyHooks.SharedPortal.CompileHardware(Settings.Project1PlcSoftwarePath0);
            Assert.IsTrue(hardware.IsSuccessful, $"the hardware does not compile:\n{string.Join("\n", hardware.Errors)}");

            var download = AssemblyHooks.SharedPortal.DownloadToSimulation(Settings.Project1PlcSoftwarePath0);
            Assert.IsTrue(
                download.IsSuccessful,
                $"download failed (network mode {SimulationRuntime.NetworkMode}):\n" +
                string.Join("\n", download.Messages.Select(message => $"  [{message.Severity}] {message.Path} — {message.Description}")));
        }

        /// <summary>
        /// Builds the names this test drives and reads, and checks the controller really has them.
        /// </summary>
        /// <remarks>
        /// The names are written down — they have to be, since the test drives specific signals —
        /// but they are checked against the tag list before anything is written, and that is the
        /// point. Without the check, a name spelled the way SCL spells it rather than the way
        /// PLCSIM reports it fails later as "no such tag" on whichever write happens to run first,
        /// which reads as the program being wrong rather than the test. Here it fails immediately,
        /// naming the tag it wanted and listing what the controller actually has.
        /// </remarks>
        private Dictionary<string, string> ResolveCellTags()
        {
            var listed = McpServer.ListSimulationTags(_instanceName, "TwoStationDemo", limit: 500);

            Assert.AreNotEqual(
                0,
                listed.Items.Count,
                $"the controller reports no tag containing 'TwoStationDemo' among {listed.TotalCount}. " +
                "The cell's instance data block is not in the program that was downloaded.");

            // Otherwise a tag that exists but fell off the page would be reported below as a tag
            // the controller does not have, which is the misdiagnosis IsTruncated was added for.
            Assert.IsFalse(
                listed.IsTruncated,
                $"only {listed.Items.Count} of {listed.MatchCount} matching tags were returned; raise the limit");

            var wanted = new[]
            {
                "CellStart", "Enable", "ModeAuto", "ModeManual", "NextPieceId",
                "BlockedAtStation", "CompletedPieceId",
                "Feeder.Step", "Feeder.PieceId", "Feeder.Done",
                "Driller.Step", "Driller.PieceId"
            };

            return wanted.ToDictionary(member => member, member => ResolveOne(listed, member), StringComparer.Ordinal);
        }

        /// <summary>
        /// Finds one member of the cell's instance data block by its full name.
        /// </summary>
        /// <remarks>
        /// By full name and not by suffix, which is how the first run of this test failed: every
        /// station has its own Enable, so '.Enable' matched three tags — the cell's input and one
        /// per station. The cell's Enable is the one the coordinator passes down, and confusing it
        /// with a station's would have driven one station and left the other idle.
        /// </remarks>
        private static string ResolveOne(ResponseSimulationTags listed, string member)
        {
            var name = InstanceDataBlock + "." + member;

            Assert.IsTrue(
                listed.Items.Any(tag => string.Equals(tag.Name, name, StringComparison.Ordinal)),
                $"the controller has no tag named '{name}'. It reports: " +
                string.Join(", ", listed.Items.Select(tag => tag.Name).Take(40)));

            return name;
        }

        private void WriteTag(Dictionary<string, string> tags, string suffix, string value)
        {
            var response = McpServer.WriteSimulationTag(_instanceName, tags[suffix], value);

            // The guard is in this path: the suite's policy allows simulation/*, so an allowed
            // write reports success. A refusal arrives as a response rather than an exception, so
            // without this assertion the test would go on to time out waiting for a cell nothing
            // had ever started.
            Assert.IsTrue(
                response.Meta?["success"]?.GetValue<bool>() ?? false,
                $"writing {value} to {tags[suffix]} was not applied: {response.Message}");
        }

        private int ReadInteger(Dictionary<string, string> tags, string suffix)
        {
            var values = McpServer.ReadSimulationTags(_instanceName, new[] { tags[suffix] });

            return Convert.ToInt32(values.Items[0].Value);
        }

        /// <summary>Waits for a condition, reporting the cell's state if it never holds.</summary>
        private static void WaitUntil(Func<bool> condition, Func<string> describe, string expectation)
        {
            var clock = Stopwatch.StartNew();

            while (clock.ElapsedMilliseconds < ObservationTimeoutMilliseconds)
            {
                if (condition())
                {
                    return;
                }

                Thread.Sleep(PollIntervalMilliseconds);
            }

            Assert.Fail($"Timed out after {ObservationTimeoutMilliseconds} ms waiting for {expectation}. {describe()}");
        }

        /// <summary>
        /// The cell's state in one line, for a failure message.
        /// </summary>
        /// <remarks>
        /// Written out in full because a timeout says only that something did not happen, and the
        /// six numbers below say which. A piece stuck with Feeder.Step 90 is a faulted station; the
        /// same timeout with every number at zero is a program that is not running at all.
        /// </remarks>
        private string DescribeTheCell(Dictionary<string, string> tags)
        {
            var names = new[]
            {
                "Feeder.Step", "Feeder.PieceId", "Feeder.Done",
                "Driller.Step", "Driller.PieceId", "BlockedAtStation", "CompletedPieceId"
            };

            var values = McpServer.ReadSimulationTags(_instanceName, names.Select(name => tags[name]).ToArray());

            return "Cell: " + string.Join(", ", names.Zip(values.Items, (name, value) => $"{name}={value.Value}"));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "spec")))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory, "could not find the repository root, so spec/ cannot be read");

            return directory!.FullName;
        }
    }
}
