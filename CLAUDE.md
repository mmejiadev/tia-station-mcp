# CLAUDE.md — tia-station-mcp

Working rules for agents in this repository. **They are binding, not advisory.**

Some are inherited from `repos/tiaportal-mcp` (MIT), whose `AGENTS.md`, `style.md` and
`docs/error-model.md` we adopted so we share logic with upstream and can contribute back
without friction.

## Language — everything in English

**Every artifact committed to this repository is written in English. No exceptions.**

That means: source code, identifiers, comments, XML doc, log and exception messages,
MCP tool descriptions, Markdown documents, file names, and commit messages.

The only thing that is not English is the conversation: the user writes in Spanish, so
reply to her in Spanish. What lands in the repo is still English.

Rationale: the base is an English MIT project we want to contribute back to, the Openness
API and its documentation are in English, and mixed-language code is a mess to search.

## Never commit — the commits are the user's

**Do not run `git commit`, `git push`, `git tag`, or anything that rewrites history.**
Not "unless asked", not "when it seems finished": never on your own initiative.

Stage nothing on the user's behalf either. Leave the work in the working tree, say what
changed and why, and let the user commit it themselves. If a commit message would help,
write it out in the reply so it can be copied — do not run the command.

This holds even when the user has authorised a commit earlier in the conversation. That
authorisation covered that commit and no other.

The one exception is an explicit, unambiguous instruction in the current turn, naming the
action: "commit this", "push it". Wanting the work finished is not that instruction.

### Never push to `main`

`main` moves through a pull request the user merges on GitHub, and no other way.

Even when a commit has been explicitly authorised, it goes on a branch. Never
`git push origin main`, and never push a branch to the same commit as `main` — with nothing
to compare, GitHub offers no pull request and the review the user wanted silently does not
happen. That mistake was made on 2026-08-12; it is why this rule exists.

Work goes on a branch, the branch is pushed **ahead of `main`**, and the user opens and
merges the pull request. Reviewing the diff is the point, not a formality.

## Starting a session

Read **`docs/STATUS.md`** before doing anything else. Its "▶ RESUME HERE" section states
where the work stopped, what is blocked, and the next action. It is the project's source of
truth and is updated at the end of every session.

Reference repositories live in `../repos/` and are analysed in `docs/REFERENCE-REPOS.md`.

## Project context

An MCP server in C# exposing the TIA Portal Openness API to an LLM, aimed at:

1. Generating and verifying PLC code (SCL) for a four-station cell.
2. Closing the generate → compile → test (PLCSIM Advanced) → fix loop.
3. Versioning TIA projects in Git through bulk export to text.

Target environment: **TIA Portal V20**, **.NET Framework 4.8**, **Windows x64**.

---

# Architecture

## The dependency rule — the most important one in the repository

Dependencies flow in **one direction only**. Never the other way.

```
McpServer  ──►  Portal  ──►  Openness  ──►  Siemens.Engineering
 (protocol)    (domain)     (adapter)        (external API)
```

- `ModelContextProtocol/` **must not** contain a `using Siemens.Engineering`.
  If it needs something from Openness, expose it in the `Portal` layer first.
- `Siemens/` **must not** know about MCP: no `McpException`, no MCP response types,
  no JSON serialization. It throws `PortalException` and nothing else.
- Openness types (`PlcBlock`, `Device`, `Project`…) **do not cross** into the MCP layer.
  They are translated into our own DTOs in `Portal`.

If a task seems to require breaking this, the task is wrong. Ask first.

### The rule is a build error, not a convention

The repository is two assemblies, and the split is where the rule stops depending on anyone
remembering it:

| Assembly | Holds | May reference Openness |
|---|---|---|
| `src/TiaMcpServer.Portable/` | `Governance/`, `Knowledge/`, `Spec/`, `Jobs/`, the error model, `GuardedTool`, `OpennessGate` | **No** |
| `src/TiaMcpServer/` | `Siemens/`, `McpServer`, `Program` | Yes — it is the adapter |

`TiaMcpServer.Portable` **must never reference `TiaMcpServer`**, and no package reference of its
may drag in the Openness resolver. That resolver locates `Siemens.Engineering.dll` from an installed
TIA Portal at *build* time, so anything that reaches it needs a licensed machine to compile — which
is how the safety tests came to be uncheckable anywhere else.

Adding the reference back would not fail any build. It would quietly move 149 safety tests off
continuous integration, which is why the rule is written here rather than left to be noticed.

