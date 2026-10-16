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

`src/TiaMcpServer.Portable/Governance/`:

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

The API is `harness/src/apiServer.ts`, `npm run api`, and it was built first: it reads the SQLite
store the runs already write and serves runs, per-run detail, metrics, the filtered audit trail, the
gate and the mode banner. Read-only by construction and on loopback only — every write stays behind
the guard in the MCP server, and the dashboard confirms a change through `ApplyChange` like any
other client.

Four views: the plant copilot (chat plus live loop phase), metrics and charts, the
filterable audit log, and the state of the workshop gate with its five criteria shown green
or red. Plus the permanent mode banner described above.

**Closed 2026-08-27.** Six views shipped — Overview, Live run, Runs, Metrics, Audit trail, Workshop
gate — plus the banner, a dark theme, and a live stream that moves the page as a run records.

**The copilot's chat was cut on 2026-08-27 and shipped later the same day.** The cut is left here
rather than edited away: it needed a model and an API key the machine did not have, so it would have
been written and never run, and it was already cut number 1 on the list below. Its other half, the
live loop phase, shipped as *Live run*.

**The condition the cut named was met.** Twenty euros of API credit went into the account, and the
chat exists. It is **not a tab**: it is docked in the corner of every view at once, behind a round
robot button, because a copilot you have to navigate away from the numbers to reach is one you stop
asking - and the question somebody wants to ask is nearly always about the view in front of them.
The conversation survives moving between views, which is the property the dock is for. The estimate of about 1.5 cents a turn was pessimistic by an order of
magnitude: measured turns against the real store cost **$0.0019 to $0.0022** on Haiku 4.5, most of
it the brief that is sent with every question.

It is given the same numbers the views are drawn from and **no tools**, so there is no path from
anything typed into it to anything that changes a project. It declines safety questions outright -
verified against a question that insisted - and says when a fact was not given to it rather than
filling the gap. Still six views, plus the dock.

A sixth view, *Guide*, arrives with phase 5b rather than here: it renders `INSTALL.md` and reports
what this machine meets. It is listed there because a guide to installing something is worth
nothing until there is something to install.

Built on 2026-08-26 and 2026-08-27. What exists: Overview, Live run, Runs, Metrics, Audit trail,
Workshop gate, the permanent mode banner, the docked copilot, and a dark theme. React, TypeScript, Vite, Tailwind,
shadcn/ui and Chart.js. `npm run api` in `harness/`, `npm run dev` in `dashboard/`.

A run in progress moves on screen: `/api/live` streams server-sent events, which is where the
WebSocket half deferred out of phase 3 landed. The event says only *that* the store changed, never
what — the page re-reads the endpoints that already serve the numbers, so one path produces a number
and the stream cannot disagree with the table.

### Phase 5 — The pitch — **done, 2026-08-28**

README rewritten with the business pitch first, an end-to-end recording of the loop, the
real numbers from phase 3, the security model, and Workshop Mode documented as roadmap with
its entry conditions. Anything the repository does not actually do comes out of the README.

All five delivered. The recording is a real one: run 40 of the specification set, printed as it ran,
rather than a description of what a run would look like. The numbers are read from the store the
runs write and are the same ones `npm run gate` and the dashboard produce.

**Three claims came out because they were not true**, which was the part of this phase with teeth:

- *"Tag tables (`PlcTagTable`) — export/import"* was listed as something this adds. **There is no
  such tool and never was.** Tag tables reach Git inside `ExportSourceSnapshot` and nowhere else,
  and there is no import at all. The README now says that.
- A draft of the pitch put "an LLM writes PLC code" directly above the 96% and 67% figures, which
  are the **pattern expander's**. Read together they would have been taken as a model's score. The
  README now says which generator produced them before the table rather than after it.
- A draft priced a generation at *"$0.008 on Opus-class models"*. The $0.0079 measured was
  **Haiku 4.5**. The Opus figure is an estimate from the price ratio and is now marked as one.

One code comment was corrected with them: `McpServerWrites.cs` said *"everything here calls
`GuardedTool.Run`"*, and `ApplyChange` does not — correctly, since it is the confirmation half of
the guard. A file that exists to be checkable by eye cannot have an unstated exception.

