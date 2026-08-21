# tia-station-mcp

An MCP server for Siemens TIA Portal aimed at verified PLC code generation and at
coordinating multi-station cells.

Final project for the CFGS in Industrial Automation and Robotics.

## What it is for

Closing the whole loop, not just generating code:

```
specification → generate SCL → import into TIA → compile → read errors
                      ↑                                        │
                      └──────────────── fix ←──────────────────┘
                                          │
                                          ▼
                              test on PLCSIM Advanced
                                          │
                                          ▼
                                    export to Git
```

An LLM generating PLC code does not get it right 100 % of the time. Reliability does not
come from the model: it comes from **the compiler and the tests**. That is why the closed
loop is the core of the design and not an add-on.

## Status

See [`docs/STATUS.md`](docs/STATUS.md).

## Base and attribution

Built on [heilingbrunner/tiaportal-mcp](https://github.com/heilingbrunner/tiaportal-mcp)
(MIT), from which we inherit the architecture, code conventions and error model.

Analysis of the seven reference repositories in
[`docs/REFERENCE-REPOS.md`](docs/REFERENCE-REPOS.md).

## Requirements

- Windows x64
- TIA Portal V20 with the Openness component
- User in the `Siemens TIA Openness` Windows group
- .NET Framework 4.8
- PLCSIM Advanced (for the test phase)

## What this adds on top of the base

What `tiaportal-mcp` does not cover and we add here:

- Tag tables (`PlcTagTable`) — export/import
- Writing SCL directly through an external source
- PLCSIM Advanced integration for automated tests
- Full project snapshot to text for Git
- An instantiable "station" pattern generator
- Reading and driving a running controller's tags, so generated code can be observed

## The cell pattern

A cell is described as data, in `spec/cells/`. Nothing in `src/` knows what a station does or how
many there are:

```json
{
  "cell": "TwoStationDemo",
  "stations": [
    { "name": "Feeder", "workSteps": 2, "dwellCycles": 5 },
    { "name": "Driller", "workSteps": 3, "dwellCycles": 10 }
  ]
}
```

`ExpandCellScl` turns that plus the patterns in `spec/patterns/` into SCL and **returns it without
writing anything**. `WriteScl` puts it in a project, `CompileSoftware` checks it. Going from two
stations to four is two more entries in the list and no code change.

The generated cell is a handshake, and the handshake is the whole of the coordination:

```
Ready   "I am empty and able to take a piece."
        The coordinator writes PieceId and raises Start. Start high means "this piece is yours".
Busy    "working on it."
Done    "finished, and the piece is still mine until you take it."
        The coordinator drops Start once it has moved the piece. Only then does the station idle.
```

A station **holds its piece until Start drops**, and if the next station has faulted it keeps it and
the cell reports `BlockedAtStation`. Releasing anyway would put a piece into a station that cannot
work it, and a piece in no station is exactly the state a traceability number cannot describe.

The station steps do nothing, deliberately. What a station physically does is cylinders, sensors
and interlocks, and none of that can be inferred from a JSON file — so the steps are where that
work goes, and the handshake around it already works without it.

Both cells that ship are written into a real project and compiled in TIA Portal V20 by the test
suite. SCL that looks right and does not compile is the normal outcome of writing it from memory,
so "it compiles" was for a while the only claim made about it here.

### And it runs

Compiling is not running: a coordinator that hands a piece to the wrong station compiles perfectly.
So the suite also downloads the two-station cell to PLCSIM Advanced, starts the CPU, and watches a
numbered piece go through it — driving `CellStart` and reading `PieceId` through the tag tools
below.

`ExpandCellScl` takes `includeEntryPoint` for this: it adds the cell's instance data block and a
`Main` OB that calls it every scan, which is what makes the cell execute at all. It is off by
default because it **replaces the project's existing `Main`**. The OB calls the instance with no
parameters, deliberately — passing constants would assign the inputs on every scan, and then a tag
write would be overwritten before the next call and nothing outside the program could drive the
cell.

One property of the pattern is worth knowing before wiring a mode selector to it: **the mode may
only be changed with the cell empty.** `ModeAuto` and `ModeManual` are two bits and cannot change
together, and a station treats both-the-same as a wiring fault — correctly, because two modes at
once is one. A station that receives a piece during that gap faults. Found by a test that tried it.

### Watching a program run

Three tools read and drive a virtual controller's tags:

- `ListSimulationTags` — the names, read from the controller and not from the project, so it is
  empty until something has been downloaded. Filter by name: a CPU has thousands. The response says
  how many matched and whether the page was truncated, because a truncated list that looks complete
  is the worst of the available answers.
- `ReadSimulationTags` — several tags in one call, through one handle, so a handshake is observed
  from nearly the same moment rather than from four round trips. Not a consistent snapshot of a
  scan, and nothing pretends it is.
- `WriteSimulationTag` — one tag per call, **guarded** like every other write. The value is text
  and is parsed as the tag's declared type, always in the invariant culture: a `Real` written `1,5`
  on a Spanish machine and `1.5` on an English one would make the same call mean two things. What
  the controller holds afterwards is read back rather than echoed, because a tag the program assigns
  every scan will not keep what you wrote.

## Safety

By default, **every deployment targets PLCSIM Advanced**. Downloading to a physical PLC
requires explicit confirmation. See [`CLAUDE.md`](CLAUDE.md).

### Nothing is written without a policy

Every tool that changes the project, a virtual controller, or the project on disk goes through
the governance layer in `src/TiaMcpServer/Governance/`. The sequence never varies: check the
policy, make a plan, record it in the audit trail, then run it.

**A missing policy denies every write.** That is not an oversight to work around: the absence of
a decision is a refusal, never a permission. To configure one:

```powershell
Copy-Item .tia-mcp\policy.example.json .tia-mcp\policy.json
```

Then edit it. A target is a place in the project tree (`PLC_0/Blocks/FB_Station`), a virtual
controller (`simulation/Station_1`), or the project as a whole (`project`). `*` stands for any
run of characters — it is not a regular expression. Deny beats allow, and anything matching
neither list is refused.

Three paths are configurable on the command line:

```powershell
TiaMcpServer.exe --policy .tia-mcp\policy.json --audit .tia-mcp\audit.jsonl --backups .tia-mcp\backups
```

The audit trail is append-only, one JSON object per line, and records refusals as well as
changes — a whitelist nobody can see working is one nobody trusts.

### The previous state is always kept, and the caller does not choose where

Before a write overwrites anything, the current state is exported to a timestamped directory
under the backup root. **No tool takes a backup location as a parameter**, deliberately: a
caller that picks the directory can pick one nobody will look in, and an agent that can pick
can pick a temp folder Windows will reap.

`ListBackups` reports everything saved, newest first. An entry with `fileCount` 0 is a change
that was refused or that failed before exporting, so there is nothing in it to restore from —
which is worth being able to tell apart from a backup that worked.

### A compile or a download does not block the caller

`CompileSoftware` and `DownloadToSimulation` accept `runAsJob`. With it they return a job id at
once instead of waiting; `GetJobStatus` reports how it went, `ListJobs` lists every job of the
session, and `CancelJob` cancels one **that has not started yet**.

Cancellation stops there because that is where Openness stops: a compile and a download are
blocking calls that accept no cancellation token and cannot be interrupted once begun. A running
job is reported as not cancellable rather than given a "cancelling" state that never resolves.

### One thing at a time inside TIA Portal

Openness runs concurrent calls in parallel and nothing serialises them. Measured: two snapshot
exports started 1 ms apart both ran from 1 ms to 1620 ms, each doing its own work. That did not
matter while every request was handled in turn; asynchronous jobs changed it.

So every tool that reaches TIA Portal takes `OpennessGate` first, and `McpServer.Portal` **refuses
to hand out the portal to a caller that has not**. A tool that forgot would otherwise work
perfectly until the day a job happened to be running at the same time.

Polling tools — `GetJobStatus`, `ListJobs`, `CancelJob`, `ListBackups` — deliberately do not take it.
If asking how a compile is going had to wait for that compile, jobs would be pointless.

### Study Mode and Workshop Mode

The session always starts in **Study Mode**, which reaches PLCSIM Advanced and nothing else, and
where a whitelisted change confirms itself. **Workshop Mode**, which commands physical hardware
and requires a person to confirm every change one at a time, is **compiled out** of the ordinary
build: it exists only in a binary built with `-p:WorkshopMode=true`. No configuration mistake can
reach a machine with the everyday build, because the capability is not in it.

`GetOperationMode` reports which mode a session is in, and `ApplyChange` confirms a plan by id.

**Workshop Mode may only be used with a teacher or workshop supervisor physically present, with
access to the emergency stop.** No software enforces that and none can — a whitelist, an audit
trail and a confirmation phrase are all bypassed by a person in a hurry who is alone in a room
with a machine. It is stated here because the rules that depend on people keeping them are the
ones that have to be written where people read them.