## Size and shape

| Rule | Limit |
|---|---|
| Method length | ≤ 30 lines |
| Class length | ≤ 300 lines |
| Parameters per method | ≤ 4 (more → parameter object) |
| Nesting levels | ≤ 2 |
| Cyclomatic complexity | ≤ 10 |

A method does **one thing** and operates at **a single level of abstraction**. If you have
to write a comment to separate "blocks" inside a method, those blocks are methods.

When `McpServer.cs` or `Portal.cs` exceed 300 lines (upstream they are 90 KB and 95 KB),
split them by functional area into partial or collaborating classes: `PortalBlocks`,
`PortalTags`, `PortalCompile`. **We do not reproduce the monolithic file.**

---

# C# code style

- Target **.NET Framework 4.8**, modern `LangVersion` so nullable reference types are available.
- **Four-space** indentation, no tabs.
- Opening brace **on a new line**.
- `PascalCase` for classes and public members; `camelCase` for parameters and locals;
  `_camelCase` for private fields.
- `using` directives grouped at the top, separated from the namespace by a blank line.
- `Task`/`Task<T>` for potentially long operations. **Never `async void`** except for
  event handlers.
- `CancellationToken` on every operation that can take a while (compile, bulk export, download).
- `Microsoft.Extensions.Logging` for logging.

## Naming

- No abbreviations: `blockPath`, not `blkPth`. No `tmp`, `aux`, `data`, `obj`, `res`.
- Empty suffixes are banned: `Manager`, `Helper`, `Utils`, `Processor`, `Handler` when they
  describe nothing. If a class is called `BlockHelper`, you cannot tell what it does.
  Name it after its responsibility: `BlockExporter`, `SclSourceBuilder`.
- Booleans read as an assertion: `isConsistent`, `hasTagTable`, `canDownload`.
- Methods start with a verb: `ExportBlock`, not `BlockExport`.

## Immutability and state

- `readonly` by default on fields. A mutable field must be justified.
- DTOs and response objects: **immutable**, no public setters.
- No mutable static state. No singletons. **No global variables.**
- Constructor dependency injection. No `new`-ing collaborators inside business logic.

## Control flow

- **Guard clauses first**, early return. No pyramids of `if`.
- `else` after a `return` is forbidden.
- No magic numbers or strings: named constants.
- No giant `switch` over types: polymorphism or a strategy dictionary.

```csharp
// bad
if (block != null)
{
    if (block.IsConsistent)
    {
        // 20 lines
    }
}

// good
if (block == null)
{
    throw new PortalException(PortalErrorCode.NotFound, $"Block not found: {blockPath}");
}

if (!block.IsConsistent)
{
    throw new PortalException(PortalErrorCode.InvalidState, "Compile the block before exporting");
}

// 20 lines, no nesting
```

## Comments

- The code explains **what** it does. Comments explain **why**.
- A comment that paraphrases the next line gets deleted.
- Comments documenting Openness quirks **do stay** and are valuable.
  For example: why `IsConsistent` must be checked, why LAD needs `.s7res`.
- `///` XML doc on every public member of the `Portal` layer.
- No commented-out code. That is what Git is for.
- No `TODO` without a date and an owner. If you are not going to do it, do not write it.

## Files and folders

- **One public class per file, and the file is named after the class.** `ModeGate.cs` holds
  `ModeGate` and nothing else. The file listing should tell you what the project contains
  without opening anything.
- **Folders group by responsibility, not by technical kind.** `Governance/` holding
  `ModeGate`, `WritePolicy` and `AuditTrail` together is right; `Interfaces/`, `Enums/` and
  `Classes/` scattering one feature across three folders is wrong.
- Interfaces are prefixed `I`: `IAuditTrail`, `IWritePolicy`.

---

# The governance layer — fail closed by construction

Everything above is style. This section is correctness, and it applies to
`src/TiaMcpServer.Portable/Governance/` above all, because a careless default there is not ugly
code, it is a safety hole.

## Anything not foreseen refuses

**No silent `default`.** A switch over `OperationMode`, over a policy decision, or over
anything that gates a write must be exhaustive and must fail loudly on an unrecognised
value. A new enum member must break the build or throw, never slip through as "allowed".