### Phase 5b — Deployment: somebody else's machine

Added 2026-08-27. Numbered 5b rather than 7 so that Workshop Mode stays last, which is a rule rather
than a position in a list.

**The constraint that decides everything here: the server cannot be deployed anywhere except a
Windows machine that already has TIA Portal V20 installed and licensed.** Openness is an in-process
API against an installed Portal, so there is no container, no Linux host and no cloud. This is not a
service to host; it is a desktop tool to distribute. The unit of deployment is one person, one
Windows PC, and everything — server, harness, API, dashboard, knowledge index — runs on it.

That is also a property worth keeping rather than a limitation to engineer around: no audit trail
leaves the machine that produced it, and nothing phones home.

| Piece | Where it runs | What that machine must already have |
|---|---|---|
| `TiaMcpServer.exe` | The same PC as TIA Portal | TIA V20 licensed, .NET Framework 4.8, membership of `Siemens TIA Openness` with a re-login done |
| PLCSIM Advanced | The same PC | Its own licence, the virtual adapter configured on the project's subnet |
| `harness/` | The same PC, driving the server over stdio | Node 22.6 or newer |
| API and dashboard | The same PC, loopback only | Node. It serves the audit trail, which is why the interface is not a flag |
| Knowledge index | The same PC | Built locally from the recipe and the user's own documents |

What the phase delivers:

- **A versioned release artefact** — the built server, zipped, with its SHA-256 published. Byte
  identity matters more here than usual: TIA binds its Openness whitelist to the exact executable, so
  a rebuild costs every user a confirmation dialog. Releases are few and versioned for that reason.
- **A precondition check that fails closed.** A PowerShell bootstrap that verifies TIA at the
  configured location, the group membership, .NET, Node and PLCSIM, and **refuses with a sentence
  naming the fix** rather than letting the failure surface an hour later as something unrelated.
- **`INSTALL.md`, written for somebody who has never seen this repository**, including the first-run
  trap that is guaranteed to bite: TIA asks for whitelist confirmation the first time a given
  executable connects, and while that dialog is open `Connect` blocks and reports `Request timed
  out` — which points at the server when the cause is on the screen.
- **Host wiring** for Claude Code and for Claude Desktop: stdio, logs to stderr, one snippet each.
- **The user guide, in the dashboard and in the repository — one file, rendered twice.** A sixth
  view, *Guide*, renders `INSTALL.md` itself rather than a hand-written copy of it. The rule is the
  one the knowledge brief states about checklists and applies here for the same reason: a document
  that exists twice diverges, and the divergent copy is the one that gets shown. The file is what a
  new user reads on GitHub before anything of this is running; the view is what they keep open once
  it is, which is why neither can be dropped in favour of the other.
- **What this machine actually has, checked rather than described.** The one thing the Guide view can
  do that a Markdown file cannot: run the same precondition check as the bootstrap script and show,
  item by item, what this machine meets and what it does not — TIA at the configured location, the
  group membership, .NET, Node, PLCSIM, and whether an index and a metrics store exist. It reports;
  it never installs anything and never changes a setting. That keeps the API read-only in the sense
  that matters: it inspects, it does not act.
- **Configuration, never paths.** `TiaPortalLocation`, project locations and `policy.json` are
  per-machine, and the shipped policy denies by default.
- **A version the numbers can be attributed to.** The server reports its version; the harness already
  records the executable it measured through.

**Blocking criterion**: installed from the release artefact alone, on a machine that has never built
this project, the smoke path runs — connect, compile, download to PLCSIM, read a tag — **following
`INSTALL.md` and nothing else**. A virtual machine counts; a second copy of the developer's own
working tree does not. Until that has been done once, the install instructions are a guess, and a
guide nobody has followed from the top is a guide that is wrong in the place it matters most.

Explicitly out of scope: a central server, remote access to the dashboard, and any shared store.
Exposing the API beyond loopback publishes a record of everything the server changed and where every
backup lives, and that is a decision with consequences, not a deployment convenience.

## Covering what TIA Portal can do — phases 6 to 10

