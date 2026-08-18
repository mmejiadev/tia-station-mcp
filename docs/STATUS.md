# Project status

> Living document. Update it at the end of every working session.
> Last updated: **2026-08-18**

## ▶ RESUME HERE

**2026-08-18 — phase 1 is done. Every deliverable it promised exists, and the calendar is gone.**

The day began with the governance layer written but never run against TIA Portal, and ended with
phase 1 actually finished rather than out of time. Three deliverables the roadmap had named were
missing and nobody had noticed, because the calendar said the phase was over. **That calendar has
been deleted** — see "Order, not dates" in `ROADMAP.md`. A phase now ends when what it promised
exists and is tested.

- **TIA suite: 127 cases, 123 passing, 4 skipped, 0 failing, 4 m 57 s.** No orphan
  `Siemens.Automation.Portal` process afterwards. The four skips are the three multiuser session
  tests (their asset cannot exist here) and `DownloadToSimulation_MinimalProject_ReachesRun`
  (`[Ignore]`, V20 confidential-data password — see "Phase 4" below).
- **Governance suite: 63 cases, 63 passing, 765 ms, no TIA Portal**, up from 27.
- **Both projects build with 0 warnings**, `TreatWarningsAsErrors` on.
- **51 tools**, up from 45: `ListBackups`, `GetJobStatus`, `ListJobs`, `CancelJob` are new, and
  `CompileSoftware` joined the guarded ones.

### What was added today, in the order it was done

1. **The guard was run against TIA Portal for the first time** and found two defects reading it
   had not, one of them in production code. Both below.
2. **`CompileSoftware` is guarded**, on the user's decision. Sixteen guarded tools, not fifteen.
3. **The calendar was deleted from the roadmap**, on the user's instruction.
4. **The backup registry**, replacing the `backupDirectory` parameter on three tools.
5. **Asynchronous jobs**, for `CompileSoftware` and `DownloadToSimulation`.
6. **`McpServerWrites.cs`**, holding every tool that changes anything and nothing else.
7. **A review of all of the above found five things**, all fixed the same day: a job that could
   report success for work the guard stopped, a coverage hole where nothing exercised the allowed
   path through the MCP layer, a test-suite isolation leak, the suite running in the wrong COM
   apartment, and Openness calls genuinely overlapping with nothing to stop them. Below.
8. **`OpennessGate`**, which closes the last of those: one Openness call at a time, enforced by the
   property every tool goes through rather than by convention.
9. **A deliverable-by-deliverable check against the roadmap**, which found one thing missing. Below.

### The two defects the first real run found

Both were in `Test16GuardedWrites`, the class that had never executed, and one of them was in
production code rather than in the test:

1. **`Engineering.TiaMajorVersion` was only ever set by `Program.Main`.** Any host that initialised
   Openness another way left it at 0, so the four V20-only document tools refused to run on a V20
   machine — `ImportFromDocuments` threw "requires TIA Portal V20 or newer" before the guard was
   ever consulted. Two statics held one fact and only one of them was written.
   `Openness.Initialize` now sets both. The test suite is a host of exactly that kind, which is how
   this surfaced; a second MCP host would have hit it in production.
2. **`RequestContext<T>` cannot be constructed without a live server**, so the test could not build
   one for the async import tool. `ImportBlocksFromDocuments` now reads `context?.Params`: no
   context is the same condition as a caller that did not ask for progress, and the tool reports
   none and does the work.

**Next action:** commit on a branch and open a pull request — never push to `main`. The diff is
large; "Phase 1 — governance" below is written to be read alongside it.

Then, before phase 2 and agreed with the user on 2026-08-18: **the single Openness thread.** See
"The hazard the apartment fix does not remove". The user chose it over leaving the concurrency
question open, wanting stability before more features are built on top. One decision is still hers
and is recorded here so it is not lost: whether `runAsJob` stays exposed in the meantime, or waits
until that thread exists.

State during the session of 2026-08-12 (afternoon):

1. ✅ **Unblocked:** the session token now includes `MANUELA\Siemens TIA Openness`.
2. ✅ **Unblocked:** `TiaPortalLocation` set at process and user level.
3. ✅ **Unblocked:** Visual Studio is not needed. `repos/tiaportal-mcp` builds with the
   already installed .NET SDK 8.0.405: `dotnet build TiaMcpServer.sln` → 0 warnings, 0 errors.
4. ✅ **First real `Connect` achieved** against TIA Portal V20. `Test1Portal` 3/3 passing
   (`Test_101_ConnectPortal` 46 s, `Test_102_DisconnectPortal` 1 ms, `Test_103_IsConnected` 503 ms).
   Phase 1 closed.
5. ✅ **Repository fully translated to English** (see the language rule in `../CLAUDE.md`).
   `ESTADO.md` → `STATUS.md`, `REPOS-REFERENCIA.md` → `REFERENCE-REPOS.md`.
6. ✅ **The three upstream defects are fixed and verified** on branch
   `fix/portal-lifecycle-and-errors` of `repos/tiaportal-mcp` (uncommitted).
   Builds with 0 warnings, 0 errors; `Test1Portal` 3/3 passing.
   Two measurements confirm the fixes rather than just the tests going green:
   - Run time dropped from **48.6 s to 5 s** — headless start instead of the forced UI.
   - After the run, `Get-Process Siemens.Automation.Portal` returns **nothing**.
     Before the fix it left a 2 GB orphan holding the licence.
7. ✅ **Phase 2 started: `RetrieveProject` works against a real archive.**
   `Test6Retrieve` 3/3 passing in 33 s, no orphan process, temp directory cleaned.
8. ✅ **Forked into `src/` and `tests/`.** The code now lives in this repository and builds
   under our own strict rules: **0 warnings, 0 errors**, down from 1581 errors on first
   attempt. `Test1Portal` + `Test6Retrieve` 6/6 passing from here in 16 s, no orphan process.
   See "The fork" below for what was switched off, what was really fixed, and what is debt.
9. ✅ **Bulk export to text works.** `SourceSnapshotExporter` writes real SCL / DB / UDT source
   plus tag tables, mirroring the group hierarchy. `Test7Snapshot` 4/4 passing; output inspected
   by hand, not just asserted. See "Bulk export to text" below, including the LAD limitation.
10. ✅ **Both operations exposed as MCP tools**, 30 → 32. `RetrieveProject` and
    `ExportSourceSnapshot` are callable by the LLM, with error mapping centralised.
    Writing those tests exposed a **fourth upstream defect**: `DisconnectPortal` never closed the
    open project, leaving TIA Portal holding file handles on its directory. Fixed by modelling
    portal ownership, since closing the project is only ours to do when we started the portal.
11. ✅ **The inherited tests are alive.** `Settings.cs` holds no filesystem paths any more and the
    six dead test classes run: **66 tests, 63 passing, 3 skipped, 0 failing** in 2 m 12 s.
    The three skips are the multiuser session tests, which need an asset that cannot exist here.
12. ✅ **Phase 3 done: the loop is closed.** The server can now write SCL into a project, compile
    it, and say what is wrong in terms a caller can act on. 34 tools.
    **74 tests, 71 passing, 3 skipped, 0 failing** in 2 m 15 s.
13. 🔄 **Phase 4 is done except the download.** Instance lifecycle, addressing, network-mode
    reporting and the simulation-only guarantee are all verified; seven simulation tools are
    exposed, taking the server to **40**. Suite: **83 cases, 79 passing, 3 skipped, 1 failing**,
    and the single failure is `DownloadToSimulation` — parked with full evidence below.
14. ✅ **Phase 8 started: `GetNetworkTopology`.** Every device interface with its address and
    subnet, `Test12Topology` 3/3. Server at **41 tools**.
    Reading and writing PLC tags was the intended next step and turned out **not** to be
    independent: PLCSIM tag access is by name and requires `UpdateTagList()`, which reads the tag
    list from a downloaded program. Measured before starting, not after.
15. ✅ **The network is versioned with the code.** `ExportSourceSnapshot` writes
    `network/topology.txt`, sorted and byte-identical between runs of an unchanged project.
16. ✅ **Phase 8: PROFINET, PROFIBUS and OPC UA done.** IO and DP master systems can be created
    and devices attached; OPC UA server interfaces can be listed and exported. **45 tools.**
17. ➡️ **Next action, agreed with the user on 2026-08-13:** commit what is pending, then the
    `FB_Station` pattern — the actual content of her coursework, now standing on solid ground.
    MQTT is the remaining piece of phase 8 and is generated code, not configuration.
14. ⏳ **Note:** the upstream clone still holds the same three fixes on
    `fix/portal-lifecycle-and-errors`, uncommitted, worth offering as a PR. There is now a fourth
    fix — the project not being closed on disconnect — that upstream also needs.

19. ✅ **2026-08-17 — PHASE 0 IS CLOSED. The loop reaches PLCSIM Advanced and RUN.**
    `Test11Download` 5/5, confirmed twice. Six separate faults in series, each hiding the next;
    see "RESOLVED" below. `DownloadToSimulation` is no longer an open issue.
    Next: metrics can now measure what fraction of specifications pass on a simulated CPU,
    because programs finally run on one.
18. 🔑 **2026-08-17 — the root cause of the whole simulation blockage was found.**
    **A PLCSIM Advanced controller stays registered only while a handle to it is open.**
    `SimulationRuntime` opened and closed an `IInstance` per operation, so every controller it
    created unregistered itself within fifteen seconds, with nothing touching it. See
    "The instance was never there" below. Fixed and covered by a test that would have failed
    yesterday. `Test11Download` 4/5 — the download itself is still open, but for the first time
    the controller is alive, reachable and discovered when it runs.

Required reading at startup: this file, `../CLAUDE.md` and `REFERENCE-REPOS.md`.
The plan for what comes next is in `ROADMAP.md`.

## Phase 1 — governance (2026-08-17)

The layer itself was written earlier the same day: mode gate, confirmation phrase, single-action
plans, deny-by-default whitelist, append-only audit trail, and `GuardedWrite` as the one path
through all of it. What was missing is what made it real — **the write tools did not use it.**

### The sixteen tools that now ask permission

| Family | Tools |
|---|---|
| Program | `WriteScl`, `ImportBlock`, `ImportType`, `ImportFromDocuments`, `ImportBlocksFromDocuments` |
| Compilation | `CompileSoftware` |
| Network | `CreateIoSystem`, `AssignDeviceToIoSystem` |
| Simulation | `CreateSimulationInstance`, `StartSimulationInstance`, `StopSimulationInstance`, `DeleteSimulationInstance` |
| Deployment | `DownloadToSimulation` |
| Project | `SaveProject`, `SaveAsProject`, `CloseProject` |