```csharp
// bad — a third mode added later silently behaves like Study
switch (mode)
{
    case OperationMode.Study: return Confirmation.Automatic;
    case OperationMode.Workshop: return Confirmation.Manual;
}
return Confirmation.Automatic;

// good
return mode switch
{
    OperationMode.Study => Confirmation.Automatic,
    OperationMode.Workshop => Confirmation.Manual,
    _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised operation mode: {mode}")
};
```

The same rule covers a policy lookup that finds no rule, a plan whose mode does not match
the session, and an audit write that fails in Workshop Mode: **the absence of a decision is
a refusal, never a permission.**

## `Result` for expected failures, exceptions for the unexpected

A plan rejected by the whitelist is not an exception — it is the system working. It returns
a result carrying the reason, so the caller can report it. Exceptions stay for what nobody
planned for: TIA Portal dying mid-operation, the database being unreachable.

This matters beyond taste: an expected refusal that arrives as an exception gets caught by
the single decoration point in the portal layer and reported as an operation failure, which
tells the caller to retry something it must not retry.

## One execution path

There is no branch that writes without producing and recording a plan first — not even in
Study Mode, where the plan is auto-confirmed. A "skip the checks" path would exist in the
Workshop build too, and an untested branch is the one that eventually runs with a machine
connected.

In practice: **a new MCP tool that changes anything calls `GuardedTool.Run`**, names its target
through `ChangeTarget`, and gets a test in `Test16GuardedWrites` asserting it refuses when the
policy says nothing about it. A write tool that forgets the guard passes every other test in the
suite, which is exactly why that class exists.

## Typed identifiers

`PlanId` is its own type, not a bare `Guid` or `string`. Passing the wrong identifier into a
confirmation is exactly the class of bug that must be impossible, and the compiler makes it
impossible for free.

## Facts that already happened are immutable

`AuditEntry`, and a `ChangePlan` once executed, describe the past. No public setters, fields
`readonly`, all state through the constructor.

## Never swallow an audit failure

`catch (Exception)` that hides a failed audit write is forbidden outright. In Workshop Mode
a failed audit write **refuses the action**; in Study Mode it proceeds and reports. Either
way it is visible.

## One explicit test per safety rule

Every rule in this section has a test that names it and would fail if the rule were removed:
the whitelist refusing an unlisted target, the exhaustive switch throwing on an unknown
value, the audit failing closed in Workshop Mode, Workshop Mode being unreachable in the
default build. Incidental coverage from a test about something else does not count — a rule
nobody asserts is a rule that quietly stops holding.

**MSTest, as everywhere else in this repository**, but in their **own project**:
`tests/TiaMcpServer.Governance.Test/`, named after what they test (`ModeGate.cs` →
`ModeGateTests.cs`) rather than the `Test<Area>.cs` numbering the Openness suite uses.

The separation is the requirement, not a preference. `TiaMcpServer.Test` starts a TIA Portal in
`[AssemblyInitialize]`, so every test in that assembly pays for one whether it needs it or not —
and cannot run at all on a machine without TIA Portal. **Governance tests must run without TIA
Portal**, because a safety rule that can only be checked on a licensed machine is a safety rule
that stops being checked. Nothing in that project may take a dependency that needs TIA Portal at
run time — **or at build time**, which is the harder half and the one that was broken until
2026-09-02. It references `TiaMcpServer.Portable` and never `TiaMcpServer`; see the dependency
rule above. The same holds for `tests/TiaMcpServer.Spec.Test/`.

---

# Domain rules — Openness

These are not style, they are correctness. Breaking them causes real failures.

- **Every TIA object is released with `using` or `Dispose()`.** Otherwise TIA Portal is left
  as a zombie process holding the licence and has to be killed from Task Manager.
  It is the most common failure and the most annoying to diagnose.
- **Never assume a block is consistent.** Check `IsConsistent` before exporting.
  TIA Portal will not export inconsistent blocks and the native error does not explain why.
- **Always full paths**: `Group/Subgroup/Name`. A bare name is ambiguous.
- **Every write is preceded by an export of the previous state.** If we are about to
  overwrite a block, save a copy first. No exceptions.
- **Zero hardcoded paths.** Everything through configuration or parameters.
- Never assume a project is open: validate the state first.

---

# Operational safety

- **Workshop Mode may only be used with a teacher or workshop supervisor physically present,
  with access to the emergency stop.** No software enforces this and none can. It is the one
  rule here that depends entirely on a person choosing to keep it, which is exactly why it is
  written down first.
- **Never** download to a physical PLC without explicit, unambiguous confirmation in that
  same conversation turn. A previous authorization does not carry over to the next time.
