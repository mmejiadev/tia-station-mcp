# Roadmap — from "it works" to "it is trustworthy"

> Agreed on 2026-08-17; reorganised on 2026-11-22 as what is built, what is missing, class material and reference.
> Companion to `STATUS.md`, which records where the work stopped; this document records what exists, what does not and why.

The goal is a system that is **safe, measurable and demonstrable**, in that order. Where
those three pull against each other, safety wins without discussion.


## How to read this document

It has four parts, in this order:

1. **What is built** — everything that exists and was run, by area, each with what it is and what it settled.
2. **What is missing** — everything that has been asked for or planned and does not exist yet, by area, each with its
   "done when". Nothing in it is optional and nothing has a date.
3. **Class material** — the exercises of the course and the corpus around them. They are useful as test cases and as
   knowledge, and they are **not** part of the construction of the server: no item in part 2 depends on one.
4. **Reference** — the starting point, the two modes, the architecture, the prior art and the decisions taken. They did
   not change; they were moved here so the plan reads as built / missing.

Where each fact is recorded in full is `STATUS.md`, by date; `HMI-COVERAGE.md` is the checklist for screens;
`KNOWLEDGE-LAYER.md` is the brief for the knowledge layer; `PEEK-POKE.md` and `WEB-PLATFORM.md` are the two measured
studies. This file says what exists and what does not, and `STATUS.md` says how it got there.

The phase numbers (0 to 11) and the waves (A to L) that `STATUS.md` and the code comments cite are those of the previous
version of this file, in git history before 2026-11-22; they are not renumbered here.

**State on 2026-11-22:** 179 MCP tools; three assemblies (`TiaMcpServer.Portable`, `TiaMcpServer.OpcUa`, `TiaMcpServer`);
1,387 governance tests, 133 specification tests and 33 OPC UA tests that run on any machine, and an Openness suite of
several hundred tests that needs TIA Portal. The development PC is on **TIA Portal V21** and PLCSIM Advanced V8; V20 is
uninstalled and still builds with `-p:TiaVersion=20`.

**Rules that hold for everything below**, carried over from the earlier roadmap:

- **Order, not dates.** An item ends when what it promised exists and is tested, and not before.
- **Done means measured.** An item that touches TIA Portal, PLCSIM or a CPU is done when it ran against the real thing,
  not when its tests pass against a double. Anything built and never run counts for less than half.
- **Every item ships the same way**: its own branch, a test that failed before the fix, `/code-review high` (and a second
  round when it touches governance or security), `STATUS.md` updated, and a commit the user makes herself.
- **Workshop Mode comes last**, under supervision, whatever else is added: new code that commands machinery must not be
  the newest code in the repository. The knowledge layer's stages each start on the user's explicit word.

---

# Part 1 — What is built

## 1.1 Governance: nothing writes without a plan, a whitelist, a backup and an audit entry

**What it is.** Two operation modes (Study on PLCSIM Advanced by default; Workshop for physical hardware, unreachable in
the default build). Every change is a single-action plan, decided by a deny-by-default policy file, confirmed by
`ApplyChange`, backed up first into a registry the caller cannot forget, and appended to an audit trail that is never
rewritten. One execution path: `GuardedTool.Run`, with targets built in one place (`ChangeTarget`). Jobs (`runAsJob`) for
the calls that take long. `OpennessGate` so only one Openness call runs at a time. A single error model
(`PortalException`, `Result` for expected refusals).

**Conclusion.** The absence of a decision is a refusal, never a permission. `Test16GuardedWrites` runs every write tool
under a policy that does not exist and asserts each one refuses, which is the property that matters and the one a new
tool can silently lose; it found two real defects the first time it ran. A job once reported success for work that never
happened, and two Openness calls really do interleave: both are covered by tests now.

## 1.2 The shape of the code, and the rule that is a build error

**What it is.** Three assemblies: the portable core (governance, knowledge, specifications, jobs, the error model), the OPC
UA client, and the Openness adapter. The portable core cannot reference the adapter, so the safety tests run on any
machine, with no TIA Portal. Openness types never cross into the MCP layer. `McpServer`, `Portal`, `McpServerWrites` and
the responses were split by area (one class per file); continuous integration runs the portable suites.

**Conclusion.** A rule that depends on being remembered stops holding; the split makes the dependency rule fail the build.
The first CI run proved both of us wrong in two different ways (2026-09-05), which is why it was worth having.

