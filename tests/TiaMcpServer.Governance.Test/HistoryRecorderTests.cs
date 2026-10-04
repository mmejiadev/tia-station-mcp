using System;
using System.Collections.Generic;
using TiaMcpServer.History;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class HistoryRecorderTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void RecordCompilation_Recorded_ReturnsNothingToSay()
        {
            var recorder = new HistoryRecorder(new RecordingJournal<CompilationRecord>(), new RecordingJournal<ProjectRecord>(), new FixedClock(Now));

            var note = recorder.RecordCompilation("Cell.ap20", "PLC_0", AReport());

            Assert.AreEqual(string.Empty, note);
        }

        [TestMethod]
        public void RecordCompilation_Recorded_StampsItWithTheClock()
        {
            var compilations = new RecordingJournal<CompilationRecord>();
            var recorder = new HistoryRecorder(compilations, new RecordingJournal<ProjectRecord>(), new FixedClock(Now));

            recorder.RecordCompilation("Cell.ap20", "PLC_0", AReport());

            Assert.AreEqual(Now, compilations.Records[0].Timestamp);
        }

        /// <remarks>
        /// The compilation already happened. Throwing would report it as failed and invite a retry
        /// that changes nothing, so the failure comes back as a sentence for the tool's answer.
        /// </remarks>
        [TestMethod]
        public void RecordCompilation_TheJournalFails_SaysSoInsteadOfThrowing()
        {
            var recorder = new HistoryRecorder(new FailingJournal<CompilationRecord>(), new RecordingJournal<ProjectRecord>(), new FixedClock(Now));

            var note = recorder.RecordCompilation("Cell.ap20", "PLC_0", AReport());

            StringAssert.Contains(note, "disk full", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RecordProject_TheJournalFails_SaysSoInsteadOfThrowing()
        {
            var recorder = new HistoryRecorder(new RecordingJournal<CompilationRecord>(), new FailingJournal<ProjectRecord>(), new FixedClock(Now));

            var note = recorder.RecordProject(ProjectEvent.Opened, AProject());

            StringAssert.Contains(note, "project", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RecordProject_Recorded_KeepsWhoMadeIt()
        {
            var projects = new RecordingJournal<ProjectRecord>();
            var recorder = new HistoryRecorder(new RecordingJournal<CompilationRecord>(), projects, new FixedClock(Now));

            recorder.RecordProject(ProjectEvent.Retrieved, AProject());

            Assert.AreEqual("mamem", projects.Records[0].Author);
            Assert.AreEqual(ProjectEvent.Retrieved, projects.Records[0].Event);
        }

        private static CompilationReport AReport()
        {
            return new CompilationReport(CompilationSeverity.Success, 0, 0, Array.Empty<CompilationMessage>());
        }

        private static ProjectSummary AProject()
        {
            return new ProjectSummary(@"C:\Projects\Cell\Cell.ap20", "Cell", "mamem", Now, Now, "mamem");
        }

        private sealed class RecordingJournal<TRecord> : IJournal<TRecord>
        {
            public List<TRecord> Records { get; } = new List<TRecord>();

            public void Append(TRecord record)
            {
                Records.Add(record);
            }
        }

        private sealed class FailingJournal<TRecord> : IJournal<TRecord>
        {
            public void Append(TRecord record)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "disk full");
            }
        }

        private sealed class FixedClock : ISystemClock
        {
            public FixedClock(DateTimeOffset now)
            {
                UtcNow = now;
            }

            public DateTimeOffset UtcNow { get; }
        }
    }
}