- By default, every deployment targets **PLCSIM Advanced**.
- Tools that write to the project must be idempotent or take a backup first.

---

# Errors

Categories:

- **Validation** — invalid input, missing resource → `PortalErrorCode.InvalidParams` → MCP `InvalidParams`.
- **Invalid state** — the item does not allow the operation (e.g. inconsistent block)
  → `PortalErrorCode.InvalidState` → MCP `InvalidParams` with actionable guidance.
- **Operation failure** — environment, I/O, underlying API → `PortalErrorCode.ExportFailed`
  → MCP `InternalError` with a concise reason.

Hard rules:

- **Never an empty `catch`.** Never swallow an exception.
- **Never `catch (Exception)` without rethrowing** outside the single decoration point.
- Do not use exceptions for normal control flow.
- The user-facing message is concise and actionable; the structured detail goes to the log.

Single decoration point: do **not** attach `Exception.Data` at the `throw` site.
Every public method of the portal layer attaches context in a **single catch block**
right before rethrowing:

```csharp
catch (Exception ex)
{
    var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

    pex.Data["softwarePath"] = softwarePath;
    pex.Data["blockPath"] = blockPath;
    pex.Data["exportPath"] = exportPath;

    _logger?.LogError(pex, "{MethodName} failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
    throw pex;
}
```

Consistency: individual exports throw `InvalidState` asking to compile first.
Bulk exports skip inconsistent items and report them in an `Inconsistent` list.

---

# Tests

- **MSTest** with `[TestClass]` and `[TestMethod]`.
- Files follow the `Test<Area>.cs` pattern. Assets in `assets/`.
- Naming: `Method_Scenario_ExpectedResult`.
  For example: `ExportBlock_InconsistentBlock_ThrowsInvalidState`.
- **AAA** structure: Arrange, Act, Assert, separated by blank lines.
- **One concept verified per test.** Several `Assert`s are fine if they check the same concept.
- Every bug fix ships with a test that failed before.
- Tests do not depend on execution order and do not share state.
- The `Portal` layer is the one that must be covered: that is where the logic lives.

## Execution policy

- Offer to run the tests, but **only run them after explicit confirmation**.
- They require TIA Portal installed, licences and assets. Do not assume they will pass.
- If the user declines, give concise instructions so she can run them herself.

```powershell
dotnet test
```

---

# Formatting and encoding — critical

- Preserve the existing indentation style.
- **Do not change the encoding**; keep the UTF-8 BOM where it exists.
- **`.gitattributes` and `.gitignore` must have no BOM.** Git does not strip it, so the byte
  order mark becomes part of the first line: a leading `#` stops being a comment and git
  parses the whole line as a rule. It fails with `is not a valid attribute name` and every
  rule in the file is suspect. Verified the hard way on 2026-08-12.
- **Keep Windows CRLF.** The C# files of the portal layer and their tests must keep CRLF:
  Siemens deployment scripts fail to parse LF.
- `.md` files committed from Windows are also CRLF and UTF-8 with BOM.

## Markdown

- `#` for headings, with a blank line after every heading block.
- Code blocks fenced with a language hint.

---

# Definition of "done"

A task is not finished until:

1. It compiles **without warnings**.
2. It has tests covering the happy path and at least one error case.
3. Public members have XML doc.
4. There is no dead code, commented-out code or orphan `TODO`.
5. Errors are mapped according to the model above.
6. `docs/STATUS.md` reflects the new state.

---

# Environment — known limitations

- User must be in the Windows group **`Siemens TIA Openness`** (needs re-login for the token).
- `TiaPortalLocation` → `C:\Program Files\Siemens\Automation\Portal V20`.
- TIA Portal asks for whitelist confirmation the first time an external app connects.
- Current MCP transport: **stdio**. With stdio, **all logs go to stderr**.
- Importing **LAD** blocks from SIMATIC SD documents requires the accompanying `.s7res`
  with en-US tags; without it, it fails (Openness limitation).
- `ExportBlock` requires a full path; a bare name is ambiguous.

If a command fails because of environment limitations, **do not retry destructively**:
report the exact failure and suggest alternatives.

---

# A note on rigour

These rules exist so the code survives growth and so failures show up at compile time
instead of in a PLC. They do not exist to produce ceremonious code.

If applying a rule literally makes the code **less** clear, say so and propose the
alternative instead of applying it blindly. Three layers of abstraction to read a file
are not clean code: they are the opposite.