**`CompileSoftware` was left out at first and then put in, on the user's decision of 2026-08-18.**
The argument for leaving it out was that a compile is the verification half of the
generate-compile-fix loop, so gating it gates the diagnosis rather than the change. The argument
that won: a compile is what marks blocks consistent, and consistent blocks are what a controller
will accept. With real machines expected from October, a session whose policy says nothing about a
program must not be able to make that program's code downloadable. It costs nothing in Study Mode,
where a whitelisted target confirms itself, and no existing test changed behaviour — every compile
in the suite calls `Portal.CompileSoftware` directly rather than the MCP tool.

Exports, snapshots and `RetrieveProject` are not guarded: they write to disk, never to the project
or to a controller.

### The backup registry

The roadmap asked for this in one sentence: *a backup the caller can forget to ask for is not a
backup.* The parameter it replaced was mandatory, so nothing could be skipped — but the caller
chose the location, which meant nobody could enumerate what had been saved and an agent could put
it in a temp directory Windows would reap.

`WriteScl`, `CreateIoSystem` and `AssignDeviceToIoSystem` no longer take a `backupDirectory`. They
ask `IBackupRegistry` for one and are told: `.tia-mcp/backups/20260818-120000-WriteScl-PLC_0/`,
configurable with `--backups`. `ListBackups` is the new read tool that makes "listable" mean
something to the caller.

Two decisions worth knowing when reading it:

- **A manifest is written at allocation, before the export runs**, and records only what is true
  then — which tool, which target, when. It never claims the change succeeded; the audit trail is
  what says that. So a directory holding a manifest and no files is a write that was refused or
  that failed before exporting, and `ListBackups` reports it as `fileCount` 0 rather than hiding it.
- **The location is decided before the policy is consulted**, because the audit line has to name
  it. A refused change therefore leaves an empty backup directory. That is litter, not a lie, and
  it is visible as exactly what it is.

### Asynchronous jobs

Folded into phase 1 by the roadmap, for a measured reason: a download once blocked this project for
thirteen hours with no way to ask what it was doing.

`CompileSoftware` and `DownloadToSimulation` take `runAsJob`. With it they return a job id at once,
in their own response type with an empty payload and `outcome` `Running` in the metadata — the same
shape a change awaiting confirmation already used, and for the same reason: there is no result yet.
`GetJobStatus`, `ListJobs` and `CancelJob` are the three new read tools.

**The job runs the whole tool, guard included.** It does not reach past the guard and start the
Openness call: the audit records a change as applied when the work returns, so a job that let the
guard finish early would write a line claiming a compile had happened before it had.

**Cancellation only works before the work starts, and that is Openness rather than a shortcut.** A
compile and a download are blocking calls that accept no cancellation token and cannot be
interrupted. So a queued job cancels and never runs; a running one is reported as not cancellable.
Agreed with the user on 2026-08-18 in preference to a "stop waiting, let it run" cancellation,
which would have left work running inside TIA Portal that nobody could see the result of.

The `Queued -> Running` transition happens under the same lock `Cancel` takes, which is what makes
cancelling before the start reliable rather than a race. `IJobDispatcher` exists so the tests can
hold the work and assert that exactly: **no test in `JobStoreTests` sleeps**, because a test that
waits long enough to usually pass is one that eventually fails for no reason and gets deleted.

### McpServerWrites.cs

`McpServer.cs` went from 2908 lines to 2077, and the seventeen tools that change anything — the
sixteen guarded ones plus `ApplyChange` — now live in `McpServerWrites.cs`, 864 lines. A partial
class, not a separate type: the MCP SDK discovers tools by attribute on one `[McpServerToolType]`,
and these methods share the private service accessors with the read tools.

Still over the 300-line limit, both of them, and that is the roadmap's own scope: it asks that the
write tools move out, not that all 45 read tools be restructured. What the split buys is checkable
by eye — **`GuardedTool.Run` appears seventeen times in `McpServerWrites.cs` and zero times in
`McpServer.cs`**, so a write tool that forgot the guard, or that was written in the wrong file, now
shows up in a `grep` rather than in a machine.

### What reviewing the day's own work found

Asked whether all of this was correct, four answers were worth having rather than reassurance.
The third was found by the tests written for the second, within minutes of writing them.

**A job could report success for work that never happened.** `runAsJob` wrapped the whole tool,
guard included, and watched only for exceptions. But a refusal and a change awaiting confirmation
are *ordinary responses* — that is a deliberate rule of this layer — so the job saw no exception and
went to `Succeeded` while nothing had been compiled or downloaded. `StartAsJob` now passes the
response through `RequireApplied`, which throws when the guard's `outcome` key is present, and the
job lands on `Failed` carrying the reason. The metadata key is a constant on `GuardedTool` now,
because a literal in two files is what let the two drift apart in the first place.

In the default build only the refusal half of that was reachable, since Workshop Mode is compiled
out. That is the argument for fixing it now rather than later, not against.

**Nothing exercised the allowed path through the MCP layer.** Every write-tool test lived in
`Test16GuardedWrites`, under a policy that denies everything, and every other test in the suite
calls `Portal` directly. So the code between a tool and the portal — the backup registry it asks
for a location, the job store it hands long work to — had been written and never run.
`Test17WritesApplied` closes that: a write really leaves its previous state where `ListBackups`
finds it, a compile job really carries a result back to `GetJobStatus`, and cancelling a finished
job does not rewrite it.

**The test suite was not isolating that policy, and had not been all along.** The three new tests
failed on their first run with *no policy is configured for Study mode* — the deny-everything
container `Test16GuardedWrites` installs was still in place. MSTest runs `[ClassCleanup]` at the
end of the **assembly** by default, not the end of the class, so the restore never happened in
time. `[ClassCleanup(ClassCleanupBehavior.EndOfClass)]` fixes it.

That leak had been there since `Test16` was written, and nothing failed: every other test in the
suite calls `Portal` directly and never touches the container. It became visible the moment a test
went through the MCP layer on the allowed path — which is the hole those tests were written to
close, closing itself on the first run.

**And the suite was running in a different COM apartment than the server.** With isolation fixed,
the two `runAsJob` tests still failed, with *Cross-thread operation is not valid in Openness within
STA*. Measured directly rather than reasoned about, on 2026-08-18:

| Calling thread | Openness directly | From `Task.Run` |
|---|---|---|
| **STA** | works | **throws** |
| **MTA** | works | works |

Openness objects created in an STA apartment are thread-affine. Created in MTA they are not,
because every MTA thread shares one apartment. `Program.Main` has no `[STAThread]`, so **the server
runs MTA**; MSTest on .NET Framework defaults to **STA**, so the suite did not.

So the failure was the test environment, not the feature — and that is the worse finding of the two.
A suite in the wrong apartment can neither reproduce a real threading bug nor tolerate code that
hands work to a worker thread, which `ImportBlocksFromDocuments` has done since it was written.
`tests/TiaMcpServer.Test/tia.runsettings` pins the suite to MTA and the csproj points at it, so
`dotnet test` picks it up with no extra argument. The suite also got faster: 6 m 33 s against 8 m 16 s.

Four times in one day, code that nobody had executed turned out to be hiding a defect: the guard's
first real run, then the allowed path, then the suite's own isolation, then the apartment it ran in.
The lesson is not about the individual bugs.

### One Openness call at a time (`OpennessGate`)

Jobs introduced something that could not happen before: **two Openness calls overlapping.** The
transport is stdio, one request at a time, so nothing used to run concurrently.

Whether that was dangerous was measured rather than assumed, and the first two attempts were
confounded before the third answered it:

- A compile plus a read, concurrently: both succeeded. Says little on its own.
- Two compiles, timed: the pair took 279 ms against 7650 ms for one. Worthless — after the first
  compile the software is consistent and the second has nothing to do.
- **Two snapshot exports, which do the same work every time: both ran from 1 ms to ~1620 ms.**
  Had COM been marshalling them into one apartment, the second would have started when the first
  finished. It did not. **They really do run in parallel, and nothing serialises them.**

The suite had been doing this all along without anyone noticing: it runs 16-way parallel at method
level, so concurrent Openness calls happened in every run.

`OpennessGate` is a re-entrant process-wide lock, taken by every tool that reaches TIA Portal:
`using var openness = OpennessGate.Enter();` as the first statement, 35 tools, and
`OpennessGate.Run(...)` around the six Openness calls the asynchronous export tools hand to worker
threads.

Four decisions in it are worth knowing:

- **Re-entrant, or the first job deadlocks.** `runAsJob` hands `CompileSoftware` to a worker, which
  calls `CompileSoftware`, which takes the gate again on the same thread.
- **`GetJobStatus`, `ListJobs`, `CancelJob` and `ListBackups` must never take it.** If polling had
  to queue behind the compile it is polling, asynchronous jobs would be pointless.
- **In `CompileSoftware` and `DownloadToSimulation` the gate goes *after* the `runAsJob` hand-off.**
  Starting a job is supposed to return at once; taking the gate first would make it wait for the
  job already running.
- **It must not be held across an `await` that changes thread.** `Monitor` is owned by a thread, so
  the continuation would not own it. That is why the asynchronous tools gate each call rather than
  their whole body.

What it guarantees is **one Openness call at a time, not one logical operation.** A bulk export makes
many calls and a compile can land between two of them. Holding the gate across a whole batch would
block every other request for minutes, which is a worse trade for a read.

#### Why the property throws

The gate would be a convention, and conventions rot: a tool that forgot it would work perfectly
until the day a job happened to be running, and the failure would be a damaged project rather than
an exception. So **`McpServer.Portal` refuses to hand out the portal unless the gate is held.** Every
tool reaches TIA Portal through that property, which makes it the only place the omission can be
caught at all — and it is caught on the first call, in the suite.

That needs a `CA1065` suppression, since a getter must not throw. It is suppressed at that one
property with the reason written beside it. The alternative was making it a method, which would have
put `RequirePortal()` in front of fifty-four call sites to satisfy a rule about accidental throws.

#### MTA is now declared rather than inherited

`Main` is marked `[MTAThread]` and calls `RequireMultiThreadedApartment`, which refuses to start
otherwise. A console entry point is MTA by default, so this was true by accident, and the accident
was load-bearing: in an STA apartment every background Openness call throws. Saying it out loud
means nobody removes it by adding an attribute for an unrelated reason, and a future HTTP transport
fails at startup with a reason instead of failing inside a job.


### Checking the deliverables one by one

Every file the roadmap's phase 1 table names exists, and `ApplyChange`, `GetOperationMode` and
`McpServerWrites.cs` with it. Two of the checks are worth recording because the answer was not the
one expected:

- **"Wildcards rejected in the Workshop section" is implemented and tested**, but in `ModeRules`
  rather than `WritePolicyFile` where it was looked for first. `WorkshopRules_WithAWildcard_AreRefusedWhenLoaded` asserts it.
- **The supervision requirement was missing from `README.md` and `CLAUDE.md`.** Phase 1 asks for the
  security model documented in both "including the supervision requirement", and it was only in
  `ROADMAP.md`. Now in all three. It is the one rule here that no software can enforce — a
  whitelist, an audit trail and a confirmation phrase are all bypassed by a person in a hurry who is
  alone in a room with a machine — so of everything in this phase it is the one that most needed to
  be where people actually read it.