Added 2026-09-05, after an audit of what this server reaches against what the Openness API actually
offers. The measurement, not the impression: 59 tools against roughly 1,800 public types across some
sixty namespaces. The stated goal is now **all of it**.

What the server covers today is one stretch of the work: *write code, compile it, simulate it,
observe it*. What it barely touches is the stretch before that — building the station — and the one
after — talking to a real machine. An integrator does three things: configures, programs, and
commissions. This automates the second.

**Two of the gaps are not extensions of what exists**, and saying so is the point of this section:

- **Reading a live value from a physical PLC cannot be done through Openness at all.**
  `OnlineProvider` offers `GoOnline`, `GoOffline`, `State` and the master-secret calls, and nothing
  else. Watch and force tables are *offline* objects: Openness creates them and fills them in, and
  never reads what they hold on the machine. That capability needs an **OPC UA client**, or an S7
  communication library — a separate component speaking a different protocol, not another tool on
  this server. Checked against `Siemens.Engineering.dll` for V20, not assumed.
- **Commanding physical hardware is gated by phase 11**, and the gate is the reason this project
  exists rather than an obstacle in front of it.

Everything else in these phases is API that exists and is documented. It is work, not research.

### Phase 6 — Addresses, subnets and tag tables

The smallest of these and the one that removes a pain already felt. Today a download to PLCSIM only
works if the CPU's address in the project matches the virtual controller's, and **there is no tool
that sets an address** — it has to be typed into TIA Portal by hand.

- ~~Setting a node's address~~ — done: `SetDeviceAddress`.
- ~~Creating subnets and connecting interfaces to them~~ — done: `GetSubnets`, `CreateSubnet`,
  `ConnectDeviceToSubnet`. PROFIBUS came with them rather than after them, because a subnet's
  network type is taken from the interface instead of being a parameter.
- ~~Creating tag tables, tags and constants~~ — done: `GetTagTables`, `GetTags`, `CreateTagTable`,
  `CreateTag`, `CreateConstant`. A caveat measured while doing it: **Openness stores a data type
  that does not exist, and a software compile does not catch it**, because it compiles blocks and
  not tag tables. The tool description says so; the server does not refuse, because it cannot tell
  an unknown type from a valid one.
- ~~PROFINET device names~~ — done: `SetProfinetDeviceName`, and `GetNetworkTopology` now prints the
  name each node holds. It is not a second spelling of the address: a controller resolves the name
  over DCP at start-up, so a device whose project name differs from the one in the hardware never
  joins its IO system, at any address. Setting one turns off the automatic naming TIA does from the
  interface, because a name written while that is on does not survive.
- Editing an existing tag — changing a type or an address — is deliberately not here. Nothing in
  this phase overwrites; that is a separate decision with a backup story of its own.

### Phase 7 — Building the station

The largest gap and the largest namespace: `Siemens.Engineering.HW` holds 862 public types. Today a
device can be created from an order number and nothing can be done to it afterwards.

- ~~`HardwareObject.PlugNew`, `CanPlugNew`, `GetPlugLocations`~~ — done: `GetPlugLocations` reads a
  rack slot by slot, free and occupied in one list, and `PlugModule` puts a module in a free one.
  `CanPlugNew` is asked first, not out of politeness: `PlugNew` reports a refusal as a generic
  failure, and "the slot is taken" and "the rack does not accept this module" need different
  answers. Nothing is replaced, and plugging the same module twice reports the one that is there.
  The backup taken is the module layout — `hardware/modules.txt`, which a snapshot now carries too,
  because a program on a rack with a different input card is a different system.
- ~~Where a module lives in the process image~~ — done: `GetIoAddresses` reads the input and output
  ranges of a whole rack as `%I`/`%Q` spans, and `SetModuleAddress` moves one of them. Only the
  start moves; the length belongs to the module. `Address.Length` is **in bits** — measured, since
  the unit is in no signature — and `hardware/modules.txt` now carries the spans, because a backup
  taken before an address moves has to contain the address.
