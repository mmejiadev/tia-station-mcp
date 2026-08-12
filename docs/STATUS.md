# Project status

> Living document. Update it at the end of every working session.
> Last updated: **2026-08-12**

## ▶ RESUME HERE

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
13. ➡️ **Next action:** phase 4, PLCSIM Advanced, or the `FB_Station` pattern itself. There is now
    a working loop to build either on.
14. ⏳ **Note:** the upstream clone still holds the same three fixes on
    `fix/portal-lifecycle-and-errors`, uncommitted, worth offering as a PR. There is now a fourth
    fix — the project not being closed on disconnect — that upstream also needs.

Required reading at startup: this file, `../CLAUDE.md` and `REFERENCE-REPOS.md`.

## Goal

An MCP server that generates, verifies and deploys PLC code for the final project of the
CFGS in Industrial Automation and Robotics: **coordination of 4 stations**
(palletizing, plotting, counting, robots).

Deadline: **six weeks** from 2026-08-12 → target ~2026-09-25 (start of classes).

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
| 4 | PLCSIM Advanced integration → automated tests | ⬜ |
| 5 | "Station" pattern generator from a specification | ⬜ |
| 6 | Documentation, demo, project report | ⬜ |

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