**The one deliverable that differs from the roadmap text** is the audit trail: JSONL behind
`IAuditTrail`, not SQLite. Deliberate and recorded above; phase 3 swaps it when the dashboard needs
real queries.

### Known and accepted, not fixed

Named here so nobody has to rediscover them:

- **`McpServer` is a static service locator, and this work added to it** — `Backups`, `JobStore`,
  and two more singletons in `Fallback`. `CLAUDE.md` bans mutable static state, and this is the
  reason `Test16GuardedWrites` has to swap a global container and be `[DoNotParallelize]`.
- **`Portal.cs` is 3558 lines** against a 300-line limit. That, not `McpServer.cs`, is the file
  that will hurt. `Responses.cs` is at 468.
- **A backup directory is allocated before the policy is consulted**, because the audit line has
  to name it, so a caller with no policy can create empty directories in a loop. Visible litter
  rather than a lie, and fine for a study session; not a permanent answer.
- **The gate serialises the test suite**, which runs 16-way parallel at method level. Run times
  since then have ranged 5 to 8 minutes against 6 m 33 s before, so the cost is inside the noise —
  and it means the suite had been making concurrent Openness calls in every run until today.

None of these were folded into this diff on purpose: they are a refactoring phase 1 did not ask
for, and adding it here would make the diff unreviewable exactly when the point is to review it.

### The shape a refusal takes

`GuardedTool.Run` is the seam. It takes the request, the work as a lambda, and a factory for an
empty response. The work runs only if the guard allows it; otherwise the caller gets the same
response type with `success` false, the reason in `Message`, and `outcome` in the metadata.

A refusal is a **response, not an exception**, and that is not taste: thrown, it would reach the
caller through the portal layer's decoration point as an operation failure — something to retry —
instead of a decision to respect.

A change awaiting confirmation loses its typed payload, and that is inherent: the work runs later
from `ApplyChange`, which reports it as text. It costs nothing in Study Mode, where whitelisted
changes confirm themselves.

### Targets, and why they are built in one place

A policy file is edited by a person, so the names in it have to be predictable. `ChangeTarget`
builds all of them:

- `PLC_0/Blocks/FB_Station` — a place in the project tree
- `simulation/Station_1` — a virtual controller, prefixed because it is not in the project and a
  rule about a PLC named `Station_1` must not govern an instance that shares the name
- `project` — save, save-as and close, which act on the project rather than on anything in it

### Three defects found on the way

1. **`AuditEntry.BackupPath` was always empty.** `ChangeRequest` had nowhere to put it, so the
   field that answers "where did the previous state go" never answered. `ChangeRequest.WithBackup`
   returns a copy carrying it — a copy rather than a setter, because a request that can be edited
   after a decision was taken about it describes something other than what was decided.
2. **The policy file could not carry comments.** Strict JSON is the wrong dialect for a file whose
   whole purpose is a decision someone took: the reason a target is listed belongs next to the
   target, and a policy that refused to load over a comment would be edited by deleting the reason.
   `JsonCommentHandling.Skip` now.
3. **`CloseProject` read `Portal.IsLocalSession` after closing**, to decide what to call the thing
   it had just closed. Pre-existing, and only visible once the method was pulled apart.

### The test that closes the gap this session opened

`Test16GuardedWrites` runs every guarded tool under a **policy file that does not exist** and
asserts each is refused. It needs no TIA Portal work, because a refused change never reaches the
Openness API — which is the property being asserted.

It exists because of a specific hole: **a write tool that forgot to ask the guard would pass every
other test in the suite**, since the rest run under a policy that allows what they do. It is also
the one test in the suite marked `[DoNotParallelize]`: it swaps the container every MCP tool
resolves from, and overlapping with another class would refuse that class's writes for reasons it
could never explain.

The suite now wires itself the way `Program` does — a real container with the governance layer in
it, reading a real `assets/policy.json` — rather than setting `McpServer.Portal` and hoping. A
suite that bypassed the governance layer would be testing a server nobody runs.

### Still open in phase 1

The three deliverables the roadmap named and the guard work had not touched are now done: the
backup registry, asynchronous jobs, and the write tools split out of `McpServer.cs`. Each has its
own section below. What remains:

- `DownloadPasswordConfiguration`, which Workshop Mode will need.
- The audit trail is JSONL behind `IAuditTrail`. Phase 3 swaps it for SQLite when the Workshop gate
  needs real queries. Deliberate: the roadmap's table says SQLite, and this is the one place the
  implementation knowingly differs from it.
- Nothing enforces that a *future* write tool is added to `Test16GuardedWrites`. The rule is
  written in `CLAUDE.md`; it is not checkable by the compiler.

Workshop Mode itself is **not** open work: by the decision of 2026-08-17 it is written last, after
every other phase, so the default build making it unreachable is the finished state, not a gap.

## The instance was never there (2026-08-17)

Everything `Test11Download` had reported since August — `Connect to module PLC_0 failed`,
`IsConfigured=False`, empty device scans, pings that answered on one run and timed out on the
next — was one fact wearing different disguises: **the virtual controller no longer existed.**

Measured directly, with nothing touching the controller between checks:

```
straight after create:      present, state=Stop, ips=[0.0.0.0]
after 15000 ms idle:        GONE
```

`SimulationRuntime` wrapped every operation in `using (var instance = Open(name))`. Releasing
the last handle unregisters the controller. **The handle is the lifetime.**

This also settles the question that cost six twenty-minute cycles in August: *why does it work
in the GUI?* Because the PLCSIM Advanced GUI keeps its own handle open. The manual download
was not doing anything different — it was talking to a controller that still existed.

`CA2000` had correctly flagged `IInstance` as disposable, and the conclusion drawn from it —
dispose immediately — was the opposite of what this API requires. The analyzer was right about
the fact and the reasoning from it was wrong.

### The same defect, worse, in the MCP layer

`McpServer` built `new SimulationRuntime(Logger)` inside each of five tool methods. With the
correct ownership model that is a production bug the test suite could never have caught: an
agent calling `CreateSimulationInstance` and then `DownloadToSimulation` would have lost the
controller between the two calls. `SimulationRuntime` is now a DI singleton, reached through
`McpServer.Simulation`.

Making `SimulationRuntime` implement `IDisposable` is what surfaced this: `CA2000` immediately
named all five sites. `TreatWarningsAsErrors` paid for itself in one build.

### What is verified now, and what is not

| Fact | Value |
|---|---|
| Controller survives while held | ✅ four checkpoints over 30 s |
| `Configuration.IsConfigured` | ✅ `True`, since `ApplyConfiguration` is called |
| `ping 192.168.0.1` | ✅ answered on attempt 1 |
| `tcp 192.168.0.1:102` (ISO-TCP) | ✅ connected |
| Device discovery | ✅ `'Accessible device' at 192.168.0.1 (MAC 02-C0-A8-00-64-00, S7-1500 (PLCSIM))` |
| `DownloadProvider.Download` | ❌ still `Connect to module PLC_0 failed.` |

### Ruled out, so nobody re-derives them

- **Network mode.** Already `TCPIPSingleAdapter`.
- **Adapter address.** `192.168.0.100/24`, manual, persistent.
- **Waiting longer.** 8 pings over 16 s changed nothing.
- **Test parallelism.** Fails identically in a single-test run.
- **Windows Firewall.** The PLCSIM adapter sits on a `Public` profile with no PLCSIM or TIA
  rules, which looked damning and is documented by Siemens as a cause. It is not this one:
  `tcp 102: connected` once a controller was actually alive. **The earlier `timed out` had been
  measured against a controller that no longer existed.** No firewall holes were opened on the
  strength of a false lead, which is the only reason that matters.

### Do not pass a discovered device to `Download`

`ConfigurationAccessibleDevice` is the third implementer of `IConfiguration`, it is what the
download dialog lists, and passing it to `Download` **kills the process**. Not an exception,
not a failed result: the test host dies after roughly 28 seconds and the run reports
`AggregateException` with no message, including for tests that never started. Reverted, and the
reason is commented at the call site so the next person does not re-derive it.

Method note, because it cost three runs: the lease and the download target were changed in the
same batch, so when it broke there was no telling which. The lease had been correct all along.
This is the rule from August — change one variable at a time — broken again on the same day it
was quoted. Having it written down does not prevent it; measuring does.

### RESOLVED — the download works, and it was six faults, not one

`Test11Download` **5/5, twice in a row**, 1 m 42 s and 1 m 44 s. The program compiles, downloads
to a PLCSIM Advanced virtual controller, and the controller reaches RUN. **Phase 0 is closed and
the loop is closed.**

Whole suite: **103 cases, 99 passing, 4 skipped, 0 failing**, 5 m 4 s, 0 warnings. The suite went
from 83 cases with the download failing to 103 with it passing. The fourth skip is new and
deliberate — see "Still open".

It was never one cause. It was six, in series, each hiding the next — which is exactly why August
was so demoralising: every correct fix still failed, because another fault waited underneath.
Testing a hypothesis and seeing it fail did not mean the hypothesis was wrong.

| # | Fault | Fix |
|---|---|---|
| 1 | A controller stays registered only while a handle to it is open; `SimulationRuntime` opened and closed one per call, so controllers vanished within 15 s | Hold the handle for the controller's lifetime |
| 2 | `ApplyConfiguration` was never called, so `IsConfigured` stayed false | Apply the connection before downloading |
| 3 | Connection and target address are separate arguments | Use the five-argument `Download` overload |
| 4 | `UserManagementDownload` had no answer in the table, and Openness discards the message of anything a delegate throws | Answer it; record the prompt type before throwing |
| 5 | `IsSimulationDuringBlockCompilationEnabled` was false, so blocks compiled unsimulatable | `EnableSimulationSupport()` before compiling |
| 6 | The instance was `CPU1500_Unspecified`; text libraries are tied to device identity and failed with `InvalidAID` | Create the instance as the project's CPU (`CPU1511`) |

Fault 6 is the one that hid longest, because **the hardware configuration downloads successfully
against an unspecified controller**. Seeing `[Success] Hardware configuration` made the target
look correct while the software half failed for a reason that had nothing to do with the network.

### What actually unblocked it: fixing the diagnostics first

None of the six were found by reasoning. Each appeared once the failure could be read:

- `PortalException` was `[Serializable]` with **no deserialization constructor**, so every
  exception crossing an app domain — MSTest, the Openness callback layer — was replaced by a
  `SerializationException` about a missing constructor. The message naming the unanswered prompt
  was being destroyed in transit.
- Unanswered prompts are now recorded before being thrown, because Openness keeps only the type
  of a delegate exception and drops its message.
- Assertions printed `Errors`, the filtered view — error severity **and** non-empty description.
  Right for feeding a fix loop, useless as the only output: twice a failing download printed a
  blank list and said nothing. There is now a `Describe(report)` helper that prints everything.

