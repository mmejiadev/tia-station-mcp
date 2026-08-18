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