- ~~`PlugCopy`, `PlugMove` and `DeviceItem.Delete`~~ — done: `UnplugModule`, `MoveModule`,
  `CopyModule`. The decision unplugging needed: the backup restores slot, order number and
  addresses — enough to plug an identical card back, and the removal answers with that recipe — but
  **not** parameters set by hand in TIA Portal, which this server does not write and therefore
  cannot restore. The tool says so. A built-in item cannot be unplugged and the CPU is refused:
  removing it takes the program with it. Moving keeps the module and everything set on it, which is
  what makes it different from unplugging and plugging again.
- ~~Parameters through `SetAttribute`~~ — done: `GetDeviceParameters` lists what a device item can
  actually be told to do, and `SetDeviceParameter` sets one. The value arrives as text and is
  converted to the type the parameter already holds, so an enumeration is refused with the words it
  takes — spellings that appear in no documentation a model has read. A read-only attribute is
  refused before TIA is asked, which is the commonest mistake with an attribute bag. It also closed
  the gap `UnplugModule` had to admit to: a removal now records the module's parameters.
- Importing GSD/GSDML for third-party devices.

This is what turns the server from "it edits a project somebody else built" into "it builds one".

### Phase 8 — Knowing what uses what

- ~~`CrossReferenceService.GetCrossReferences`~~ — done: `GetCrossReferences` answers *who calls
  this block*, which is exactly the question to ask **before** rewriting it. For a loop where a
  model edits code it did not write, this is worth more than its size suggests. The answer comes in
  three lists: what uses the block and breaks if its interface changes, what the block uses, and
  every other relation Openness reports — because it has thirteen of them, and dropping the eleven
  that are neither would tell a caller that a block in use is used by nobody.
- Watch and force tables, created offline, belong here too.

### Phase 9 — Reading a real PLC, over OPC UA

Not Openness. A client that connects to the CPU's OPC UA server and reads variables from a machine
that is running.

It sits after phases 6 and 7 for a reason: reading a real machine is only useful once one can be
configured. It also has a precondition the server can already check — `GetOpcUaInterfaces` says
whether the CPU publishes anything at all, and an empty list means there is nothing to read until
somebody configures an interface.

**This reads. It does not command.** That distinction is the whole reason it comes before phase 11.

- ~~Browsing and reading a server~~ — done: `BrowseOpcUaServer` and `ReadOpcUaValues`, in their own
  assembly, `TiaMcpServer.OpcUa`, so their tests run on CI against an OPC UA server started in the
  test process. Three rules came with it, each with a test that fails when the rule is removed: the
  endpoint must be listed in the policy as `opcua/<host>:<port>`; an endpoint offering no message
  security is refused; and the first certificate a server presents is pinned, so a different one
  at the same address is refused until a person removes the pin.
- Measured against PLCSIM Advanced and a real S7-1500: not yet. Everything so far ran against the
  OPC Foundation's own server stack, which is not a Siemens CPU.
- Subscriptions, so a value can be watched rather than polled. Still reads; not started.

### Phase 10 — The rest of the surface

In descending order of what they give back for the work:

- **Graphical languages.** LAD, FBD and GRAPH have no authoring API: they are generated as SimaticML
  XML and imported. Expensive, and GRAPH is the worst of the three.
- Technological objects (axes, cams), alarms and text lists, libraries and master copies, project
  comparison, certificates and syslog.
- **HMI, classic WinCC (Basic/Comfort/Advanced)** — *decided 2026-10-13, and moved ahead of the rest
  of this phase*: the class panels are classic WinCC, and the user asked for generated screens.
  Measured that day: classic HMI has no object model for screen items in Openness, but screens, HMI
  tag tables and text lists export to XML and import back, linked by name, and a hand-written
  screen and tag table compiled with 0 errors. The work is MCP tools for that round trip, a
  generator, and a screenshot comparison against a reference. See `STATUS.md`, 2026-10-13.
- **HMI Unified**, roughly 400 types. It is the largest remaining surface and the one that gives a
  four-station cell the least. Doing it should be a decision, not momentum.

The whole list of what is still open, in priority order and with progress, is kept in `STATUS.md`
under "Everything still open"; what a company-grade tool needs beyond this roadmap is in
`ENTERPRISE-READINESS.md`.

### Phase 11 — Workshop Mode, supervised and last

Last, and only under supervision. Deliberately.