That last one was written, identified, and then **repeated half an hour later** in a new
assertion, by copying the existing pattern. Diagnostic traps propagate by imitation, which is why
the fix was a named helper rather than an edited line.

### Ruled out by measurement, so nobody re-derives them

Network mode, adapter address, waiting longer, test parallelism, Windows Firewall
(`tcp 102: connected` once a controller was actually alive), the PLCSIM licence
(`LicenseStatus=OK`), `EnableLegacyCommunication`, and the fixture's own protection settings —
a project created from scratch failed identically until the six faults above were fixed.

Two dead ends worth naming:

- **Passing a `ConfigurationAccessibleDevice` to `Download` kills the process.** Not an
  exception, not an error result: the host dies after ~28 s and reports nothing.
- **`AlarmTextLibrariesDownload` must be `ConsistentDownload`.** `NoAction` looks like the safe
  skip and makes the hardware configuration itself fail to load (`0013 -32 0 0`), because the text
  libraries are part of it.

### New capabilities that came out of it

`Portal.CreateProject`, `Portal.AddDevice`, `Portal.CompileHardware`,
`Portal.EnableSimulationSupport`, `Portal.IsSimulationSupportEnabled`,
`Portal.DescribeSimulationConnection`, `SimulationRuntime.PowerCycleInstance`,
`SimulationRuntime.CreateInstance(name, cpuType)`, and `LicenseStatus` on
`SimulationInstanceInfo`.

`CompileSoftware` compiling only the program was a real defect: any change invalidating the
hardware configuration left a stale one that the download rejected with an error blaming the
target rather than the project.

### Still open

- **A project created from scratch cannot compile its hardware**: V20 demands a password for
  confidential PLC configuration data, and Openness exposes nothing for it — no type or member
  matching `Confidential`. `Test15MinimalFixture` therefore builds and addresses a CPU but cannot
  download to it. The fixture stays `TestProject1` for now.
- `DownloadPasswordConfiguration` is still unimplemented, which Workshop Mode will need.

### Superseded: the fixture protection theory

`TestProject1`'s CPU — a **CPU 1511-1 PN** — has its access level set to
**"No access (complete protection)"**: *"TIA Portal users and HMI applications will not have
access to any functions."* A download against that fails at connection time, and TIA reports it
as `Connect to module failed`, which is the same message it uses for "I cannot find you". The
API cannot tell the two apart.

This is a setting of the inherited fixture, not of this machine: nothing to do with the licence
(`LicenseStatus=OK`, measured) or the network (all measured good).

**Attempted and inconclusive.** Setting `Access control configuration` to `Disable access
control`, recompiling and re-archiving did **not** change the outcome. But the `Access level`
table still showed `No access` selected and greyed throughout, so it is unknown whether the
protection was actually cleared. The hypothesis is untested, not disproven.

To test it properly the level itself has to change: `Enable access control` →
`Use access control via access levels` → `Full access (no protection)`. That checkbox is the one
that makes the level table editable, and it is greyed out while access control is disabled.

### Two candidate next steps

1. **Finish the access-level change** as above, recompile, re-archive.
2. **Build a minimal fixture instead.** A fresh project with one CPU 1511-1 PN, one OB and the
   PROFINET interface at 192.168.0.1 sidesteps every inherited setting at once, and gives a test
   bench whose configuration is understood rather than archaeology. Probably the better use of
   an hour.

Also disproven along the way: the CPU-type theory. Siemens documents creating the instance as an
*unspecified* CPU 1500 as the normal workflow — the download is what specifies the hardware — so
`cpu=CPU1500_Unspecified` is correct and not the difference it looked like.

Openness V20 exposes **no protection settings at all**: `SupportSimulation`, `ProtectionLevel`
and three other candidate names all return `EngineeringNotSupportedException` on the CPU device
item, and `PlcSoftware` exposes only `Name`. `ProtectionLevel` was probed as a control precisely
because it certainly exists in the GUI. So this cannot be fixed in code — it is a project
setting, and it belongs in the install documentation.

### Archiving writes somewhere else

TIA Portal's `Archive...` saves to `Documents\Automation\` by default, not next to the project.
The first attempt at this test ran against the August fixture because the asset had not actually
been replaced — caught only by checking `LastWriteTime` before running. Same rule as ever: verify
the effect where the failure happens, not where the change was made.

## Goal

An MCP server that lets a **coding agent** generate, verify and deploy PLC code — first for the
final project of the CFGS in Industrial Automation and Robotics (**coordination of 4 stations**:
palletizing, plotting, counting, robots), and then for every project of the degree after it.

The four-station cell is the first client of this tool, not its purpose. Anything that only makes
sense for that cell belongs in `spec/` as data, never in the server. See phase 7.

Deadline for the first client: **six weeks** from 2026-08-12 → target ~2026-09-25 (start of classes).

## Verified environment (2026-08-12)