## 1.3 Getting a program onto PLCSIM Advanced (phase 0, closed)

**What it is.** Compile, download to a PLCSIM Advanced virtual controller and reach RUN, through `CompileSoftware`,
`CompileHardware`, `EnableSimulationSupport`, `DownloadToSimulation`, `DescribeSimulationConnection` and the simulation
instance tools (create, start, stop, delete, list, read and write tags). `UseTcpIpNetworkMode` puts the runtime where TIA
Portal can find a controller.

**Conclusion.** The download that blocked the project for thirteen hours was **six faults, not one**, found by fixing the
diagnostics first and not by reasoning: an exception that lost its message across an app domain, a controller that no
longer existed because nothing held it, and a discovered device that kills the process if it is passed to `Download`.
Measured rules came out of it: one download per open project and address, and the TCP/IP mode must be set in the process
that uses it.

## 1.4 Cell patterns (phase 2, closed)

**What it is.** `FB_Station` with a fixed interface and a handshake, a coordinator and a handover expander generated from a
specification written as data; `ExpandCellScl` returns the SCL without touching a project. A four-station cell exists as
three specifications.

**Conclusion.** A numbered piece goes through two stations in PLCSIM, asserted by a test and not watched by hand
(`Test20CellRuns`). The template language is deliberately not a language (two constructs, no conditionals), which is what
keeps the output deterministic: same cell, same SCL, same diff.

## 1.5 The harness (phase 3, closed)

**What it is.** `harness/`, Node and TypeScript: generate, write, compile, download, start and check the cell, with a
SQLite store that records every run, the path it took and the server version it measured through. Workshop gate and
specifications included. The model generator is written and was deliberately left unexecuted (the API is billed apart from a
subscription); the pattern expander is the main generator.

**Conclusion.** The first rate was reported with its sample size: 18 of 18 specification runs passed on a simulated CPU
(3 repetitions of 6 specifications). Chasing a failure produced the rule the day had been circling: one download per
session.

## 1.6 The dashboard and the pitch (phases 4 and 5, closed)

**What it is.** An API over the harness data, bound to `127.0.0.1` on purpose, six views, a live stream by server-sent
events and the plant copilot's chat. The README was rewritten for the pitch on 2026-08-28.

**Conclusion.** Writing the pitch caught three false claims, and running the front end found defects no test could (charts
that draw nothing in a screenshot, behaviour on real data): execute before believing.

## 1.7 The knowledge layer, stages 1 and 3

**What it is.** Retrieval over the manufacturers' documents, with citations, and the gate that measures it. A
`hardware-lookup` skill returns verbatim excerpts with document, version and page, or says nothing was found. The system
cites and never authors.

**Conclusion.** There are no semantic embeddings; the word "hybrid" in the brief does less than it looks, and the gate found
a defect on its first use. Method skills (GRAFCET) sit outside the layer and never answer hardware or safety questions.

## 1.8 The `grafcet` skill

**What it is.** Read, check, draw and program sequential charts: transcribe a chart or a photo, find design faults, draw it
cleanly, recover the chart a TIA Portal LAD program really runs, and generate set/reset LAD for an S7-1200, with edges
(`↑A`, `↓A` as P and N contacts). 276 tests.

**Conclusion.** Not a tool of the server and not part of the knowledge layer, by decision. Its open part is GEMMA (see part
2).

## 1.9 Reading and writing a program

**What it is.** Blocks, types and their groups: read, find, export (SimaticML and SIMATIC SD documents), import, delete.
`WriteScl` (including a `TYPE`), checked after writing for PEEK and POKE faults. `GetCrossReferences` answers who calls a
block. `ExportSourceSnapshot` and a full program backup before every write. **`EditTagTable`** changes single rows of a tag
table; **`EditDbMembers`** changes single members of a global DB; tag tables, tags and constants are created and read.

**Conclusions, each measured.**

- A bulk export or import reports what it could not do; it used to answer success and say nothing (Batch 2, five items).
- A block that does not compile **is** exported as a document; the earlier code hid it.
- `Tags.Create` accepts any text as an address, so `EditTagTable` checks it itself.
- A global DB imported again keeps its number and the start values its XML holds, but becomes a new object, inconsistent
  until the software is compiled, and removing a member a block reads is refused only by the compile — so `EditDbMembers`
  imports the original back when the errors went up.