**It was phase 6 until 2026-09-05 and its number moved, not its position.** Five phases were
inserted before it, and the rule has never been a number: new code that commands physical machinery
is not the newest code in the repository, and it comes after everything else.

### The knowledge layer

`KNOWLEDGE-LAYER.md` holds a separate work brief — hardware documentation retrieval and a cited
pre-flight review, delivered as Claude Code skills. It is **deliberately not numbered as a phase
here**: it runs alongside this order and is cut from the bottom up, and every one of its seven
stages delivers something on its own.

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

1. ~~The live chat in the dashboard.~~ **Cut on 2026-08-27 and built on 2026-08-27**, once there
   was a key to build it against. The data views remain, which are the ones that cannot be
   faked. **Taken, 2026-08-27** — see phase 4.
2. The cell drops from four stations to two.
3. The harness uses pre-generated specifications instead of calling the model.

**Phase 1 is not cut.** It is the one that makes the following year possible.

## Completion plan — every open item, to the end

Written 2026-10-13, when the user asked for a plan that finishes **all** of the open technical debt
and work, not a selection of it. It orders everything in `STATUS.md` ("Everything still open") and
`ENTERPRISE-READINESS.md` into waves. **Nothing in it is optional and nothing is cut**; the cut
list above applies to the phases before it, not to this plan.

The rules of the rest of this document hold here too:

- **Order, not dates.** A wave ends when every item in it meets its "done when", and not before.
- **Done means measured.** An item that touches TIA Portal, PLCSIM or a CPU is done when it ran
  against the real thing, not when its tests pass against a double.
- **Each item ships the way everything ships**: on its own branch, tests that failed before the
  fix, `/code-review high` (a second round when it touches governance or security), `STATUS.md`
  updated, and a commit the user makes herself.
- **A wave may start before the previous one ends** when it depends on nothing in it. The
  dependencies are written next to each item; where none is written, there is none.
- Knowledge-layer stages and Workshop Mode keep their own gates: each starts on the user's explicit
  word, and Workshop Mode only with a supervisor present.

### Wave A — close what is in flight

| # | Item | Done when |
|---|---|---|
| A1 | ~~Edges in the LAD generator (`work/lad-edges`)~~ — merged as PR #43 | Reviewed, committed, merged. Already measured in TIA Portal |
| A2 | ~~Server 0.0.19 registered in Claude Code~~ — done; 0.0.20 is next (`work/release-0.0.20`) | `GetState` answers from 0.0.19 |
| A3 | ~~The server reports its own version~~ — done on `work/release-0.0.20`: `GetState` returns `serverVersion`, and the harness stores it in `runs.server_version` (schema 3) | `GetState` returns the assembly version; the harness records it next to the path |
| A4 | Work while the user has TIA Portal open. `Connect` attaches to `TiaPortal.GetProcesses().First()` (`Portal.cs`), whatever it holds: on 2026-10-15 it picked a leftover instance with no window while the user's window held the project, and `OpenProject` then failed because the project was locked. Prefer the process whose `ProjectPath` is the requested project, then one with a user interface; `OpenProject` on a project another instance has open attaches to that instance instead of failing; say which instance was chosen. Also: a server killed mid-session leaves the TIA Portal it started running with no window | With the project open in the user's TIA Portal window, a read, a guarded write and a compile through the MCP succeed and show up in that window, without the user closing anything; no instance is left behind after a session ends |

### Wave B — a class exercise from nothing to a compiled project

What the hopper exercise of 2026-10-08 could not do without a person in TIA Portal.

| # | Item | Done when | Depends on |
|---|---|---|---|
| B1 | MCP tools `CreateProject` and `AddDevice` — merged in PR #44 and tested in TIA Portal (`Test39`); the S7-1200 round trip below is still to run | A new S7-1200 project is created, saved and reopened through the MCP; refusal tests in `Test16GuardedWrites` | A2 |
| B2 | MCP tool `DeleteBlock`, with a backup first — merged in PR #44 and tested in TIA Portal (`Test39`); restoring from the backup is still to run | A superseded block is removed and its backup restores it | A2 |
| B3 | Global `IEC_TIMER` DB generated with the program | The hopper program compiles with no DB written by hand | |
| B4 | Calls from Main generated | The generated program runs in PLCSIM without editing Main | |
| B5 | Network and block titles and comments through `.s7res` | A generated block shows its titles in TIA Portal | |
| B6 | One block per GEMMA state, as the class writes it | The hopper exercise comes out in the teacher's block layout | C-GEMMA below |
| B7 | S7-1500 `time_type` template on TON calls | The same chart compiles on S7-1200 and S7-1500 | |
| B8 | The whole hopper exercise regenerated end to end through the MCP | 0 errors, the 13 PLCSIM test cases pass, nothing added by hand | B1–B7 |