| Component | Status |
|---|---|
| TIA Portal V20 | Installed (`TIAP20`) |
| TIA Portal V12 | Installed (`TIAP12`, legacy) |
| Openness PublicAPI | V17, V18, V19, V20 in `C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\` |
| PLCSIM Advanced | Installed (`PLCSIMADV`) |
| PLCSIM V20 | Installed |
| WinCC Unified | Installed |
| .NET | `C:\Program Files\dotnet\dotnet.exe` |
| Node.js | v24.15.0 |
| Git | 2.55.0.windows.4 |

### Checks on 2026-08-12

- ✅ .NET Framework **4.8.1** (release 533509) — enough for the 4.8 target
- ✅ The local group `Siemens TIA Openness` **exists** on the machine
- ✅ The session token includes `MANUELA\Siemens TIA Engineer` **and**
  `MANUELA\Siemens TIA Openness` (re-login done)
- ✅ `TiaPortalLocation` = `C:\Program Files\Siemens\Automation\Portal V20` (process and user)
- ✅ External application whitelist cleared: `Test1Portal` genuinely connects

### Build tooling — RESOLVED

Verified 2026-08-12 (afternoon): there is still **only .NET SDK 8.0.405**, no Visual Studio,
no Build Tools, no MSBuild, no NuGet CLI. **None of them are needed.**

The previous session misdiagnosed the project. `src/TiaMcpServer/TiaMcpServer.csproj` is
**SDK-style with `PackageReference`**, not legacy. The `packages.config` sitting next to it
is a stale leftover from history: MSBuild does not read it and its versions no longer match
the csproj (e.g. `ModelContextProtocol` 0.2.0-preview.1 vs 0.3.0-preview.4). Ignore it.

Verified build of the whole solution:

```powershell
dotnet build "..\repos\tiaportal-mcp\TiaMcpServer.sln" -c Debug
# Build succeeded. 0 Warning(s). 0 Error(s).
```

The Siemens resolver locates the assemblies correctly:
`Resolve Siemens.Engineering assemblies for TIA Portal V20.0 from ...\PublicAPI\V20`.

Outputs: `src/TiaMcpServer/bin/Debug/net48/TiaMcpServer.exe` and
`tests/TiaMcpServer.Test/bin/Debug/net48/TiaMcpServer.Test.dll`.

### The inherited tests — revived

They used to be dead: `Settings.cs` pinned every one of them to absolute paths under
`D:\Siemens\...` that existed only on the original author's machine. Six of the eight test
classes could not run anywhere else.

`Settings.cs` now contains **no filesystem paths at all**. What remains are paths *inside* the
project (`PLC_0`, `Group1/PLC_1`, …), which belong to the fixture rather than to a machine, and
stay `const` because `[DataRow]` accepts only compile-time constants. The project itself is built
at run time: `AssemblyHooks` retrieves `assets/TestProject1.zap20` once per run and publishes the
resulting path.

**66 tests, 63 passing, 3 skipped, 0 failing, in 2 m 12 s.** No orphan process, working directory
removed.

Three things worth knowing about how they were revived:

- **Several of them asserted nothing.** `Assert.IsNotNull(success)` on a `bool` can never fail, so
  those tests passed by construction. They now check something real: that the project tree names
  `PLC_0`, that the software tree names `Main`, that an empty result is an empty list and not null.
- **The multiuser session tests are skipped, not faked.** A local session is an `.alsNN` produced
  by a TIA Portal Multiuser server; no such asset ships here and one cannot be synthesised
  offline. They carry `[Ignore]` with that reason rather than being deleted, because a deleted
  test stops recording that the surface is uncovered. `GetSessions` with no session open is a real
  test and does run.
- **Block and type paths come from a snapshot that was actually inspected**, not from what the
  upstream fixture assumed. The import tests are round trips — export first, then import what was
  exported — so they cannot fail on a fixture document that the installed TIA version rejects.

`Common.cs` and `Helper.cs` were left unused by this and were deleted.

## Upstream defects found on first connect (2026-08-12)

All three came out of the first real `Connect`. They are debt we inherit unless we fix them.

### 1. `Test1Portal` leaves TIA Portal orphaned

Confirmed: after `dotnet test`, `Siemens.Automation.Portal` was still alive (2 GB of RAM,
window open), holding the licence.

MSTest instantiates the test class **once per method**, so the `_portal` field is not shared
between `Test_101` and `Test_102`:

- `Test_101_ConnectPortal` calls `new TiaPortal(TiaPortalMode.WithUserInterface)`
  (`Portal.cs:161`) and **never releases it** → orphan process.
- `Test_102_DisconnectPortal` runs on a fresh instance whose inner `_portal` is `null`.
  `_portal?.Dispose()` does nothing and returns `true`. **It passes green without
  disconnecting anything: a false positive**, and its 1 ms duration gives it away.
- `Test_103_IsConnected` does connect and release, but since a portal was already running it
  takes the `TiaPortal.GetProcesses().Any()` branch → `Attach()` (`Portal.cs:143`).
  **`Dispose()` on an *attached* portal only detaches; it does not close the process.**
  Only whoever created it with `new` closes it. Hence 503 ms versus the 46 s of a real start.

`AssemblyHooks.cs` has an **empty** `[AssemblyCleanup]`: that is exactly where the portal
created by the suite should die.

### 2. `ConnectPortal` opens TIA Portal with a user interface

`Portal.cs:161` forces `TiaPortalMode.WithUserInterface`, with no option. For an MCP server
running in the background the sensible default is `WithoutUserInterface`: it starts much
faster and does not steal focus. It must be configurable.

### 3. Swallowed exceptions

`ConnectPortal` (`Portal.cs:165`) does `catch (Exception) { return false; }` and
`DisconnectPortal` (`Portal.cs:190`) has a `catch` containing only a comment. Both violate
"never an empty `catch`, never swallow an exception" from CLAUDE.md, and they make it
impossible to know *why* a connection failed — which is the most common diagnostic in
Openness. They must map to `PortalException` with a `PortalErrorCode` per `docs/error-model.md`.

### Fixes applied (2026-08-12)

Branch `fix/portal-lifecycle-and-errors` in `repos/tiaportal-mcp`, kept separate so it can be
offered upstream as a pull request.

| Defect | Fix |
|---|---|
| 1 — orphan portal, false positive | `Test1Portal` gets a `[TestCleanup]` that disposes the portal of every test. `Test_102` now connects before disconnecting, so it can no longer pass without a connection. `Test_103` became an honest assertion: a fresh `Portal` reports no connection. |
| 2 — forced user interface | `ConnectPortal(bool withUserInterface = false)`. The default is now `TiaPortalMode.WithoutUserInterface`. |
| 3 — swallowed exceptions | `ConnectPortal` and `DisconnectPortal` map failures to `PortalException` with the new `PortalErrorCode.ConnectFailed`, decorated and logged at the single catch point. `Portal.Dispose()` still never throws — that would mask the caller's original exception — but it logs instead of swallowing silently. |

`AssemblyHooks.AssemblyCleanup` was left empty **on purpose**, with a comment explaining why:
`TiaPortal.GetProcesses()` cannot distinguish an instance started by the suite from one the
developer opened by hand, so killing processes from there would close the user's own TIA Portal.

Known wart: `ConnectPortal`/`DisconnectPortal` now always return `true` or throw, which makes
the `bool` return vestigial and leaves an unreachable `else` in `McpServer.Connect`. Kept for
now to hold the upstream diff to a reviewable size; collapse it to `void` when we fork.

## Phase 2 — in progress

Verified by grep: upstream had **no `Retrieve`/`ArchiveProject` support**, so there was no way to
get from `TestProject1.zap20` to an openable `.ap20` through the existing 30 tools.

### `RetrieveProject` — done

Added to the `Portal` layer, over `ProjectComposition.Retrieve(FileInfo, DirectoryInfo)`.
Signature confirmed by reflecting over the real `Siemens.Engineering.dll`, not assumed.

- Guard clauses map to `InvalidParams` / `InvalidState` / `NotFound`; failures decorate and
  rethrow as `PortalException` with `PortalErrorCode.RetrieveFailed` at the single catch point.
- **Refuses to overwrite** an existing target directory, per the backup-before-write rule.
- Uses `Retrieve`, deliberately **not** `RetrieveWithUpgrade`: the latter rewrites a project
  from an older TIA version irreversibly. Keeping the non-destructive default is documented
  in the code.
- `Test6Retrieve`: happy path plus two error cases. 3/3 passing in 33 s against the real
  archive, no orphan process left, temp directory cleaned up.

Side fix, required by our own CLAUDE.md: the sample archive is now copied next to the test
assembly and resolved through `AppDomain.CurrentDomain.BaseDirectory`, so
`Settings.Project1ArchivePath` is no longer an absolute `D:\` path that only existed on the
original author's machine.

## The fork (2026-08-12)

`src/` and `tests/` are no longer empty: the upstream code now lives in **this** repository,
with `LICENSE.txt` carried over as MIT attribution requires. Done before writing the bulk
export, so that work does not have to be written twice.

### What forking cost

Building the inherited code under our own `Directory.Build.props`
(`TreatWarningsAsErrors` + `AnalysisLevel latest-all`) produced **1581 errors**. Getting to
zero was not a matter of formatting.

**Analyzers switched off with a written reason** (they contradict decisions we had already
taken, so obeying them would have made the code worse):

| Rule | Count | Why it is off |
|---|---|---|
| `IDE0008` (`var` → explicit type) | 522 | Demoted to a suggestion. CLAUDE.md asks for judgement about when a type is obvious; the analyzer cannot make that call and forcing explicit types everywhere lengthens code without clarifying it. |
| `CA1848` (LoggerMessage delegates) | 212 | A hot-path optimisation. Every log here wraps a TIA Portal call that takes seconds, so the allocation is irrelevant and the ceremony hurts readability. |
| `CA1031` (catch general Exception) | 88 | Our error model **requires** a single `catch (Exception)` per public portal method to decorate and rethrow. The rule that matters, "never swallow", is enforced by `RCS1075` instead. |

**Real findings, fixed rather than suppressed:**

- `CA1001` — `Portal` held a disposable `TiaPortal` field but was not `IDisposable`. This is the
  repository's number-one domain rule and the analyzer caught it. `Portal` now implements the
  full dispose pattern with a `_isDisposed` guard.
- `IDE0005` in `Responses.cs` — an unused `using Siemens.Engineering;` **in the MCP layer**,
  which violates the dependency rule. Removing it makes that boundary real rather than nominal.
- `IDE0051` — `McpServer.BuildBlockPathSuggestion` was dead code; the logic had been inlined
  into the catch block. Deleted.
- `CA2237` — `PortalException` is now `[Serializable]`.
- The hardcoded `HintPath` into `C:\Program Files (x86)\Reference Assemblies\...` for
  `System.Management` is gone; the reference resolves from the targeting pack.

### Remaining debt — the ledgers

The rest is tracked as an **explicit, shrinking list** of `NoWarn` codes in
`src/TiaMcpServer/TiaMcpServer.csproj` and `tests/TiaMcpServer.Test/TiaMcpServer.Test.csproj`,
each with its count and reason, rather than by turning strictness off. When an area is cleaned,
delete its code from the ledger and the analyzer starts guarding it again.

Largest items: `CS1591` (424 public members without XML doc), `CA2254` (94 non-static log
templates), `CA1062` (56 unvalidated public arguments).

**One entry deserves attention:** `CA1001`/`CA2000` are suppressed in the test project because
the six inherited test classes (`Test2ProjectSession`, `Test21Project`, `Test22Session`,
`Test3Devices`, `Test4Software`, `Test5McpServer`) each hold a `Portal` and never dispose it —
**the same orphan-process leak that was fixed in `Test1Portal`**. They cannot run at all today
because `Settings.cs` points them at absolute `D:\` paths, so both problems get fixed together.

### Bulk export to text — done

`SourceSnapshotExporter`, a collaborator rather than more code in `Portal.cs`, which is already
102 KB. Reached through `Portal.ExportSourceSnapshot(softwarePath, targetDirectory, ct)`.

This is **not** upstream's `ExportBlock`. That writes SimaticML XML: faithful, enormous,
unreadable in a diff. Here blocks go through `PlcExternalSourceSystemGroup.GenerateSource`, which
produces the real SCL / DB / STL text — the whole point of putting a project in Git. Layout:

```
snapshot/
  blocks/<Group>/<Subgroup>/Name.scl|.db|.awl
  types/<Group>/Name.udt
  tags/<Group>/Name.xml