- A `PEEK` of an optimized DB by number compiles and reads 0; every access error returns 0 with ENO false and the CPU stays
  in RUN, so the checker warns after writing.

## 1.10 Hardware and networks (phases 6 and 7, closed)

**What it is.** Addresses, subnets and PROFINET names; tag tables; a rack built, read, addressed and taken apart (plug,
unplug, move, copy modules, I/O addresses, module address); device parameters read and set; a node's subnet mask and router;
connections between PLCs (S7, TCP, ISO-on-TCP) and PUT/GET; users and roles (an S7-1200 compiles without passwords once the
anonymous user is active); `CreateProject`, `AddDevice`, `AddPcStation`; since 2026-11-24 (PR #87) devices created in a
device group, whose network paths start at the group, and a PLC with its HMI and their connection placed from a device group
template, each copy connected to its own PLC.

**Conclusions.** A device could be created from an order number and nothing could be done to it afterwards; now it can be
built and taken apart, and what a backup cannot restore is written down. Running the first slice found two older defects, and
one of them turned up a second time in the next slice. Every fact recorded from V20 was measured again on V21 before it was
trusted.

## 1.11 Going online, and watch and force tables

**What it is.** `GetPcInterfaces`, `ScanAccessibleDevices` (decided by the policy as `network/<interface>`, because it reaches
every device on a network), `GetOnlineState`, **`GoOnline`** and `GoOffline`; watch and force tables with row-level writes.

**Conclusion.** `GoOnline` killed TIA Portal until 2026-11-22, when the cause turned out to be the online connection's
**`EnableLegacyCommunication`, true by default**: with it false, going online works every time. The tool applies the PLCSIM
interface itself and refuses when there is none: it never goes online with a controller on a real network.

## 1.12 Version control

**What it is.** TIA Portal's Version Control Interface (workspaces, export, import, synchronisation, branches) and nine Git
tools on the repository in a VCI workspace's folder (commit, branches, pull, changes as patches; no push, which was not asked for). A local
secret store encrypted with Windows DPAPI: no tool takes or returns a secret value.

**Conclusion.** Git is always run with hooks, filesystem monitors and submodules switched off, and a repository with
`.gitmodules` or `commondir` is refused: project content is data, never instructions.

## 1.13 HMI, classic WinCC

**What it is.** HMI devices; read tools (`GetHmiInfo`, `ExportHmi`); import of screens, HMI tag tables and text lists with
their links checked; `GenerateHmiTags`; a screen generator with animations on every item, button and field events, symbolic
and date/time fields and text lists; `CompareHmiScreens` (the XML half); PC stations with the class's network card; a device
group template that carries the integrated connection.

**Conclusion.** Classic HMI has no object model for screen items in Openness, so the route is XML: screens, tag tables and
text lists export and import linked by name, and a hand-written screen compiled with 0 errors. The integrated HMI
connection cannot be created by Openness; a template copied from a device group is the way that worked.

## 1.14 WinCC Unified

**What it is.** `CompileSoftware` on a Unified HMI returning every nested message; tags with comments, limits, scaling and
arrays; text lists; alarms (classes, discrete and analog); logging (data logs, alarm logs, logging tags, log settings,
trends); animations (range tables, flashing, script, movement, rotation, bar and gauge); events as named functions or
scripts, with `increaseValue`, `decreaseValue`, `invertBit` and `callModule`; **`DrawUnifiedScreen`** and
**`EditUnifiedScreen`** (change some items and leave the rest); faceplates (instantiate, release, update); script modules;
`CheckUnifiedScripts`; Custom Web Controls (a catalogue, install, place).

**Conclusions.** TIA Portal compiles broken calls into modules without a word, so the checker finds them. A screen has one
global definition area per script context, not one per item, so the definitions are merged. With an `import` in a screen's
global definitions a syntax check ends TIA Portal's session, so scripts are checked with the imports left out. A "Compiling
aborted" with no text was the encrypted-transfer password, which Openness lists.

## 1.15 Libraries and master copies

**What it is.** Project and global libraries: read, copy types from a global library, release a version, publish to a global
library of one's own, a new version in work, move faceplates between versions, create a PLC data type from a library, create
a library type or a new version from a SIMATIC SD document, find who uses a type, keep the library tidy; master copies of
blocks, tag tables and PLC data types (read, folders, made, created from); library backups named for V21. **Closed
2026-11-24 (PR #84):** block types into a program, a new version of a block or data type through its instance, instances
moved to another version, versions compared by content, new versions to a global library, a global library archived and
retrieved, type folders, master copies and their folders deleted; a type created, versioned and instantiated in a project and
a global library through the MCP alone (`Test81LibraryWorkflow`).

**Conclusion.** Creating a faceplate **type** cannot be done through Openness (V20 or V21); it is drawn by hand once and
everything after it is generated. A tag repeated in a table is renamed `X(1)` silently even when told not to, so it is
detected and the table deleted. On V21, `UpdateProject` removes a program's instances instead of updating them, and a block
or data type gets a new version only through an instance in a program: both measured, both worked around.

## 1.16 Reading a running controller over OPC UA (phase 9, opened)

**What it is.** `BrowseOpcUaServer` and `ReadOpcUaValues`, in their own assembly, with certificate pins; policy decides which
server may be contacted. They read and nothing else: no write, no method call, no subscription.

**Conclusion.** Reading a live value from a physical PLC cannot be done through Openness at all; it needs a separate
protocol component, and that is why this is its own assembly.

## 1.17 Working with the user's TIA Portal open, and V21

**What it is.** `Connect` takes the process that holds the project, then one with a window, and says which and why; it never
closes the user's project. The repository builds for V21 by default; the whole Openness API was compared V20 against V21 by
reflection (322 types added, 16 removed).

**Conclusion.** A TIA Portal the server starts and leaves running when it is killed is kept on purpose: killing it can break
the project. A probe that disposes a process from `GetProcesses()` closes that TIA Portal.

## 1.18 Deployment and release

**What it is.** A versioned release artefact with its SHA-256, a precondition check that fails closed, `INSTALL.md`, host
wiring for Claude Code and Claude Desktop, and the server reporting its own version (0.0.20; installed from the artefact and
registered).

**Conclusion.** The blocking criterion (a clean machine following only `INSTALL.md`) was not run; see part 2.

## 1.19 The web platform, phases 1 to 4 (steps 1 to 3)

**What it is.** A Postgres and Drizzle database, sign-in with Google (tried in a browser), a workspace (who sees what, by
organisation), every change recording its project, and the control-room screens. PR #41.

**Conclusion.** It is optional and local-first: the C# server is local either way, and the platform only reads what the
server already wrote. What was fragile was around the web, not the web.

## 1.20 Debt paid

**What it is.** The audit findings F1 to F4, F6 and F7 and half of F5 closed; `McpServer`, `Portal` and `McpServerWrites`
split (2,176 lines against a limit of 300); responses one per file; the bulk tools with one progress reporter; the imports
that used to swallow their failures; the methods over 30 lines measured (91 on 2026-09-27) and split in batches.

**Conclusion.** Most of the debt was visible only by running the tool, so every closed item has a test that failed first.

---

# Part 2 — What is missing

Everything asked for, planned or found open, by area. Each item says when it is done. **The order the user fixed:** HMI and
libraries first; then the knowledge layer; among the other branches of TIA Portal, communication between controllers and the
CPU's OPC UA server first, **multiuser and safety last**; Workshop Mode after everything.

## 2.1 HMI, the full list ("everything everything")

The checklist is `HMI-COVERAGE.md`, whose rows are **Measured**, **Built** (ran offline only) or **Open**. The open rows and
the milestones of the old plan:

- **Classic WinCC:** the symbol-library graphics and groups in the generator; the runtime screenshot compared with a reference
  picture, under a threshold fixed before measuring; an ISA-101 style option; simulating the panel against PLCSIM so a press
  reaches the PLC. Documentation read and cited (the WinCC Comfort/Advanced manuals and the Openness HMI chapter), each fact
  carrying its source.
- **WinCC Unified:** the Unified panel against PLCSIM, so a press reaches a PLC tag; download to the simulated runtime
  reporting the result, reading the runtime's own log when TIA Portal says only "Action canceled"; runtime settings with a
  password that never reaches a log, an audit entry or a backup in clear; users and roles; the audit viewer and which of these
  need an option or a licence; the Unified system manual cited; that a script-module function called from an event runs **in
  runtime** (not measured: it needs the runtime open).
- **Both:** ~~the connection between a PLC and an HMI created on V21 without a template; the HMI connection template measured
  again on V21~~ - **done**: `CreateHmiConnection` makes the integrated connection on V21 and the template works there
  (2026-11-11, PR #69); devices in a group and the secret in the template's policy added 2026-11-24 (PR #87). Left: a device
  group of more than one level, and the runtime password and start screen a new Unified runtime needs. Faceplate **templates**, several
  together (each a global library drawn once by hand, a tool that assembles the motors from a specification, and a behaviour test
  on PLCSIM), when she asks.
- **Done when**, for the whole area: every row of `HMI-COVERAGE.md` is Measured.

## 2.2 Libraries, master copies and types - met 2026-11-24

- **Done (PR #84):** deleting a master copy and its folder; updating PLC instances and block types after a new version; a
  global library archived and retrieved; the library tree; comparing library type versions; a type's default version and
  deleting a type (2026-11-15). A version without a test instance is **not possible on V21** (measured): it is made through an
  instance, one tool call away.
- **Left:** master copies of the other source kinds (block and tag table groups, devices: measured, not built, as the names
  inside a group master copy are not readable); moving a function block's instances (instance DBs not measured); a type
  created straight into a library folder.
- **Done when:** a type is created, versioned and instantiated, in a project and a global library, through the MCP alone -
  **met** by `Test81LibraryWorkflow`.

## 2.3 Programs and blocks

- **Generator features learned from building a program end to end** (they are generator work, not exercise work): a global
  `IEC_TIMER` DB written with the program; calls from `Main` generated; network and block titles and comments through
  `.s7res`; one block per state of a chart; the S7-1500 `time_type` template on TON calls.
- Block operations still missing: add, update and remove interface members; update block content; compile a single block;
  rename and move; block folders; external sources and their folders; export a block's source; block snapshots; load block start
  values; block protection (protect, unprotect, status); a block namespace; import SCL from a file.
- Software Units (all of them), PLC tag operations (delete, rename, update), tag tables and constants (delete, rename, folders,
  update), tags imported and exported as CSV or XLSX, UDT create, update, delete and folders.
- **EditDbMembers:** comments on members; members inside a struct; what a download does with the values a CPU holds when the
  DB's structure changed (TIA Portal's rule, to be measured on PLCSIM Advanced).
- **Review nits still open:** the undo of `EditTagTable.remove` restores name, type, address and comment but not the other
  flags; `EditTagTable` and now `CreateTag` (2026-11-24, PR #85: TIA Portal takes any text as an address) refuse addresses
  ending `:P`, which TIA Portal may accept (measure first); a refused
  `WriteUnifiedScriptModule` leaves a backup entry; `CheckUnifiedScripts` exports the modules on every call and fails whole if
  one export fails; the module-call rules can error on valid code (an arrow function's parameter, a non-exported `const`
  function); a multi-line `import` in given globals leaves its tail behind; `UnifiedScratchScreen` may throw on dispose when the
  HMI had no start screen; `callModule` refuses a module name with a hyphen or a space.
- **PEEK and POKE:** closed 2026-11-24 (PR #85): the text findings are in the plan before the write, a missing DB is an
  error, the peripheral areas a warning, and only the DBs named are read. Left: what the peripheral areas do at run time (not
  measured), the I/O areas on an S7-1200 (PLCSIM Advanced has none, so `peek-poke-peripheral-on-1200` needs the CPU family),
  `poke-blk-dbnumber-types`, and an Openness test driving `WriteScl` itself.
- **How to really program with TIA Portal's instructions, asked for 2026-11-25.** Go beyond generating code that compiles:
  study, with the V21 help cited and each behaviour measured in PLCSIM Advanced, the instructions a real program uses, and turn
  what is learned into generator knowledge and SCL checks (as `SclMemoryAccessRules` did for PEEK and POKE). First
  **`NORM_X` and `SCALE_X`** (and the classic `SCALE`/`UNSCALE`): an analog input normalised and scaled to engineering units
  and back to an output, the raw ranges of each module (27648 for a standard S7-1200/1500 channel, the over- and underrange
  values, live-zero 4-20 mA), types and the error output; then, one family at a time: timers and counters (IEC, multi-instance,
  `time_type` on S7-1500), edges, comparison and math (`CALCULATE`, `LIMIT`, `MIN`/`MAX`, rounding and conversions with their
  silent truncations), move and block move (`MOVE_BLK`, `FILL_BLK`, `Serialize`/`Deserialize`), strings and date/time (`DTL`,
  `RD_SYS_T`), program control and error handling (`GET_ERR_ID`, ENO), PID (`PID_Compact` and its tuning), and the
  communication instructions. Each family: a study sheet with sources, measurements in PLCSIM, the mistakes a student makes and
  a rule that finds them. **Done when** every family has its sheet, its measurements and, where a mistake can be read from the
  source, its rule.
- **Other languages:** FBD and STL imported as documents; S7-GRAPH on an S7-1500; the graphical languages have no authoring API
  and are generated as SimaticML.
- **Done when:** one block of each language compiles, and each tool above has its test and its refusal test.

## 2.4 Hardware, CPU and drives

- A CPU added through Openness on V21 does **not** compile its hardware: it demands a password for the confidential PLC
  configuration data, a communication certificate and an access-level password. How to set them through Openness is not
  measured, and until it is no new CPU can be downloaded without a fixture.
- CPU properties (read and configure, change the CPU type, analog channels), delete and rename a device, delete a subnet,
  hardware export and import, EPLAN AML, GSD/GSDML devices (a third-party device plugged and compiled), compatible slots, the
  parking lot, the engineering views, the cycle clock memory byte on an S7-1200 (unreachable on V20; **not measured on V21**),
  PG/PC access points and their mapping (these touch the Windows registry: a copy of the key first and a confirmation on each
  change).
- OPC UA and web server users on the CPU, and their configuration; **the CPU's OPC UA server configured through the MCP** (it
  is only read from today) — done when a server interface with chosen tags is created, downloaded to PLCSIM Advanced and
  browsed.
- **Drives and servomotors, asked for 2026-11-24:** work with them the way a class or a machine does - a SINAMICS drive (G120,
  S120, V90) added, networked over PROFINET and given its telegram (standard telegram 1 for speed, 3 or 105 for a servo axis);
  its drive parameters read and set through Startdrive's Openness where it has one; and the technology objects that drive it
  (`TO_SpeedAxis`, `TO_PositioningAxis`, `TO_SynchronousAxis`, cams, PID) created, configured and linked to the drive. Then
  the program side: `MC_Power`, `MC_Home`, `MC_MoveAbsolute`, `MC_MoveRelative`, `MC_MoveVelocity`, `MC_Halt`, `MC_Reset` in
  SCL, and an axis run in simulation (PLCSIM Advanced with a simulated drive, or the TO's own simulation mode). Measured first,
  as everything else: what Openness exposes of technology objects and of Startdrive on V21, whether a V90 is reachable without
  V-ASSISTANT, and what runs without a real drive. Done when a positioning axis is created on a 1511 with a simulated drive,
  homed and moved to a position from SCL in PLCSIM Advanced, and the MCP reads back its position. Physical drives stay behind
  Workshop Mode: a servo moves metal. **Studied 2026-11-24 (PR #86, `DRIVES-AND-SERVOS.md`, offline):** Startdrive Advanced
  V21 is installed; technology objects and drives are both in Openness V21; the CPU firmware bounds the technology version (the
  fixture's 1511 V4.0 at most V7-V9); an axis in `Simulation.Mode = 1` runs with no drive, so the done criterion needs none.
  Next: its measurements M1-M17, then the proposed tools and the `SclMotionRules` checker.
- PROFINET in depth (device names, topology, I-Device, IO-Link masters and ports), PROFIBUS and AS-i networks.
- Communication between controllers: PUT/GET exists; S7 connections, open user communication (TSEND and TRCV) and Modbus TCP,
  client and server — done when two simulated CPUs exchange a value over each.
- Going online: a CPU other than an S7-1500 (an older one may speak only legacy communication, which `GoOnline` turns off);
  what an online connection allows besides exports and imports being refused. Physical hardware stays behind Workshop Mode.
- **PLCSIM Advanced beyond the basics:** batch read and write, power, run and stop, memory reset, instance and network
  configuration, persistence, repair and diagnostics.

## 2.5 Diagnostics, protection, alarms and tests

- Diagnostics: the diagnostic buffer, module state, the CPU web server — a fault provoked in PLCSIM is read back with its text.
- Protection: block know-how protection, CPU access levels, copy protection, user management. **A password never reaches a log,
  the audit trail or a backup in clear**, and a test fails if it appears in any written file.
- Secrets: the local DPAPI store is built; the wider set of ten secret tools she chose on 2026-11-05 is not (encrypted only,
  the value never returned or logged, a security review before merging).
- PLC alarms (program alarms, ProDiag, text lists, the instance texts in XLSX) — an alarm raised in PLCSIM appears on the panel.
- Tests as a tool: test cases written once and run automatically against PLCSIM, reporting pass or fail per case, and the
  reports a customer asks for (FAT, SAT, evidence, project report as PDF or Excel).
- Safety: **read and explain only.** The MCP never generates or writes safety logic, in any mode. She recovered four tools
  (create and delete a safety runtime group, log in and out of the safety program), offline only, with explicit confirmation
  on each write, no download to a physical F-CPU and the password per call.

## 2.6 Project, history and orchestration

- Project: archive a project (`ArchiveProject`, so a project can be handed in as an archive; today it can only be retrieved),
  attach, detach and shut down a portal, export and import project texts, project languages, history, overview, files, sync.
- Compare two versions of a project; snapshots that can be restored (today there are backups and no restore).
- Multiuser engineering — **last**: two sessions check in changes to one server project without conflict.
- Add-Ins, which run inside TIA Portal: a small read-only one and its trade-off against attaching, written down.
- Orchestration (work plans, pipelines) and pattern tools (list, search, instantiate) beyond `ExpandCellScl`.

## 2.7 The knowledge layer, and the technologies she asked to learn

Each stage starts on her explicit word.

- **Stages 0, 2, 2b, 4 and 5** of `KNOWLEDGE-LAYER.md`, each meeting its "done when" there.
- **Technologies to learn and add as layers, apuntadas 2026-11-20 and 2026-11-22:** OPC UA, S7 communication, MQTT, **Node-RED**
  and **UaExpert**, each with its sources cited; and, added 2026-11-24, **drives and servomotors** (SINAMICS, telegrams,
  technology objects and the PLCopen motion instructions; the building side is in 2.4); and, added 2026-11-25, **TIA Portal's instructions as they are really programmed**, starting with `NORM_X` and
  `SCALE_X` (the building side, with its families and checks, is in 2.3). What OPC UA already measured: PLCSIM Advanced in Softbus mode is not reachable
  from Windows, in TCP/IP mode it is; variables of a global DB and of the tag table appear as `ns=3;s="DB"."Var"` and
  `ns=3;s="tag"`; a Real is a `Float` in a Node-RED item; `red.js start` makes a flow file called `start`.
- **GEMMA:** learn the guide (stops, starts and failures of an automaton) from cited documentation and not from memory, and
  have the skill read, check and draw it — the GEMMA model, its checks (every used state reachable, D1 reachable from every
  state, a way back to A1), the standard layout (zones A, F and D; renvois for jumps; no overlapping labels), forcing orders in
  the notation, and the `transient-evolution` false positives. The clean drawings of 2026-10-15 came from a throw-away script,
  not from the skill.
- **The original S7-PLCSIM** (not only PLCSIM Advanced): measured only as metadata. V20 is uninstalled on the development PC,
  so what to do about classic simulation of an S7-1200 has to be decided again.
- Tips (eleven, local, nothing sent out without her approval each time), the product's own documentation tools, licence and
  approval-registry tools, and a catalogue of the MCP's own tools — the 22 she recovered on 2026-11-05.
- Documentation read and cited for each generator: WinCC Unified system manual and its Openness chapter.
- Study sheets per topic, each fact with its source, built from what the work above measured.
- A local model for customers who cannot send code out, with the harness gate run against it.

## 2.8 Quality, security and the enterprise

- Technical debt: audit findings F8 to F12 and the second half of F5 (pure rules out of `SclBlockGenerator` and
  `SourceSnapshotExporter`); the `NoWarn` ledger emptied; methods over 30 lines; dependency scan and an SBOM in CI; project
  content always treated as data by the model (a test feeds an instruction hidden in a block comment).
- A style-guide check of generated code against Siemens' programming style guide, with the rule each finding breaks.
- Four eyes (a plan confirmed by a second person in the web platform), an IEC 62443 self-assessment, a product-law review
  (Cyber Resilience Act, Openness terms, AI Act, MIT notice) by someone qualified, and a written support process.
- Anything the review of `ENTERPRISE-READINESS.md` lists beyond these.

## 2.9 Other people's machines, and the web platform

- **The blocking criterion of phase 5b:** installed from the release artefact alone, on a machine that never built the project,
  following only `INSTALL.md`, up to reading a PLCSIM tag. A virtual machine counts; a second copy of the working tree does not.
  Until then the instructions are a guess. Open questions about the institut's PCs (licence, proxy, IP segment) are unconfirmed.
- **Local first**: the daily screens run at `127.0.0.1` with no Docker and no sign-in; the shared part stays optional.
- Web platform phase 4, the optional shared part (the teacher signs in from another machine and reads a project) and phase 5
  (descriptions, images, snapshots, metrics, the diff). Sign-in with GitHub is configured and was not tried.
- A user guide a classmate can follow without help.
- Remote access to the dashboard and any shared store stay **out of scope**: exposing the API beyond loopback publishes a record
  of everything the server changed.

## 2.10 Beating T-IA Connect

She set the goal on 2026-11-04: a project much better than the commercial reference. Counting by name on 2026-11-11, of 492
advertised tools 69 are out of scope and 105 of the rest are covered by function; the missing categories are the ones listed
above (project, hardware, PG/PC, CPU services, blocks, generation, Software Units, tags, UDTs, observation and forcing,
alarms, HMI, online, PLCSIM, tests and delivery, libraries, motion and drives, safety, security, history, VCI, orchestration,
patterns, knowledge, discovery). **The figures are what their site claims, not proof that anything works.** The aim is not a
number of tools: it is that what is generated works and is shown with data. Three of their claims contradict what was measured
here and must be measured again before anything is copied (an integrated HMI connection, the cycle clock memory on an S7-1200,
a library type version from a block).

## 2.11 Workshop Mode, last and supervised

The five measured conditions, an in-person design review (`workshop-review.md`), and a teacher or workshop supervisor present
with the emergency stop in reach. It comes after everything above. **No software enforces the supervisor and none can.**

---

# Part 3 — Class material (not part of the construction)

By the user's decision on 2026-11-22 the exercises of the course do not count as items of the construction. They stay because
they are good regression cases and a good source of facts, and because they found real defects; none of the items in part 2
waits for them.

- **The exercises**: 0965 (the warehouse), 0967 (Unified HMI, the flour-mill motors, two PLCs and a Unified panel) and 0968 (the
  hopper with its GEMMA, the planning, the cabinet and the budget, and the final project), and every later one. Each is kept as
  a statement, a solution and its checks, so that every release solves it again with 0 errors.
- **The hopper exercise regenerated end to end through the MCP** (0 errors, 13 PLCSIM cases, nothing added by hand), and the
  same cases on an S7-1200 and not on the S7-1500 stand-in.
- **The teacher's style**, as drawn in his notebook: the selection remembered by lamps with S and R, stored actions as a row of
  S and R boxes, production branching on the lamp; and the layout of one FC per GEMMA state numbered in call order, Main only
  calling, outputs in one block, a watchdog timer per step, names in Catalan.
- **The class corpus** (63 projects and their documents, kept on this machine, never in the repository): a private recipe with
  hashes; ingestion of the other formats; the retrieval gate extended; the projects searchable block by block; generators
  writing in the class style by default; static checks learned from it (the comparison of a value with itself that resets a
  counter every cycle); study sheets per module.

---

# Part 4 — Reference

What follows did not change when the plan was reorganised: the starting point, the two modes, the architecture, the prior art
and the decisions taken.

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
| Local first; a desktop shell only as a wrapper, the shared web platform optional (2026-10-17) | The question was whether a web platform is less stable than a desktop app. The connection to TIA Portal does not depend on it: Openness runs only on the machine with TIA Portal, so the C# server is local either way, and the platform only reads what the server already wrote. What is fragile is around the web, not the web: Docker Desktop opened by hand next to TIA Portal, sign-in that needs a public HTTPS URL, and a server whose home is still undecided. A desktop rewrite would drop those and also drop what was asked for — the teacher and classmates reading from other machines. So the daily screens run locally with no Docker and no sign-in; if a window and an icon are wanted, the same front end is wrapped in Tauri rather than rewritten; Postgres and sign-in serve only the shared part. |