### Wave C — the `grafcet` skill and GEMMA

| # | Item | Done when |
|---|---|---|
| C1 | GEMMA model: states used, evolutions with conditions, signalling, the master step of each | The 0968 GEMMA is written in it without loss |
| C2 | GEMMA checks: every used state reachable, D1 reachable from every state, a way back to A1 | Each check has a test that fails without it |
| C3 | GEMMA drawn in the standard layout — on 2026-10-15 drawn once by a throw-away script outside the repository (the A, F and D zones, every state with the used ones solid, each evolution with its condition, no crossing lines); the layout is proven, the code is not in the skill | The 0968 guide drawn by the skill from its GEMMA model, as clean as the 2026-10-15 drawing |
| C4 | Labels no longer overlap on several outgoing transitions; wide charts not cut — the same script showed the fix: jumps drawn as renvois (an arrow with the target step), `/SE → 13` as a short branch on the left of each step | The 0968 master chart draws cleanly from the skill |
| C8 | The teacher's style, as drawn in his notebook on 2026-10-15: the selection remembered by the lamps with `S`/`R` (steps that last only while the button is held), stored actions drawn as a row of `S`/`R` boxes, production branching on the lamp | The skill reads, draws and generates the 0968 selection in that style; the hand-edited program of 2026-10-15 comes out of the generator unchanged |
| C5 | Forcing orders (`F/G_PROD:{30}`) in the notation, the checker and the generator | The hierarchical 0968 design is checked whole |
| C6 | `transient-evolution` false positives | The 11 warnings on 0968 are gone and the true case still warns |
| C7 | The reader understands DB-member TON calls | No "unsupported" warning on the hopper program |

### Wave D — HMI, classic WinCC

The panels used in class (`6AV2 124-0JC01-0AX0`) are classic WinCC; the round trip was measured on
2026-10-13.

| # | Item | Done when | Depends on |
|---|---|---|---|
| D1 | Read tools: export screens, templates, HMI tag tables, text and graphic lists — `GetHmiInfo`, `ExportHmi` merged in PR #44, tested on a new panel; the teacher's project through the MCP is still to run | The teacher's project exported through the MCP, read-only | A2 |
| D2 | Write tools: import screens, HMI tag tables and text lists, guarded, with a backup — `ImportHmiScreen`, `ImportHmiTagTable`, `ImportHmiTextList` merged in PR #44, tested in TIA Portal, links checked before every import | A screen imported through the MCP compiles; refusal tests in `Test16GuardedWrites` | D1 |
| D3 | HMI tags generated from PLC tags — `GenerateHmiTags` merged in PR #44; on a panel with a PLC connection it is measured only by the 2026-10-13 probe | Every memory bit of a program gets its HMI tag, and the HMI compiles | D2 |
| D4 | Screen generator: buttons, pilot lights, I/O fields, bars, screen navigation — `GenerateHmiScreen` built 2026-10-14, with animations on every item, button and field events, symbolic and date/time fields, text lists; offline, two class screens match their exports but for images and groups; not yet imported into TIA Portal | The teacher's `principal` screen regenerated from a description, attribute for attribute | D3 |
| D5 | Documentation read and cited: WinCC Comfort/Advanced, the Openness HMI chapter, the tutorials named by the user | Each fact the generator relies on carries its source | |
| D6 | Runtime screenshot compared with a reference picture — the XML half exists (`CompareHmiScreens`); the runtime picture does not | A generated screen and the teacher's differ by less than a threshold fixed before measuring | D4 |
| D7 | ISA-101 style as an option (grey background, colour only for abnormal states) | One flag switches the generated screen to it | D4 |
| D8 | Simulating the panel against PLCSIM, so a button press reaches the PLC | Pressing MARXA on the simulated panel sets `Marxa` in PLCSIM | D4, E1 for S7-1200 |