```

`SnapshotResult` reports four lists — `Exported`, `Inconsistent`, `Unsupported`, `Failed` — because
a snapshot is legitimately partial and failing the whole export over one block would make the
operation useless.

**The limitation that matters: LAD, FBD and GRAPH have no text form.** They exist only as
SimaticML. A text snapshot therefore never describes a program that contains graphical blocks.
This is reported in `Unsupported`, not hidden. Measured against `TestProject1`, the snapshot
produced 8 files and left out 5 LAD blocks — **including `Main`, the main OB**. For our own
four-station cell this is fine, since we already decided to generate SCL, but it means a snapshot
is not a backup and must never be treated as one.

Verified by inspecting real output, not only by tests going green:

```
blocks/1_Tests/FC_Block_1.scl                              types/Common/CarrierRegister/*.udt (4)
blocks/1_Tests/DB_Block_1.db                               tags/Standard-Variablentabelle.xml
blocks/Common/CarrierRegister/GLOBAL_POSITIONING.db
```

The generated `.scl` is genuine readable source (`FUNCTION "FC_Block_1" : Void … END_FUNCTION`),
and the group hierarchy is mirrored as folders.

Two improvements over `repos/TiaExportBlocks/Program.cs`, the reference for this phase:

1. It **never recursed into type user groups**, so UDTs filed in subgroups were silently missing
   from its exports. Confirmed by reflection that `PlcTypeGroup` exposes `Groups`, and recursed.
   On `TestProject1` this is the difference between 4 UDTs and 0.
2. Added a `CancellationToken`, checked between items. A bulk export is exactly what CLAUDE.md
   requires to be cancellable.

`Test7Snapshot`: 4/4 passing in 38 s, happy path plus tag-table coverage plus two error cases.

### Exposed as MCP tools

Both new operations are reachable by the LLM, taking the server from 30 tools to 32:

- `RetrieveProject(archivePath, targetDirectory)`
- `ExportSourceSnapshot(softwarePath, targetDirectory)`

The tool description states the LAD limitation, and the response message repeats it whenever a
snapshot is partial. A caller that never reads `Unsupported` still cannot mistake a partial
snapshot for a complete one.

Error mapping is centralised in a small `ToMcpException` helper rather than the copy-pasted
`switch` upstream repeats per tool. Missing paths and invalid state map to `InvalidParams`, so a
caller's mistake is never reported as an `InternalError`.

The two new response objects are **immutable**, as CLAUDE.md requires. The 30 inherited ones
still carry public setters; converting them is a separate change, since doing two of thirty makes
`Responses.cs` inconsistent for no gain.

### A fourth defect, found by the MCP tests

Wiring the tools up made `Test8McpSnapshot` fail in cleanup with a sharing violation on
`PEData.idx`: TIA Portal still held the retrieved project directory.

The cause was `DisconnectPortal` setting `_project = null` without closing the project. Until a
project is closed, TIA Portal keeps file handles inside its directory, and disposing the portal
does not release them in time. `Portal.Dispose()` did close the project, which is why
`Test7Snapshot` passed and the MCP tests did not.

The naive fix — always close the project on disconnect — would be **destructive**: when we merely
attached to a TIA Portal the user started, closing their project is not ours to do. So the
ownership distinction noted earlier is now modelled. `_ownsPortalProcess` is set when
`ConnectPortal` calls `new TiaPortal(...)` and cleared when it attaches, and
`ReleaseProjectIfOwned` closes the project only for a portal we started.

This also explains the test structure: one portal per class rather than per test. A portal does
not die the moment it is disposed, so a per-test connect/disconnect cycle makes the next test
attach to the previous, still-closing process — and an attached portal correctly refuses to close
the project, locking the directory. `ClassCleanup` deletes best-effort for the same reason.

### Next in phase 2

1. Decide what lands in `export/` and whether a snapshot is committed automatically.
2. Consider a companion XML export for the LAD blocks, so a snapshot can be complete even if
   half of it is not human-readable.
3. Remove `CA1001`/`CA2000` from the test project's NoWarn ledger. They were suppressed because
   the inherited test classes each held a `Portal` and never disposed it; none of them owns a
   portal any more, so the rules should be armed again and the ledger should shrink.

Do not extract to `D:\`: only 2.3 GB free. The tests use a fresh directory under `%TEMP%`.

## Phase 3 — the closed loop

The half of the project that reading a project cannot provide. `WriteScl` and a repaired
`CompileSoftware` mean the server can now generate code, compile it, and explain the failure.

### `CompileSoftware` was losing the answer

The tool interpolated the Openness `CompilerResult` into a string, so a failed build reported the
object's type name and nothing else. Compiling from an agent was pointless: the caller could tell
*that* it failed and never *why*.

`CompileSoftware` now returns a `CompilationReport` of our own, which also removes an Openness
type that was crossing into the MCP layer against the dependency rule. A failed compile is a
normal return value, not an exception — the errors are the answer the caller asked for.

The messages arrive as a **tree**: the device holds messages for its software, which hold messages
per block, which hold the real diagnostics. Only leaves carry a description and only branches
carry a path, so flattening from either side alone produces nothing usable.
`CompilerResultReader` walks the tree joining parent names, which turns this:

```
CompilerResult
```

into this:

```
Error: PLC_0/Program blocks/FC_Inspect (FC2)/1 — Tag #NoSuchVariable not defined.
Error: PLC_0/Program blocks/FC_Inspect (FC2)/1 — Tag "AlsoMissing" not defined.
```

`Errors` also drops messages with no description. TIA marks every branch with the worst severity
beneath it, so one bad line yields three empty "Error" entries — for the device, the folder and
the block — that bury the two lines naming the problem. This was only visible by reading real
output; all seven tests passed either way.

### `WriteScl`

Openness has no "create a block from this text". The only route is to write the text to a file,
register it as an external source, and generate blocks from it. Three things worth knowing, all
commented at the point they matter:

- The external source is a real object that **stays in the project tree**, so it is deleted after
  generating. Otherwise every generation leaves another entry behind.
- Generation reports failure by **producing nothing**, not by throwing. An empty result is the
  error.
- `GenerateBlockOption.KeepOnError` keeps the existing blocks when the source does not parse,
  rather than half-replacing a working program with a broken one.

`backupDirectory` is **required**, not optional: generation overwrites blocks of the same name,
and the repository rule is that every write is preceded by an export. The backup deliberately uses
the full XML export rather than the text snapshot, because a snapshot cannot represent LAD and a
backup that silently omits half the program is not a backup.

## Decisions taken

| Decision | Reason |
|---|---|
| MCP in **pure C#**, not TS + sidecar | Openness is .NET; a bridge only adds serialization |
| Base: fork of **heilingbrunner/tiaportal-mcp** (MIT) | 30 tools already done, same target, permissive licence |
| Adopt its `AGENTS.md` + `style.md` + `error-model.md` | Shared logic, option to contribute upstream |
| Downloads only to **PLCSIM Advanced** by default | Safety: a badly generated block on real hardware is an accident |
| SCL over LAD for generated code | The SimaticML XML for LAD is huge and fragile |
| Everything in the repo written in **English** | Upstream is English, Openness is English, mixed-language code is hard to search |

## Analysis of the base: `tiaportal-mcp`

**MIT licence.** .NET Framework 4.8. TIA V20 by default (`--tia-major-version` for others).
Transport is `stdio` only.

### Architecture (2 layers)

```
src/TiaMcpServer/
├── ModelContextProtocol/
│   ├── McpServer.cs      (90 KB) — definition of the 30 tools
│   ├── McpPrompts.cs     (10 KB) — prompts guiding the LLM
│   ├── Responses.cs             — response objects
│   └── Types.cs
└── Siemens/
    ├── Portal.cs         (95 KB) — high-level API over Openness
    ├── Openness.cs              — wrapper around the Openness API
    ├── PortalException.cs / PortalErrorCode.cs
    └── State.cs
```

The separation is clean: the `Siemens/` layer knows nothing about MCP, and the
`ModelContextProtocol/` layer does not touch Openness directly. Keep that boundary.

### The 30 existing tools

- **Connection (3):** `Connect`, `Disconnect`, `GetState`
- **Project (6):** `GetProject`, `OpenProject`, `SaveProject`, `SaveAsProject`, `CloseProject`, `GetProjectTree`
- **Devices (3):** `GetDeviceInfo`, `GetDeviceItemInfo`, `GetDevices`
- **Software (3):** `GetSoftwareInfo`, `CompileSoftware`, `GetSoftwareTree`
- **Blocks (7):** `GetBlockInfo`, `GetBlocks`, `GetBlocksWithHierarchy`, `ExportBlock`, `ImportBlock`, `ExportBlocks`
- **Types/UDT (5):** `GetTypeInfo`, `GetTypes`, `ExportType`, `ImportType`, `ExportTypes`
- **SIMATIC SD documents, V20+ (4):** `ExportAsDocuments`, `ExportBlocksAsDocuments`, `ImportFromDocuments`, `ImportBlocksFromDocuments`

`CompileSoftware` exists → **the closed generate/compile loop is viable from day one.**

### What it does NOT have (verified by grep: 0 matches)

This is our added value. There is no reference at all to:

- `TagTable` → **no tag table management**
- `ExternalSource` / `GenerateBlocksFromSource` → **cannot write SCL directly**
- `Download` → **does not deploy to the PLC**
- `PlcSim` / `Simulation` → **no PLCSIM Advanced integration**
- `Watch` / `ForceTable` → no watch tables

## Roadmap

| Phase | Content | Status |
|---|---|---|
| 0 | Clone and analyse reference repositories | ✅ Done |
| 1 | Build `tiaportal-mcp` and connect to real TIA | ✅ Done |
| 2 | Bulk export → Git (project snapshot to text) | 🔄 `RetrieveProject` done |
| 3 | `WriteScl` through external source + `Compile` with parsed errors | ✅ Done |
| 4 | PLCSIM Advanced integration → automated tests | 🔄 All but the download |
| 5 | "Station" pattern generator from a specification | ⬜ |
| 6 | Documentation, demo, project report | ⬜ |
| 7 | Hardening: the difference between a demo and a tool used all year | ⬜ |
| 8 | Industrial communications: PROFINET, PROFIBUS, OPC UA, MQTT | ⬜ |

## Phase 4 — PLCSIM Advanced, started

The runtime is reachable and `SimulationRuntime` drives it: list, create, start, stop and delete
virtual controllers. `Test10Simulation` 4/4 passing.

Finding the API took some digging. PLCSIM Advanced is installed under
`C:\Program Files (x86)\Siemens\Automation\PLCSIMADV`, but that folder holds only the network
adapter tool. The runtime API is somewhere unexpected:

```
%ProgramW6432%\Siemens\Automation\PLCSIM_V20\resources\bin\wwwroot\assets\lib\runtime\
    Siemens.Simatic.Simulation.Runtime.Api.x64.dll   (v7.0.0.0)
```

`SimulationRuntimeManager.IsRuntimeManagerAvailable` returns true, so the native runtime is
registered and the whole phase is viable.

There is no NuGet resolver for this API as there is for Openness, so the path is a **probe, not a
constant**: `PlcSimApiPath` or the `PLCSIM_API_PATH` environment variable override it, and the
reference is guarded by `Exists()` so a machine without PLCSIM Advanced still builds. The
simulation tools then report the runtime as unavailable rather than failing to compile.

### Two things learned the hard way

- **`Private=false` compiles and then fails at run time.** The API is not on any probing path, so
  it has to be copied next to the output.
- **A `try`/`catch` around the API call does not protect anything.** The CLR loads an assembly
  when it *compiles* the method that mentions its types, not when the line runs, so
  `FileNotFoundException` was thrown before entering the `try` and the graceful degradation this
  class advertises did not exist. The API touch now lives in its own `[MethodImpl(NoInlining)]`
  method, which is what makes the boundary real. `IsAvailable` is asserted in the tests precisely
  because a silently stubbed build would let every other simulation test pass while testing
  nothing.

`CA2000` also caught that `IInstance` is disposable and each handle holds a live connection to a
virtual controller — the same resource rule as TIA Portal objects, and the reason those analyzers
were re-armed.

### Instance lifecycle — done

`Test10Simulation` 7/7. Two facts the failures taught, both now written into the code:

- **`Run()` on a freshly created instance fails with `-52 IsEmpty`.** A new virtual controller is
  powered on but has no program, and an empty PLC cannot enter RUN. Reaching RUN requires a
  download, so RUN belongs with the download tests, not the lifecycle ones. There is a test that
  asserts this failure, so the rule stays documented rather than remembered.
- **`PowerOff()` on an instance that is already off throws `InvalidOperatingState`**, which turned
  every cleanup path into a failure. `DeleteInstance` checks the state first.

Transitions now pass an explicit timeout. The parameterless overloads do not wait for the state
change, so a create-run-stop-delete sequence was racing the controller.

The PLCSIM API throws its own `SimulationRuntimeException`, which was escaping the layer intact —
callers would have faced two unrelated error models depending on which part of the server they
reached. `SimulationRuntime` now has a single decoration point, like `Portal`, mapping to
`PortalErrorCode.SimulationFailed`.

### Download to simulation — written, not yet exercised

The connection configuration had to be discovered rather than assumed:

```
software service: NULL      ← DownloadProvider is NOT a service of PlcSoftware
deviceItem service: ok      ← it belongs to the DeviceItem
MODE: PN/IE
  PC INTERFACE: 'Siemens PLCSIM Virtual Ethernet Adapter'   ← simulation
  PC INTERFACE: 'RZ616 Wi-Fi 6E 160MHz'                     ← real hardware
```

That last pair is the whole safety design. **The PC interface is what decides where a download
goes.** `SimulationDownloader` only ever selects the PLCSIM virtual adapter and refuses when it is
absent, so "simulation only" is enforced by construction instead of by remembering. Downloading to
hardware has no implementation at all — adding one is a decision with a confirmation flow
attached, not a parameter.

A download is interactive: TIA raises prompts and an unanswered one blocks forever. Each prompt
type carries its own selection enum, so there is no generic answer, and a strategy table keyed by
type replaces what would otherwise be a forty-branch switch. **The answers are chosen, not
defaulted** — taking the first enum member would have been wrong, because
`StopModulesSelections` begins with `NoAction` and a download that does not stop the modules
fails. An unrecognised prompt is logged by type name, which is exactly what needs adding.

`DownloadResult` has the same tree shape as `CompilerResult`, so both are reported through
`CompilationReport`: an agent that understands compile errors understands download errors without
learning a second format.

### The download is blocked on a machine setting

`Test11Download` 2/3. The safety check and the not-found case pass; the download itself reaches
TIA Portal and fails with **`Connect to module PLC_0 failed.`**

Two API facts were fixed on the way, both worth keeping:

- **`DownloadOptions.None` is rejected** with "Invalid download option". The call has to say what
  to download, and a virtual controller starts empty, so `Hardware | Software` is the combination
  that makes sense: without the hardware configuration there is nothing for the blocks to run on.
- **The PLCSIM interface is always present**, whether or not an instance exists — it is a driver.
  A test asserting "refuses when no instance is running" was therefore testing a behaviour that
  does not exist. It was replaced by `GetSimulationTargetName`, which reports where a download
  *would* go without performing one. That is a better check of the safety property and a useful
  capability in its own right: an agent should be able to ask where an action would land before
  taking it.

**Status: still failing, and the next step is manual. Do that before writing any more code.**

The last run downloaded to an instance created **outside** the suite, already powered on at the
right address, with the runtime in `TCPIPSingleAdapter` bound to the virtual adapter, and the
software compiling cleanly. It failed identically. That rules out instance creation, addressing
and lifecycle as the cause — everything this project controls is now known-good, and the failure
is in the connection itself.

The environment was also genuinely misconfigured in two ways found along the way, both now fixed
by the user: the virtual adapter had an APIPA address, and PLCSIM had **no interface mapping**,
which surfaced as "interface mapping is invalid or missing" when powering an instance on by hand.
That second one had been visible for hours in `NetInterfaces` and was missed because the probe
read a field name that does not exist on `SNetInterfaceInfo` — PowerShell returns empty for a
missing property, so a broken probe and an empty result look identical. **A diagnostic that cannot
distinguish "asked and got nothing" from "could not ask" will mislead an agent exactly as it
misled this one.** That belongs in the phase 7 diagnostics work.

`Connect to module PLC_0 failed.` survives every change below. Three real defects were found and
fixed on the way — none of them was the cause, and each was announced as though it might be:

1. `DownloadOptions.None` is rejected; the call must say what to download.
2. The download address was not being named, leaving `IsConfigured=false`.
3. In TCP/IP mode a new instance reports **`0.0.0.0`** — it has no address until one is assigned.
   `SetInstanceAddress` fixes that, and the assignment is verified in the test before the
   download runs, so this one is genuinely closed.

The machine's PLCSIM virtual adapter was also on an APIPA address and is now `192.168.0.100/24`,
which needed fixing regardless.

**The method was the problem, not the knowledge.** Six cycles of roughly twenty minutes, each
spent reasoning about the system instead of interrogating it. The one useful step — creating an
instance and asking whether anything answered at `192.168.0.1` — took thirty seconds and produced
the `0.0.0.0` that no hypothesis had considered. The rule earned twice over: **measure the system,
do not reason about it, and check the effect where the failure happens.**

The discriminating test has not been run because it needs the GUI: perform the same download by
hand in TIA Portal against a PLCSIM instance. If that fails too, the environment is not set up and
no amount of code will help. If it succeeds, the difference between what the GUI does and what
`SimulationDownloader` does is the answer, and it will be a short list.

### The GUI answered it: connect through the subnet, not the target interface

Downloading by hand in TIA Portal **works**: `1 compatible devices of 1 accessible devices found`,
CPU at `192.168.0.1`. So the environment is fine and the fault was ours. The dialog also showed
where: **Connection to interface/subnet = `PN/IE_1`**. The code was passing
`TargetInterfaces.First()` — `1 X1` — which the API accepts and then fails on at connection time
with the same useless "Connect to module failed".

`ConfigurationSubnet` is not an `IConfiguration`, but the `ConfigurationAddress` objects under it
are, so the pairing to pass is subnet-plus-address — exactly what the dialog shows: `PN/IE_1` at
the top, `192.168.0.1` in the table below. Fixed, but **not yet verified**: see the hang below.

**When something works in the GUI and not through the API, the GUI is showing the answer.** That
check was proposed three times and skipped three times in favour of another hypothesis, at roughly
twenty minutes each.

### OPEN ISSUE: `DownloadToSimulation` — parked deliberately

**Everything around it is verified. The `Download` call itself is not.** Suite: 79 passing,
3 skipped, 1 failing, and the one failure is this.

Measured, so a future session does not re-derive it:

| Fact | Value |
|---|---|
| Instance reachable | `ping 192.168.0.1` → **True**, `comm=TCPIP`, `IPs=[192.168.0.1]` |
| PLCSIM network mode | `TCPIPSingleAdapter`, bound to the virtual adapter |
| PC adapter | `192.168.0.100/24`, same subnet as the CPU |
| Manual download in the GUI | **works** — `1 compatible devices of 1 accessible devices found` |
| `SUBNET 'PN/IE_1'` | `addresses = [192.168.0.1]` |
| `TARGET '1 X1'` | `addresses = []` |
| `Configuration.IsConfigured` | `False`, before and after every attempt |

Combinations tried against `DownloadProvider.Download`, all failing with the identical and
uninformative `Connect to module PLC_0 failed.`:

1. target interface, `DownloadOptions.None` → rejected: "Invalid download option"
2. target interface, `Hardware | Software`
3. target interface **+ its own address** (there are none, so this degraded to 2)
4. the subnet's address alone as `IConfiguration`
5. target interface as connection **+ the subnet's address** as `ConfigurationAddress`

Not an unanswered prompt: the handler now throws on unknown prompt types and never fired.

**Leads for next time, cheapest first.** `IsConfigured` staying `False` suggests the connection
must be *established* through the composition rather than assembled from leaves — Openness
samples navigate with `Modes.Find(...)`, `PcInterfaces.Find(name, number)`, `TargetInterfaces.
Find(...)`, and that navigation may be what configures it, rather than the objects being passed.
Worth trying before anything else. After that: whether a first hardware download needs the CPU
specified beforehand, and whether TIA's own log records what the working GUI download did
differently.

### Two defects the hang exposed, both fixed

The verification run blocked for **thirteen hours** and had to be killed.

- **An unanswered download prompt blocked forever.** The handler logged unknown prompt types and
  left them at their default, with a comment admitting this "can block the call". It now throws,
  naming the type. A caller with an error can act; a caller with a hang cannot.
- **The suite attached to a TIA Portal opened by hand.** `ConnectPortal` joins a running portal
  rather than starting one, so the run shared the user's session, project and dialogs. The suite
  now refuses to start when any TIA Portal is running, via `Portal.GetRunningPortalCount()`.

A third lesson, from reading the wrong processes while the run hung: CPU climbing on *a* TIA
process was taken as proof that *our* download was progressing. It was the user's manual session.
Same failure as the earlier `SNetInterfaceInfo` probe — **a measurement that does not identify
what it measured is not evidence.**

### Ruled out along the way

**The PLCSIM virtual network adapter had no address on the controller's subnet.**

```
Ethernet 2 (Siemens PLCSIM Virtual Ethernet Adapter) = 169.254.65.88/16   ← APIPA, unconfigured
PLC in project and simulation instance               = 192.168.0.1
Test-NetConnection 192.168.0.1                       = False
```

TIA Portal cannot reach `192.168.0.1` through an adapter sitting on a link-local `169.254.x.x`
address. Everything else was a distraction. The fix is a static address on that adapter, in the
same subnet as the controller and different from it — for example `192.168.0.100/24`. It needs
administrator rights and it changes the machine's network configuration, so it belongs to the
user and to the install documentation.

**How three attempts were wasted, so it does not happen again.** The first dump already contained
`IsConfigured=False`, and the adapter's address was one command away throughout. Instead the mode
was blamed, changed *in a different process*, read back in that same process, and declared
applied — verifying an effect where it could not be observed. The rule this earns: when a change
is supposed to fix something, check it where the failure happens, not where the change was made.
The decisive check cost one command once the guessing stopped.

### Earlier evidence, kept because it rules things out

```
NetworkMode = Softbus
INSTANCE TiaMcpInspect_...: state=Stop cpu=CPU1500_Unspecified
  IPs = [192.168.0.1]
TARGET = Siemens PLCSIM Virtual Ethernet Adapter
PROJECT PLC 'PROFINET-Schnittstelle_1' address = 192.168.0.1
```

The addresses **match**, so the second candidate — an IP mismatch between the virtual controller
and the project — is ruled out. What remains is the mode: the instance is reachable over the
PLCSIM softbus, while the download is aimed at the virtual Ethernet adapter, which only carries
traffic when the runtime is in `TCPIPSingleAdapter` or `TCPIPMultipleAdapter`. No softbus PC
interface is offered by the CPU, so in this mode there is nothing valid to target.

The fix is `SimulationRuntimeManager.NetworkMode = TCPIPSingleAdapter`, applied **once, from
outside the server**, on 2026-08-12.

It is deliberately not exposed as a tool. Flipping a global setting on the developer's machine is
an installation step, not a runtime capability, and handing an agent the ability to reconfigure
PLCSIM is exactly what phase 7 exists to prevent. It belongs in the install documentation, and
`SimulationRuntime.NetworkMode` reports it so a diagnostic can say why a download cannot connect.

`SimulationRuntime.NetworkMode` and `SimulationInstanceInfo.IpAddresses` were added while chasing
this and are worth keeping: the failure TIA reports, "Connect to module PLC_0 failed", says nothing
about the real cause, and these are what turn it into something diagnosable. The test project
deliberately does not reference the PLCSIM API — reaching past our own layer to inspect the
runtime would have been the easy way and the wrong one.

### Next in phase 4

1. Decide the PLCSIM network mode, then finish the end-to-end download.
2. Read and write PLC tags on the running instance, so a test can drive inputs and assert outputs.
3. Expose the simulation tools over MCP.

## Phase 7 — hardening: from a demo to a tool used for years

The goal is not this year's project. It is that a **coding agent** drives this server across every
project of the degree, and keeps being useful years from now. That reframes what "professional"
means here: the primary user is not a person reading a screen, it is a model choosing a tool from
a description and acting on whatever comes back. Everything below follows from that.

### 1. Rails the agent cannot walk off

An agent will eventually call the destructive tool with the wrong path. The server has to be the
thing that makes that survivable, because the agent will not be.

- **A backup registry, not a parameter.** `backupDirectory` is currently something the caller
  passes and can therefore forget, mistype, or reuse. Backups should go to a configured root, be
  timestamped and rotated, and be listable and restorable through `ListBackups` / `RestoreBackup`.
  A backup nobody can find is not a backup.
- **`dryRun` on every write tool**, reporting exactly what would change. This is how an agent
  proposes a change for review instead of performing it.
- **An audit trail** of every write: when, which tool, which blocks, which backup. When something
  breaks three sessions later, this is the only way to know what touched it.
- **A hard gate on downloads to physical hardware.** CLAUDE.md already forbids it without explicit
  confirmation; that rule currently lives only in prose an agent may not have in context. It
  belongs in the code, as a refusal by default and an explicit opt-in argument.
- **Idempotency.** Calling `WriteScl` twice with the same source should be a no-op the second
  time, not a second overwrite.

### 2. Feedback an agent can actually act on

The generate-compile-fix loop is only as good as the errors it feeds back.

- **Map compiler messages to SCL source lines.** Today an error says "network 1 of FC_Block". The
  agent wrote *text*, and needs to know which line of the text it submitted is wrong. Without
  this, fixing is guesswork on anything longer than a toy block.
- **Cross-references**: which blocks call this one, which tags it touches. Openness exposes a
  `CrossReference` API. An agent asked to change a block cannot judge the blast radius without it.
- **Compile only what changed**, so the loop is seconds rather than a minute.

### 3. The gaps a real project will hit

- **Tag table writing.** We can export tag tables but not create or modify them. SCL that
  references tags is useless if the tags cannot be declared, and every real project needs them.
- **`DeleteBlock` and `RenameBlock`.** There is currently no way to remove a block an agent
  generated by mistake — the project only accumulates.
- **A companion XML export for LAD, FBD and GRAPH**, so a snapshot can be complete. Today a
  snapshot silently describes only the text-representable part of a program.
- **Watch and force tables**, which is how anyone actually debugs a running PLC.

### 4. Generality and longevity

This is what makes it last past this project and past V20.

- **No assumptions about the project.** Nothing in the server should know about four stations.
  The station pattern belongs in `spec/`, as data the agent reads, not as code.
- **A reusable pattern library** in `spec/`: SCL snippets, block templates, naming conventions.
  This is what turns "the agent writes SCL" into "the agent writes *our* SCL" on project after
  project.
- **TIA version independence.** V20 is hardcoded as the default today. V21 will arrive during the
  degree; the resolver already supports V17 to V20, so the version belongs in configuration and
  the version-specific behaviour behind a check, not scattered through the code.
- **Prompts that teach the workflow.** `McpPrompts` is inherited and generic. An agent that is
  told "retrieve, snapshot, write, compile, read errors, fix, snapshot again" will use the server
  correctly on the first attempt instead of the fourth.

### 5. Operability

- **A diagnostics tool.** The first session of this project was spent by hand checking group
  membership, `TiaPortalLocation`, .NET version, Openness versions and licences. That check should
  be one tool call, so the first thing an agent does on a new machine is find out what is missing.
- **Structured logging with levels**, all to stderr, since stdio is the transport.
- **Packaging**: a single-file executable and an install document, so this is installable rather
  than buildable. The `vscode-tiaportal-mcp` reference repo shows the extension route if it is
  ever wanted.
- **CI that builds and runs the analyzers** on GitHub Actions. The tests need TIA Portal and
  cannot run there, but a build that enforces the rules on every push still catches most of it.

### 6. Paying off the debt ledger

The `NoWarn` lists in both csproj files are meant to shrink, and one entry already has.

- `CS1591`: 424 public members without XML doc.
- `CA2254` (94) and `CA1062` (56).
- **Split `McpServer.cs` (90 KB) and `Portal.cs` (102 KB)** by functional area. Our own rule caps
  a class at 300 lines, and these are the two files that break it worst.
- Convert the 30 inherited response objects to immutable, so the file stops being half and half.
- **Offer the four upstream fixes as pull requests.** They are real bugs in a public MIT project,
  and contributing them back is also the cheapest way to stop carrying a private fork forever.

## Phase 8 — industrial communications

The degree covers PROFINET, PROFIBUS, OPC UA and MQTT, and the server should carry that weight
too. What follows separates what Openness genuinely supports from what it does not, because
promising the second kind would waste time later.

### What Openness supports well — network topology

This is the strongest ground and the most useful.

- **Read the topology**: devices, their interfaces, IO systems, subnets and addresses. The
  `NetworkInterface` service and its `Nodes` already gave us `192.168.0.1` while debugging the
  download, so the API is right there.
- **Create and connect**: add a device, assign its interface to a subnet, build a PROFINET IO
  system, set device numbers and addresses.
- **Assign IO devices to a controller**, which is the actual work of wiring a PROFINET cell.
- **Export and import the hardware configuration as text**, so the network layout goes into Git
  next to the code. Today `ExportSourceSnapshot` covers only the program: a snapshot that
  describes the software but not the network describes half a project.

A `GetNetworkTopology` tool matters beyond configuration: an agent asked to write code for a
distributed cell cannot address IO it cannot see.

### PROFIBUS — verified, not assumed

"It comes for free" was the guess; this is the check. **Openness models no separate DP master
system** — there is no `DpMasterSystem` type, only mode enums — so a PROFIBUS master system is
created through the very same `IoController.CreateIoSystem` call as PROFINET.

The fixture turned out to carry four `CP 5622` PROFIBUS cards, unconnected, at station address 2,
which made it testable here rather than in theory. `Test13IoSystem` 7/7 includes creating a DP
master system on one of them and confirming the card ends up on a subnet.

Two things the same dump exposed, both worth having:

- The topology reported a bare `type=16` — a net type value the published enum has no name for.
  `ToString()` renders that as a number, which reads like data rather than like a gap; it now
  renders as `Unknown(16)`.
- Several interfaces carry an IP and belong to **no subnet at all** (`PROFINET onboard_1` at
  192.168.0.2, among others). That is precisely the silent misconfiguration the topology tool was
  built to surface, showing up unprompted in a real project.

Still open here: PROFIBUS devices usually arrive as **GSD files**, and whether V20 exposes GSD
installation through Openness has not been checked.

### OPC UA — reading and exporting done

`GetOpcUaInterfaces` and `ExportOpcUaInterface`, over
`PlcSoftware.GetService<OpcUaProvider>()` → `CommunicationGroup.ServerInterfaceGroup.
ServerInterfaces`, where each interface has `Export` and `Import`. `Test14OpcUa` 4/4.

**The obvious candidate is a dead end and cost a detour.** `HW.Utilities.OpcUaExportProvider`
exists, is named exactly right, and has a method taking a `DeviceItem` and a file — and is
unreachable: no public constructor, and nothing in the API returns one. The reachable path is a
different type in a different namespace. Written down here so nobody follows the name again.

A server interface is the contract between the PLC and every client that talks to it. It is
configuration rather than code, which is exactly why it belongs in version control: changing it
breaks every client without touching a line of SCL.

Still to do here: enabling the server and publishing tags is configuration this server does not
write yet, and the verification half — connecting an OPC UA client to a simulated CPU and
asserting a published tag is readable — needs the download, so it is behind that open issue.

### MQTT

Different in kind, and worth being clear about: **an S7-1500 has no built-in MQTT server**. MQTT
means the PLC acts as a client, through a library such as LMQTT or a hand-written FB over the
open user communication blocks (`TCON`, `TSEND`, `TRCV`).

So MQTT is not a configuration feature here, it is **generated code** — which is precisely what
this server is already good at. The realistic shape is a `spec/` pattern for an MQTT client FB
that phase 3's `WriteScl` instantiates, plus a test that runs it on PLCSIM against a local broker
and asserts a message actually arrives. Claiming "MQTT support" as a checkbox would be dishonest;
generating and *verifying* an MQTT client is real.

### `GetNetworkTopology` — done

Reads every device interface in the project with its address and subnet, walking the device tree
recursively because devices nest: a rack holds a CPU which holds the interfaces. `Test12Topology`
3/3 passing; it finds `PLC_0` at `192.168.0.1` on `PN/IE_1`.

An interface attached to no subnet is **reported, not skipped**. An unwired interface is a common
and otherwise silent reason a download or an IO connection fails, and the tool's message counts
them explicitly.

This is also the answer to a question that cost real time during the download work: the CPU's
address had to be dug out by hand with a throwaway probe. Nobody should have to do that again.

Chosen as the next step after measuring that **PLCSIM tag access is by name and needs
`UpdateTagList()`, which reads the tag list from a downloaded program** — so reading and writing
tags is blocked by the same open issue, and is not the independent path it looked like.

### The network is in the snapshot

`ExportSourceSnapshot` now also writes `network/topology.txt`, so a snapshot describes the network
as well as the program. The same blocks addressing a device at a different address are a different
system, and nothing in the previous export would have shown it.

**Openness V20 exposes no hardware export** — no AML, no `Device.Export`, only
`ExportProjectTexts` — so the file is reconstructed from what can be read. It does not capture
module part numbers or rack layout; it captures what changes between revisions: which device sits
on which subnet, at which address.

Rows are sorted, and a test exports twice and compares. Line order that followed whatever order
TIA happened to enumerate devices in would produce phantom diffs, and phantom diffs train
everyone to ignore real ones.

Also found while looking: **`Siemens.Engineering.HW.Utilities.OpcUaExportProvider` exists**, which
is the entry point for the OPC UA work later in this phase.

### Order for the rest

1. ~~`GetNetworkTopology`~~ — done.
2. ~~Hardware configuration in the snapshot~~ — done, within the limits of the API.
3. ~~PROFINET IO system creation and device assignment~~ — done. `Test13IoSystem` 6/6.

   `NetworkConfigurator` creates an IO system on a CPU and attaches devices to it, over
   `IoController.CreateIoSystem`, `IoConnector.ConnectToIoSystem` and `Node.ConnectToSubnet`.
   The objects live in different places — a controller exposes an `IoController` on its
   interface, an IO device exposes an `IoConnector` on its own — and neither is reachable from
   the device itself, because a CPU's PROFINET interface is a *child* device item. Same nesting
   the topology reader walks.

   Two decisions worth keeping:

   - **An interface already on a subnet keeps it.** Rewiring a working network because a name
     did not match would be a destructive surprise.
   - **The backup is mandatory**, as with `WriteScl`. Rewiring a network without recording what
     it was is not undoable by reading the result.

   A test asserts that attaching the CPU to its own IO system is refused. That failure was found
   by writing the test wrong — a CPU is the controller, not one of its devices — and the code was
   right, so the test now documents the rule instead of the mistake.

   PROFIBUS uses the same three concepts, so this should carry over with little more than a
   different subnet type.
2. Hardware configuration in the snapshot, so the network is versioned with the code.
3. ~~PROFINET IO system creation and device assignment~~ — done.
4. ~~PROFIBUS~~ — done and verified: the same `IoController.CreateIoSystem` call, because
   Openness models no separate DP master system. GSD installation support is still unchecked.
5. OPC UA: listing and exporting server interfaces is **done**. Still to do: enabling the
   server and publishing tags, and verifying against a simulated CPU — the latter needs the
   download, so it sits behind that open issue.
6. MQTT as a generated, tested pattern in `spec/`. Not started, and it is code rather than
   configuration: an S7-1500 has no MQTT server, so the PLC is a client written in SCL.

## Available assets

- **`repos/tiaportal-mcp/tests/TiaMcpServer.Test/assets/TestProject1.zap20`** (3.6 MB) — a real
  TIA V20 project, usable as an immediate test bench. Solves the lack of a project of our own.
- Siemens SCE documents (free, in Spanish) — additional teaching projects.

## Design: the "station" pattern

A standard interface to instantiate four times. It is the core of the final project.

```
FB_Station
  IN : Start, Reset, Enable, ModeAuto, ModeManual
  OUT: Busy, Done, Error, ErrorId, Ready
  IN_OUT: PieceId          // traceability between stations
  STATIC: Step (internal sequence, GRAFCET)
```

An `FB_Coordinator` manages the handshake between the four instances: who holds the piece,
when it is released, and what station N does if station N+1 is in a fault state.
