# Roadmap — from "it works" to "it is trustworthy"

> Agreed on 2026-08-17. Companion to `STATUS.md`, which records where the work stopped;
> this document records where it is going and why.

The goal is a system that is **safe, measurable and demonstrable**, in that order. Where
those three pull against each other, safety wins without discussion.

## The starting point, stated honestly

Three facts about the repository as of 2026-08-17, because a plan built on the README's
claims rather than on the code would be planning for a different project.

1. **The loop stops before PLCSIM.** `DownloadToSimulation` fails with
   `Connect to module PLC_0 failed.` The verified loop today is
   `spec → SCL → import → compile → read errors → fix → snapshot to Git`. Everything past
   the download — running a program, driving inputs, asserting outputs — is written but
   unproven. The consequence for this roadmap is direct: **the headline metric, "percentage
   of specifications that pass their tests on PLCSIM Advanced", cannot be measured until
   phase 0 succeeds.**

2. **There is no station pattern.** `spec/` and `export/` are empty directories.
   `SclBlockGenerator.cs` is the external-source engine behind `WriteScl`, not a pattern
   generator. So the multi-station work is a prerequisite of the metrics work, not a
   follow-up to it: a repeatable specification set needs something to generate.

3. **Workshop Mode is impossible by construction, and that is the most valuable property
   this repository currently has.** `SimulationDownloader` selects only the PLCSIM virtual
   adapter and refuses when it is absent. There is no `DownloadToHardware`, no selection of
   a physical adapter — the path to real machinery does not exist in the code. The design
   question is therefore not "how do I add Workshop Mode safely" but **"how do I build
   everything else without destroying the property I already have".**

## Two modes of operation

### Study Mode — PLCSIM Advanced, always the default

Where the work happens: generate, compile, test, iterate, break things, learn from the
errors. The worst case is a failed simulation. The audit trail and the metrics still apply.

### Workshop Mode — physical hardware, exceptional

Reserved for the coursework of the following academic year, on the institute's machinery.
A badly commanded industrial machine can injure a person or destroy expensive equipment.

**Workshop Mode is not implemented in this roadmap, deliberately.** It is designed,
documented, gated, and left unwritten. The reasons are in "The gate" below, but the short
version is that a portfolio to finish and new code that commands physical machinery must never
be the same piece of work.

**Workshop Mode may only be used with a teacher or workshop supervisor physically present
and with access to the emergency stop.** No software enforces this. It is a requirement of
the project and it is stated here, in the README and in `CLAUDE.md` so that it cannot be
mistaken for an afterthought.

### Five layers, not a flag

An environment variable that can be left switched on from yesterday is the exact failure
mode to avoid. Defence in depth, outermost first:

| Layer | Mechanism | What it prevents |
|---|---|---|
| 0. Compile time | Hardware code lives behind `#if WORKSHOP_MODE`. The default build does not contain the capability. | That the binary used daily could ever reach a machine, whatever the configuration says. |
| 1. Startup | That separate binary still starts in Study Mode. Entering Workshop Mode needs an explicit CLI argument **and** a confirmation phrase typed by a person in that session. Never persisted; session TTL with automatic reversion. | Finding it still enabled from a previous session. |
| 2. Per action | Every write produces a plan covering exactly **one** action, with a human-readable code that must be confirmed. No batching, no "apply all". | A single confirmation covering more than what was reviewed. |
| 3. Whitelist | Deny by default, and no wildcards permitted in the Workshop section of the policy. | An unforeseen tag slipping through a `*`. |
| 4. Audit | In Workshop Mode, if the audit record cannot be written the action is **refused** (fail closed). In Study Mode it is reported and the action proceeds. | Acting on a machine without leaving a trace. |

Layer 0 has a precedent in this repository: `PLCSIM_AVAILABLE` in
`src/TiaMcpServer/TiaMcpServer.csproj` already gates a capability at compile time and
degrades cleanly when it is absent, so the mechanism is known to work here.

### One execution path, always

Dry run is **not** disableable, in either mode. A branch that executes without a plan would
exist in the Workshop binary too, and an untested branch is the one that eventually runs
with a machine connected.

Every write creates a plan and is recorded. What differs between modes is only **who
confirms the plan**:

- **Study Mode:** whitelisted operations are auto-confirmed immediately. Fast, unobtrusive,
  and still fully audited.
- **Workshop Mode:** human confirmation is mandatory, one per action.

Same code, same audit trail, a single place that has to be correct.

### The gate

Workshop Mode is not enabled "because it is time", but because the data says it is
reasonable. The criteria are fixed now, in the cold, and are all answerable by a query
against the metrics database rather than from memory:

1. At least **50 complete loop runs** recorded in Study Mode.
2. **Zero silent failures**: every audited operation has an explicit outcome; none ends in
   an unknown state.
3. **Complete audit**: every recorded write has a locatable backup on disk. Checked by a
   script, not by inspection.
4. A stable clean-compilation rate across the **last 20 runs**.
5. An in-person design review with the supervising teacher before a single line of
   Workshop code is written.

`harness/src/gate.ts` evaluates these five and answers yes or no. The metrics work is
therefore not decoration for the portfolio: **it is the instrument that opens the workshop
door.**

## Architecture

The MCP server is stdio on .NET Framework 4.8, where ASP.NET Core does not run, so a web
front end cannot hang off the executable. It also must not: a browser tab is a control
surface that can be open on another machine.

```
+-----------------+   WebSocket / REST  +--------------------+
|  React + TS     |<------------------->|  Node + TS backend |
|  (dashboard)    |                     |  = MCP client      |
+-----------------+                     +---------+----------+
                                                  | stdio (MCP)
                                        +---------v----------+
                                        | TiaMcpServer (.NET)|---> TIA Portal / PLCSIM
                                        +---------+----------+
                                                  | writes
                                        +---------v----------+
                                        | SQLite: audit,     |
                                        | runs, metrics      |
                                        +--------------------+
```

The Node backend is the MCP client, orchestrates the loop, and emits phase events. The
front end never touches TIA Portal. In Workshop Mode the dashboard is **read only**: it
shows what is pending confirmation, and the confirmation happens at the physical console
where the person with access to the emergency stop is standing.

The mode travels in every server response. When the connection is stale the dashboard shows
**`MODE UNKNOWN`** in red — never the last value it saw. An indicator that keeps showing a
comfortable green after losing contact is worse than no indicator.

### One repository

`harness/` and `dashboard/` live in this repository rather than beside it. The MCP tool
surface and the SQLite schema are contracts shared between C# and TypeScript, and they have
to change in the same commit. The workshop gate depends on the audit and the metrics
agreeing with each other; split across repositories, "the history is clean" would depend on
which commit of each you looked at.

### The governance layer

A new layer, respecting the existing dependency rule:

```
McpServer  -->  Governance  -->  Portal  -->  Openness
```

`src/TiaMcpServer/Governance/`:

| File | Responsibility |
|---|---|
| `OperationMode.cs` | `Study` or `Workshop`. Immutable for the lifetime of a session. |
| `ModeGate.cs` | The single source of truth for the current mode. In the default build `Workshop` is unreachable and says so clearly. |
| `WritePolicy.cs` | Answers `IsAllowed(target)` for the current mode. |
| `WritePolicyFile.cs` | Loads `.tia-mcp/policy.json`: allowed and denied block and tag patterns, per mode. Wildcards rejected in the Workshop section. |
| `ChangePlan.cs` | An immutable single-action plan: `PlanId`, target, value, backup reference, expiry. |
| `ChangePlanStore.cs` | Holds pending plans and expires them. |
| `AuditTrail.cs` | Append-only SQLite writer. Fail closed in Workshop, fail open with a warning in Study. |
| `AuditEntry.cs` | Immutable record: timestamp, tool, target, value, outcome, origin, `PlanId`, backup. |
| `GuardedWrite.cs` | The single point every write passes through. |

In the MCP layer: `ApplyChange(planId)`, `GetOperationMode()`, and `McpServerWrites.cs` —
splitting only the write tools out of `McpServer.cs`, not restructuring all 2415 lines of it.

The mandatory `backupDirectory` parameter on `WriteScl` and `CreateIoSystem` is replaced by
a configured backup registry: one root, timestamped, listable. A backup the caller can
forget to ask for is not a backup.

## Prior art