### Wave E — simulation for the class hardware

| # | Item | Done when |
|---|---|---|
| E1 | Classic S7-PLCSIM V20 driven by the MCP (undocumented gRPC; measure, never guess) | An S7-1200 class project is downloaded and a tag read back |
| E2 | The hopper test cases run on the S7-1200 original, not the S7-1500 stand-in | The 13 cases pass on S7-1200 |

### Wave F — somebody else's machine

| # | Item | Done when |
|---|---|---|
| F1 | Phase 5b blocking criterion | Installed on a machine that never built the project, following only `INSTALL.md`, up to reading a PLCSIM tag |
| F2 | Web platform phase 4 | The teacher signs in from another machine and reads a project |
| F3 | Web platform phase 5: descriptions, images, snapshots, metrics, the diff | Each one shown for a real project |
| F4 | TIA Portal V21 | The test suite passes against V21, as the institut runs it |
| F5 | A user guide | A classmate completes an exercise with it and without help |

### Wave G — technical debt and audit findings

| # | Item | Done when |
|---|---|---|
| G1 | Audit F5, second half: pure rules out of `SclBlockGenerator` and `SourceSnapshotExporter` | Those rules tested in `TiaMcpServer.Portable`, without TIA Portal |
| G2 | Audit F8–F12 (inherited ledger, one Node process per write, the retrieved-data marker, a TypeScript linter, the commented property in `Responses`) | Each closed with its test or its CI check |
| G3 | The `NoWarn` ledger in `TiaMcpServer.csproj` emptied | The list is gone and the build is still warning-free |
| G4 | Methods over 30 lines | None left, except where `CLAUDE.md`'s note on rigour is argued in the code |
| G5 | Dependency scan and SBOM in CI | CI fails on a known vulnerable package; each release carries an SBOM |
| G6 | Project content always treated as data by the model | A test feeds an instruction hidden in a block comment and the model does not follow it |

### Wave H — the rest of TIA Portal

| # | Item | Done when |
|---|---|---|
| H1 | GSD/GSDML devices (left from phase 7) | A third-party device from a GSDML file is plugged and compiled |
| H2 | OPC UA measured against PLCSIM Advanced and a real S7-1500; subscriptions | A value read and watched from both |
| H3 | FBD and STL imported as documents; S7-GRAPH on S7-1500 | One block in each compiles |
| H4 | Alarms (program alarms, ProDiag) and text lists | An alarm raised in PLCSIM appears on the panel |
| H5 | Technology objects (axis, PID) | One of each configured and compiled |
| H6 | Libraries, types and master copies | A type is created, versioned and instantiated |
| H7 | Project compare and archive (`.zap`) | Two versions compared; an archive retrieved back |
| H8 | WinCC Unified | A Unified screen created through its object model and compiled |
| H9 | SINAMICS Startdrive | A drive added and parameterised |

### Wave I — what a company will audit

| # | Item | Done when |
|---|---|---|
| I1 | Four eyes: a plan confirmed by a second person | A write waits until another account confirms it in the web platform |
| I2 | Knowledge layer stages 0, 2, 2b, 4 and 5, each on the user's word | Each meets the "done when" in `KNOWLEDGE-LAYER.md` |
| I3 | A local model for customers who cannot send code out | The harness gate runs against it and its rate is recorded |
| I4 | Style guide check of generated code (Siemens programming style guide) | Findings reported on the hopper program |
| I5 | IEC 62443 self-assessment | A written gap list against the standard's requirements |
| I6 | Product law review (Cyber Resilience Act, Openness terms, AI Act, MIT notice) | Written answers, from someone qualified to give them |
| I7 | Support process: how bugs are reported, fixed and released | Written, and used once |

### Wave J — Workshop Mode, last and supervised

Phase 11, unchanged: the five measured conditions, an in-person design review, and a teacher or
workshop supervisor present with the emergency stop in reach. It comes after everything above
because new code that commands machinery must not be the newest code in the repository.

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