Checked on 2026-08-17, because a README that implicitly claims to be first collapses the
moment anyone searches.

**T-IA Connect** is a commercial MCP bridge for TIA Portal V17–V21 claiming 316 endpoints
across 22 categories, SCL/LAD/FBD/GRAPH generation, download with manual confirmation, and
PLCSIM Advanced. The open-source field is thinner: `heilingbrunner/tiaportal-mcp` — the base
this repository forked — is read and export only with no SCL generation and no PLCSIM;
`gangsterke/Tia-Portal-MCP-server` is twelve read-only tools; `cadugrillo/s7-mcp-bridge`
talks to a PLC directly with no engineering capability and no safeguards.

Two things follow, and neither is discouraging.

**Tool count is the wrong axis.** 45 against 316 is a comparison that measures nothing about
whether generated PLC code is correct.

**Their "Validation & Metrics" is server observability** — diagnostics, API-call auditing,
health checks — not reliability of the generated code. Nobody in this field publishes how
many iterations an LLM needs to reach a clean compile, or what fraction of specifications
pass their tests. Phase 3 is therefore not a smaller version of what exists; it asks a
different question, and it is the one that has to be answered with data rather than claims.

Note on sourcing: the comparison table above is published by T-IA Connect, one of the
products being compared. Their own figures are marketing until independently verified.

### Borrowed deliberately

Three concepts worth taking, agreed on 2026-08-17:

- **Asynchronous jobs.** A compile or a download takes minutes, and an agent blocked on one
  is useless. A download once hung this project for thirteen hours. Long operations should
  return a job handle that can be polled and cancelled. Folded into phase 1, since that is
  where the write path is redesigned anyway.
- **Watch and force tables.** How anyone actually debugs a running PLC, and the natural
  companion to reading and writing tags on a simulated controller.
- **Tag table import from CSV and Excel.** Real projects define their tags in spreadsheets.
  SCL that references tags is useless if the tags cannot be declared.

## Phases

### Phase 0 — Unblock the download to PLCSIM

Nothing downstream works without it: no tests, no reliability metrics, no end-to-end
recording.

The cheapest lead is already recorded in `STATUS.md`: `Configuration.IsConfigured` stays
`False` through every attempt, which suggests the connection has to be **established by
navigating** the configuration (`Modes.Find`, `PcInterfaces.Find`, `TargetInterfaces.Find`)
rather than assembled by passing leaf objects to `Download`. The subnet correction
(`PN/IE_1` rather than `1 X1`) is described in the code comments but **is not what the code
currently does** — `ResolveSimulationTarget` still returns a target interface.

The API surface is verified by reflecting over the real `Siemens.Engineering.dll` before any
hypothesis is acted on, as was done for `ProjectComposition.Retrieve`. Measure the system,
do not reason about it.

**Timebox: two sessions.** If it does not fall, the fallback runs without further debate:
metrics are limited to "compiles / does not compile", the README says so plainly, and the
download moves to the roadmap. The six twenty-minute cycles of August are not repeated.

Touches `SimulationDownloader.cs` and `Test11Download.cs`.

### Phase 1 — Governance and modes

The layer described above, plus the documentation of the security model in the README and
`CLAUDE.md`, including the supervision requirement.

**What this phase does not do: write a single line that downloads to hardware.**

### Phase 2 — FB_Station and multi-station — **done, 2026-08-18**

In `spec/`, as data rather than as server code:

```
spec/patterns/station.scl.tmpl       FB_Station
spec/patterns/coordinator.scl.tmpl   FB_Coordinator
spec/cells/two-station-demo.json
spec/cells/four-station-cell.json
```

`FB_Station` is the interface already designed in `STATUS.md`: `IN Start, Reset, Enable,
ModeAuto, ModeManual`; `OUT Busy, Done, Error, ErrorId, Ready`; `IN_OUT PieceId`.

The minimal honest coordination is the `Ready` / `Done` / `PieceId` handshake between
station N and N+1, and what N does when N+1 is in a fault state. **Two stations that work
are worth more than four in a diagram.**

In `src/`: `SclTemplateExpander.cs`, which instantiates a template from a JSON
specification. No knowledge of any particular cell inside the server.

### Phase 3 — Metrics and harness

`harness/`, Node and TypeScript, outside the solution:

```
harness/src/mcpClient.ts   MCP client over stdio against TiaMcpServer.exe
harness/src/loop.ts        generate -> write -> compile -> read -> fix, bounded
harness/src/generator.ts   SCL generation through the Anthropic API
harness/src/telemetry.ts   phase events to SQLite (WebSocket deferred to phase 4, see below)
harness/src/gate.ts        evaluates the five workshop criteria
harness/src/run.ts         CLI: runs the whole specification set
harness/specs/             5-10 cases of increasing complexity
```

Shared SQLite schema with the audit trail: `runs`, `iterations`, `phase_timings`, `audit`.

**The WebSocket half was deferred to phase 4 on 2026-08-26, on the user's decision.** Its only
consumer is the dashboard, and this repository has now been bitten three times by code that was
written, never executed, and believed. SQLite already records everything the stream would carry, so
the deferral costs the dashboard nothing but a reader it has to write anyway.

Outputs: mean iterations to a clean compilation per specification, percentage passing on
PLCSIM, and time per phase. Reported with the sample size attached — `n=10, 3 repetitions`,
never a bare percentage.

### Phase 4 — Dashboard

`dashboard/`, React, TypeScript and Vite, against the harness API.

Four views: the plant copilot (chat plus live loop phase), metrics and charts, the
filterable audit log, and the state of the workshop gate with its five criteria shown green
or red. Plus the permanent mode banner described above.

### Phase 5 — The pitch

README rewritten with the business pitch first, an end-to-end recording of the loop, the
real numbers from phase 3, the security model, and Workshop Mode documented as roadmap with
its entry conditions. Anything the repository does not actually do comes out of the README.

### Phase 6 — Workshop Mode, supervised and last

Last, and only under supervision. Deliberately.

## Order, not dates

The phases run in the order they are numbered. **There is no calendar**, and its removal on
2026-08-18 was deliberate: the one that used to be here made a phase look finished when its week
ran out rather than when its deliverables were done. Phase 1 was read as finished with three of
its deliverables missing, which is exactly the failure a calendar invites. **A phase ends when
what it promised exists and is tested**, and nothing else ends it.

Sequence is still real, and it is the only scheduling claim made here: nothing downstream can be
measured before the download works (phase 0), nothing may write to a project before the guard
exists (phase 1), and Workshop Mode comes last because new code commanding physical machinery
must not be the newest code in the repository.

### What gets cut if something has to be, in this order

1. The live chat in the dashboard. The data views remain, which are the ones that cannot be
   faked.
2. The cell drops from four stations to two.
3. The harness uses pre-generated specifications instead of calling the model.

**Phase 1 is not cut.** It is the one that makes the following year possible.

## Decisions taken

Agreed on 2026-08-17.

| Decision | Reason |
|---|---|
| `ApplyChange(planId)` rather than the MCP SDK's elicitation | Works with any client, records the plan before it executes, and becomes a natural button in the dashboard. |
| Harness and backend in Node and TypeScript | Avoids solving HTTP hosting on .NET Framework 4.8, and the front end never shares a process with the machinery. |
| One execution path; dry run is not disableable | A "skip the checks" branch would exist in the Workshop binary too, and untested branches are what eventually run. |
| Dashboard read-only in Workshop Mode; confirmation at the physical console | A browser tab can be open on another machine. Confirmation belongs where the emergency stop is. |
| Workshop Mode is written last, after every other phase | Pressure to finish and new code commanding physical machinery must not coincide. Originally phrased as "not before October"; the date went with the calendar on 2026-08-18, the order did not. |
| Phase 0 timeboxed to two sessions with an automatic fallback | Authorised in advance so the decision is not taken while inside the problem. |
| `harness/` and `dashboard/` in this repository | The MCP tool surface and the SQLite schema are shared contracts that must change in one commit. |
| The pattern expander is the main generator; the model one is written and deferred (2026-08-26) | The API is billed separately from a Claude subscription, and cut 3 below already allowed pre-generated specifications. It is also the better engineering: the expander is deterministic — same cell, same SCL, same diff — and a model is not, which is the wrong property for code that may one day command a station. `--generator model` is written and tested against a double, and costs a key and a command whenever there is a reason. |
