# Project status

> Living document. Update it at the end of every working session.
> Last updated: **2026-09-05**

## ▶ RESUME HERE

### McpServer.cs is split too — 2026-09-05

**2,176 lines against this repository's limit of 300; now 369, across ten files.** Same move as
`Portal`, made possible by the same thing: with no Openness type in this layer, splitting it was a
move of whole tools rather than a rearrangement of a tangle. On branch `work/mcpserver-split`,
uncommitted, on top of a `main` that already carries PR #14.

```
McpServer.cs            369   the connection, the session's state, the services the tools share
McpServerProject.cs     296   what is open, its tree, its snapshots and backups
McpServerDevices.cs     206   hardware and the software on it
McpServerBlocks.cs      430   reading and exporting blocks
McpServerTypes.cs       338   reading and exporting types
McpServerDocuments.cs   234   SIMATIC SD documents, and the .s7res check
McpServerSimulation.cs  194   reading PLCSIM Advanced
McpServerNetwork.cs     111   PROFINET topology and OPC UA interfaces
McpServerJobs.cs        107   polling a long operation
McpServerCell.cs         86   expanding a cell specification into SCL
McpServerWrites.cs     1176   everything that changes anything -- untouched
```

**`McpServerWrites.cs` was deliberately not touched, and it is the one file here whose boundary is
not about size.** A tool that changes anything calls `GuardedTool.Run`, names its target through
`ChangeTarget` and gets a test in `Test16GuardedWrites`; a write tool that forgets the guard passes
every other test in the suite. Keeping reads and writes in separate files means a new write tool
that lands anywhere else is a review finding on sight. Splitting that file by area would trade that
signal for shorter files, which is the wrong way round.

**`[McpServerToolType]` lives on one partial only.** It may appear once per class, and the SDK finds
the tools in the other nine files through it regardless of where they are written. Copying it into
each new file was the one thing that did not compile, and it failed loudly — CS0579 — rather than
quietly registering nothing.

**Checked rather than assumed.** 68 members before, the same 68 after, with an empty difference both
ways. **59 tools, and that count is not from reading the source**: the harness's `toolContract` test
starts the built server over stdio and asks it what tools it has, so the registration survived the
split in the only way that matters. Governance **118/118**, specification **44/44**, harness
**198/198**, TIA **154/158 in 7 m 31 s** with 4 skipped and 0 failing, no orphan process, 0 warnings.

**The next action.** Both oversized files are done, so the size rule in `CLAUDE.md` is met
everywhere it was being broken. What is left of the audit is **F5 and F6**, which were blocked on F2
and are now unblocked, and **F3 and F8 to F12**, which are smaller. The other open front is
**phase 5b, deployment** — the release artefact, the precondition check that fails closed, and
`INSTALL.md` followed from the top on a machine that has never built this project. That phase is
what turns this from something that runs here into something that can be handed over.

**Left running on the machine**: nothing.

---

### F2 is closed — 2026-09-05

**No Openness type crosses into `ModelContextProtocol/` any more.** Not a `using
Siemens.Engineering`, not a type named in a signature, not an engineering object reaching the layer
through a `var`. The finding that opened in the audit of 2026-09-02 and was half-closed on
2026-09-03 is finished. On branch `work/portal-dtos-types`, uncommitted.

```
types — the mirror of the blocks
  src/TiaMcpServer/Siemens/TypeDescription.cs   new  narrower than a block: a UDT has no language
  src/TiaMcpServer/Siemens/TypeDescriber.cs     new  its own class; the two share not one property

devices, software, projects and sessions
  src/TiaMcpServer/Siemens/ObjectDescription.cs new  one type for four things that read the same
  src/TiaMcpServer/Siemens/ObjectDescriber.cs   new  takes IEngineeringObject, the one place that fits

  src/TiaMcpServer/Siemens/Portal.cs            mod  GetType(s), ExportType(s), GetDevice(s),
                                                     GetDeviceItem, GetPlcSoftware, GetProjects,
                                                     GetSessions describe; the finders are private
  src/TiaMcpServer/ModelContextProtocol/McpServer.cs mod  the last nine translations, now two
  src/TiaMcpServer/ModelContextProtocol/Responses.cs mod  ResponseTypeInfo carries its path
  tests/.../Test2ProjectSession.cs, Test3Devices.cs, Test4Software.cs  mod  6 tests
```

**Everything is green.** Solution builds with **0 warnings**; governance **118/118**, specification
**44/44**, TIA **154/158 in 8 m 58 s** with 4 skipped and 0 failing, and no orphan portal afterwards.

**Two things worth keeping from how it went.** The type path walker was written as a copy of the
block one *after* that one was fixed, so it inherited the fix rather than the defect: the root
system group — "PLC data types" — is dropped, and
`GetType_ExistingPath_DescriptionCarriesItsFullPath` proves it on a path two groups deep. And the
one failure in the first full run was **the test, not the code**: `PLC_0` is a device *item* in that
project, not a device. Worth stating plainly, because a test that fails for its own reasons is the
one that teaches people to rerun until it passes.

**One judgement call to review rather than accept.** `ObjectDescription` is a single type for a
device, a device item, a PLC software and an open project, because what the portal reads about all
four is the same three values. Four identical classes would have been ceremony. The moment one of
them carries something the others cannot — a device's order number, say — it earns its own
description instead of a nullable property here that means nothing for the other three.

**And `Portal.cs` is split, in the same session and for the reason F2 had to come first.** It was
3,770 lines against this repository's own limit of 300; it is now **381**, across sixteen files by
responsibility:

```
Portal.cs                    381   the connection: attaching or starting, its state, disposing
PortalProject.cs             335   opening, retrieving, creating, saving, closing
PortalSession.cs             121   multiuser sessions
PortalDevices.cs             247   devices, device items, adding one, compiling hardware
PortalSoftware.cs            114   the PLC software and its compilation
PortalBlocks.cs              454   blocks
PortalTypes.cs               384   types
PortalDocuments.cs           407   SIMATIC SD documents (.s7dcl/.s7res)
PortalSourceCode.cs          116   WriteScl and the source snapshot
PortalSimulation.cs          209   PLCSIM Advanced
PortalNetwork.cs             144   PROFINET topology and the IO system
PortalOpcUa.cs                83   OPC UA server interfaces
PortalPathLookup.cs          291   resolving Group/Subgroup/Name
PortalSoftwareContainer.cs   146   a software path to its container
PortalRecursiveWalks.cs      110   the filtered hierarchy walks
PortalProjectTree.cs         242   the project tree
PortalSoftwareTree.cs        218   one software's tree
```

**A partial class rather than collaborating classes, deliberately.** All sixteen files work on the
same three fields — the portal, the project and the session — and their lifetime is why this class
exists: splitting that state across objects would multiply the places that can leave a zombie TIA
Portal holding the licence. `CLAUDE.md` allows either shape; this is the one that does not touch
state.

**Four files are still over 300** — blocks, documents, types, project. Splitting further would
separate an export method from its failure loop, which reads worse rather than better. The 300-line
rule is about classes; here the class is one and the unit of reading is the area.

**Nothing was lost, and that was checked rather than assumed**: the 117 members in `HEAD`'s
`Portal.cs` against the 129 now spread across `Portal*.cs` — the twelve extra being F2's `Find*`
methods — with an empty difference on the missing side. The `using` directives were pruned by the
compiler, not by eye: `IDE0005` is an error here, so the whole using block was copied into each new
file and the build was run in a loop until it stopped complaining. One `ICompilable` was pruned that
was needed; the next build caught it, which a visual review would not have.

**Verified against TIA Portal**: **154/158 in 7 m 53 s**, 4 skipped, 0 failing, no orphan process.

**The next action.** `McpServer.cs` is around 2,100 lines and is the same problem, now with the same
solution available: the MCP layer holds no Openness type any more, so `McpServerBlocks`,
`McpServerTypes`, `McpServerDevices`, `McpServerSimulation` and `McpServerJobs` are a move of whole
tools. `McpServerWrites.cs` shows the shape already, and the rule it enforces — a tool that changes
anything lives with the guarded writes — is worth keeping visible as the file it is. After that,
F5 and F6, which were waiting on F2.

**Left running on the machine**: nothing.

---

### 2026-09-05 — the first CI run, and it was right about both of us

**Continuous integration had never actually run until pull request #13 opened it.** The workflow was
written on 2026-09-02 and the note beside it said, honestly, that a hosted runner had not yet proved
anything. It has now, and it failed twice for two different reasons — neither of which any local
build or test run could have found.

**The harness job: seven tests that cannot run on a runner, failing instead of saying so.**
`mcpClient.test.ts` and `toolContract.test.ts` start the real `TiaMcpServer.exe`. A hosted runner
cannot build it — that is the same Openness-at-build-time constraint the safety job exists for — so
`resolveServerExecutable` threw inside their `before` hook, and the runner reported seven cancelled
tests and a red build. `serverExecutableAbsence` now asks the same question without throwing, and
both suites carry `{ skip: ... }` with the reason. On a machine that has built the solution they
still run: **198 of 198 here, 191 with the executable hidden, exit code 0 both ways.**

**The safety job: two style rules this machine's compiler does not enforce.** `IDE0040` on eleven
interface members, then `IDE0032` on `PlanId._value`. Both are errors here by policy —
`.editorconfig` sets `dotnet_style_require_accessibility_modifiers = always:error` and
`Directory.Build.props` sets `TreatWarningsAsErrors` — and neither fires locally, because
`AnalysisLevel` is `latest-all` and **the rule set is whatever the SDK in use happens to ship**. The
runner installs the newest 8.0.x; this machine has 8.0.405.

The interface members were given their modifiers. `IDE0032` was **suppressed with a justification
and a test**, which is the one case where suppressing beats complying: `PlanId` is a struct, so
`default(PlanId)` runs no constructor, and the auto property the analyzer offers would hand back a
null `Value` where the getter's `?? string.Empty` returns an identifier that matches nothing.
`PlanIdTests` now asserts that, so the suppression fails loudly if anyone accepts the offer.
Governance is **118/118**.

**A third round followed, and it is why the SDK is now pinned.** `CA2263` on
`HardwareContextTests`, wanting the generic `Assert.IsInstanceOfType<T>`. Three failures in one
afternoon, each a rule that exists on the runner and not on this machine, each found only by
pushing: `AnalysisLevel=latest-all` plus warnings-as-errors means **the rule set is whatever SDK the
runner happens to install**, and there is no way to enumerate it from here.

`global.json` now pins **8.0.405**, the SDK this machine already has, with `rollForward: disable`,
and `setup-dotnet` takes its version from that file instead of `8.0.x`. Both sides compile with the
same Roslyn, so a build that is green here is green there. Raising the floor becomes a deliberate
commit to `global.json` — validated locally, where the findings can be fixed in one pass, rather
than discovered one push at a time. **Taken on 2026-09-05, on the user's decision**, with the cost
stated: new analyzer rules no longer arrive on their own.

The three fixes stay regardless of the pin, because all three were improvements: explicit
accessibility on interface members, a justified suppression defended by a test, and the generic
assertion overload.

---

### Where the work stopped — 2026-09-03, end of session

**The second half of F2 is under way: `PlcBlock` no longer leaves the portal layer, and
`ModelContextProtocol/` no longer contains a single `using Siemens.Engineering`.** Everything is in
the working tree and nothing is committed; the branch is `work/portable-split-and-audit`, whose
twelve earlier commits are pushed and ahead of `main`.

```
the block descriptions
  src/TiaMcpServer/Siemens/BlockDescription.cs        new  what a block is, once it is detached
  src/TiaMcpServer/Siemens/BlockGroupDescription.cs   new  a group and everything under it
  src/TiaMcpServer/Siemens/ObjectAttribute.cs         new  one attribute, read rather than referenced
  src/TiaMcpServer/Siemens/BlockDescriber.cs          new  the translation, in the layer that may do it
  src/TiaMcpServer/Siemens/EngineeringAttributeReader.cs new  moved down from Helper.cs
  src/TiaMcpServer/IsExternalInit.cs                  new  so a DTO can be immutable on net48

the option that was an Openness enum in the MCP layer
  src/TiaMcpServer/Siemens/ImportDocumentOption.cs    new  string to ImportDocumentOptions, with the aliases
  tests/TiaMcpServer.Test/Test23ImportOptions.cs      new  8 cases, one of them the refusal

the layers themselves
  src/TiaMcpServer/Siemens/Portal.cs                  mod  GetBlock, GetBlocks, GetBlockHierarchy,
                                                           ExportBlock(s), ImportBlocksFromDocuments
                                                           all return descriptions; the finders are private
  src/TiaMcpServer/ModelContextProtocol/McpServer.cs  mod  eight copies of the same translation, now one
  src/TiaMcpServer/ModelContextProtocol/McpServerWrites.cs mod  passes the option as a word, maps PortalException
  src/TiaMcpServer/ModelContextProtocol/Responses.cs  mod  ObjectAttribute, BlockGroupDescription, Path
  src/TiaMcpServer/ModelContextProtocol/Helper.cs     del  its two methods now live in the portal layer
  src/TiaMcpServer/ModelContextProtocol/Types.cs      del  Attribute shadowed System.Attribute; gone
  tests/TiaMcpServer.Test/Test4Software.cs            mod  4 tests on what a description must carry
```

**Everything is green.** Solution builds with **0 warnings**; governance **113/113**, specification
**44/44**, TIA **148/152 in 6 m 08 s** with 4 skipped and 0 failing, and no orphan portal process
afterwards — the process list was checked, not assumed.

**A new test found a real defect rather than confirming the refactor.**
`GetBlock("1_Tests/FC_Block_1")` described that block with the path
`Program blocks/1_Tests/FC_Block_1`, which no tool accepts as input: the inherited group walker
tests the *original* group for `PlcBlockSystemGroup` instead of the one it has climbed to, so it
never stops below the root. The suggestions that `ExportBlock` offers on a not-found path came from
that same call, so every "Did you mean" it has ever printed was unusable. `GetBlockPath` now walks
with `GetUserBlockGroupPath` and stops at the system group. `GetPlcBlockGroupPath` was deliberately
left as it is: it lays out the directories of a preserve-path export, where the extra folder is
harmless and changing it would move every file an existing snapshot already wrote.

**A test that passes by not running is worse than one that fails.** The first version of
`Test23ImportOptions` named `ImportDocumentOptions` in a `[DataRow]` and in a method signature.
VSTest reads that metadata while discovering tests, before `AssemblyInitialize` has resolved
Openness, so the whole class was skipped with a message in the log and a green summary. The options
travel through those tests as strings now.

**The next action, exactly.** The same treatment for what is left, in this order, one Portal method
at a time with its `Test<Area>` case: **types** (`GetType`, `GetTypes`, `ExportType`, `ExportTypes`
— the mirror image of the blocks, so `TypeDescription` and a `TypeDescriber` alongside the ones
written today), then **devices** (`GetDevice`, `GetDeviceItem`, `GetDevices`), then
**project and session** (`GetProjects`, `GetSessions`, `GetPlcSoftware`). Those nine methods still
hand Openness objects to `McpServer.cs`, which reads them through
`EngineeringAttributeReader.Read` — the reference is gone from the `using` list but the objects
still cross, and F2 is not closed until they do not.

**Left running on the machine at the end of the session**: nothing.

---

### Where the work stopped — 2026-09-02, end of session

**Everything below is in the working tree and nothing is committed.** `main` is at `83918a1` and the
tree has not been staged. Twenty-one paths, in three groups that could be three commits:

```
knowledge layer, stage 3 and the extraction repair
  harness/knowledge-eval/questions.json          new   34 answerable + 20 unanswerable
  harness/src/knowledge/retrievalGate.ts         new   the judging, pure and testable
  harness/src/knowledge/retrievalReport.ts       new   npm run knowledge:gate
  harness/src/knowledge/pageText.ts              new   the control-character repair
  harness/test/retrievalGate.test.ts             new   13 tests
  harness/test/pageText.test.ts                  new   10 tests
  harness/src/knowledge/pdfPages.ts              mod   applies the repair on extraction
  harness/package.json                           mod   knowledge:gate script
  docs/KNOWLEDGE-LAYER.md                        mod   stage 3 marked done

audit finding F1 — the data race
  src/TiaMcpServer/Governance/ChangePlanStore.cs  mod  one lock, and why not a concurrent collection
  src/TiaMcpServer/Governance/JsonlAuditTrail.cs  mod  appends serialised; VerifyChain tightened
  tests/.../ConcurrentWriteTests.cs               new  5 tests; all 5 fail without the locks

audit findings F7 and F4 — CI, the hash chain, and the gate that reads it
  .github/workflows/ci.yml                        new  208 tests on every push
  src/TiaMcpServer/Governance/AuditChain.cs       new  hashing, canonical form
  src/TiaMcpServer/Governance/AuditChainReport.cs new  what a check found
  tests/.../AuditChainTests.cs                    new  10 tests, each tampers then checks
  harness/src/auditChain.ts                       new  the same chain, verified in TypeScript
  harness/test/auditChain.test.ts                 new  14 tests against fixtures .NET wrote
  harness/test/assets/audit-chain-golden.jsonl    new  a chained trail, from System.Text.Json
  harness/test/assets/audit-chain-escaping.json   new  the escaping vector, same provenance
  harness/src/auditTrail.ts                       mod  readAuditChain
  harness/src/gateEvidence.ts                     mod  the chain is gathered with the entries
  harness/src/gate.ts                             mod  criterion 3 fails on a broken chain
  harness/test/gate.test.ts                       mod  2 tests: broken chain, unattested history

audit finding F2, first half — the assembly split
  src/TiaMcpServer.Portable/**                    new  everything that never touches Openness
  src/TiaMcpServer/TiaMcpServer.csproj            mod  references it; the four folders left
  tests/*.Test/*.csproj                           mod  they reference Portable, not TiaMcpServer
  .github/workflows/ci.yml                        mod  a third job: 149 safety tests, no TIA
  CLAUDE.md, README.md, docs/ROADMAP.md           mod  the paths and the dependency rule

audit of 2026-09-02 - eight fixes, one finding left open
  src/TiaMcpServer.Portable/Siemens/NameFilter.cs  new  a caller's regex, bounded and validated
  tests/.../NameFilterTests.cs                     new  4 tests, one runs (a+)+$ and demands it end
  src/TiaMcpServer.Portable/Governance/GuardedWrite.cs  mod  a Study-mode audit failure is reported
  src/TiaMcpServer.Portable/Governance/TargetPattern.cs mod  anchored exactly
  src/TiaMcpServer.Portable/Knowledge/HarnessHardwareLookup.cs mod  the timeout now bounds a hang
  src/TiaMcpServer/Siemens/Portal.cs               mod  7 regex sites through NameFilter
  src/TiaMcpServer/ModelContextProtocol/McpServerWrites.cs mod  3 empty catches, now logged
  tests/.../GuardedWriteTests.cs, WritePolicyTests.cs, HarnessHardwareLookupTests.cs  mod  4 tests
  harness/src/knowledge/pdfPages.ts                mod  destroys the loading task, not the document
  harness/package.json, package-lock.json          mod  pdfjs 6.3.289, and npm audit fix
  src/TiaMcpServer/TiaMcpServer.csproj             mod  off the previews; the CVE pinned

docs/STATUS.md                                    mod  this file
```

**Everything is green.** Solution builds with **0 warnings**; governance **113/113** and
specification **44/44**, none skipped; the TIA suite **136/140 in 5 m 56 s**, 4 skipped and 0
failing, with no orphan portal process afterwards; harness **198/198**; dashboard **11/11** plus a
clean `vite build`; both typechecks clean. **Zero known vulnerabilities** in NuGet and npm, where
there were four this morning.

**The TIA suite was run after the audit changed `Portal.cs`, and it exercises the change rather than
merely surviving it.** `GetBlocks_Regex_ReturnsMatchingBlocks` runs `^F.+` and the empty filter
against a real project, and `GetBlocks_RegexMatchingNothing_ReturnsEmptyList` runs `^NoSuchBlockName$`
— so `NameFilter` is proved on the matching path, the non-matching path and the match-everything
path, through Openness. What no test covers is the single-block lookup with regex characters in the
name; that path is compiled and reasoned about, not exercised.

The retrieval gate reports 91% precision over n=34 and 95%
abstention over n=20, and the workshop gate reports 4 of 5 criteria met —
criterion 5, the in-person review, is the one left.

**The next action, exactly.** The second half of **F2**: Openness types still cross into the MCP
layer. `Portal` returns `PlcBlock`, `PlcType`, `Device`, `PlcSoftware`, `PlcBlockGroup` and
`ProjectBase` from about fifteen public methods, and `McpServer.cs` reads their properties at
roughly forty sites, so `ModelContextProtocol/` keeps four `using Siemens.Engineering` that
`CLAUDE.md` forbids. Removing them means giving those methods DTO return types — the translation
`Helper.cs` already does, moved to where it belongs. **Attack it one Portal method at a time**, each
with its `Test<Area>` case, rather than as one change: this is inherited code, and the suite that
would catch a mistake takes ten minutes and needs the licensed machine in front of you.

**F4 and the first half of F2 are closed** as of this session. F5 and F6 depend on what is left of
F2; F3 and F8 to F12 are smaller.

**Left running on the machine at the end of the session**: nothing. The TIA suite closed its portal,
and the process list was checked rather than assumed.

---

**2026-09-02, evening — a full audit of the repository. Eight things fixed, and the worst of them
was a comment that promised something the code did not do.**

**The finding that matters most is in the governance layer, which is otherwise the best code here.**
`CLAUDE.md` says a failed audit write refuses the action in Workshop Mode and, in Study Mode,
proceeds *and reports* — "either way it is visible". The Study-Mode `catch` was **empty**, under a
comment claiming the failure was "reported through the outcome". Nothing reported it. Entries could
go missing from the trail that the workshop gate reads to decide whether a machine may be switched
on, and criterion 2 counts silent failures it could not see. The mechanism to fix it was already
written and unused: `ChangeOutcome.Applied` takes a `detail` nobody passed. `Record` now returns the
reason instead of swallowing it, and all four paths carry it; on the throwing path it rides in
`Exception.Data`. **The existing test passed with and without the bug** — it asserted only that the
write still ran — which is why two new ones name the rule directly.

**The timeout on the hardware lookup did not bound the failure it was written for.**
`StandardOutput.ReadToEnd()` returns only when the child closes the pipe, so a Node process that
started and then hung never reached the `WaitForExit` below it, and the write tool that asked for a
citation waited for ever. Both pipes are now drained asynchronously. Proved by mutation: with the old
code the new test does not finish and VSTest aborts a blocked test host.

**A caller's regular expression could freeze the whole server.** Seven sites compiled `regexName`
with no match timeout, inside the Openness gate — so `(a+)+$` does not slow a listing down, it stops
every other tool in the process from reaching TIA Portal, silently. Three of those sites also caught
`Exception` and *skipped the item*, returning a short list that looked complete; two returned `null`,
which reads as "no such block". `NameFilter` validates, compiles with one second of patience, and
refuses with `InvalidParams` — which is what a mistyped bracket is.

**The whitelist matched a string it was never shown.** `TargetPattern` anchored with `^`/`$`, and in
.NET `$` also matches before a trailing newline. No escalation was found — allow and deny behave the
same way — but exactness is the whole product here. Now `\A` and `\z`.

**Four vulnerabilities, now none.** `Microsoft.Bcl.Memory 9.0.5` (GHSA-73j8-2gch-69rq, CVSS 7.5,
denial of service) arrives through the MCP SDK and is pinned to 10.0.11 until that SDK is upgraded.
`pdfjs-dist` carried arbitrary JavaScript execution on opening a malicious PDF, which is precisely
what the knowledge layer ingests; it went to 6.3.289 and **the extraction is byte-identical across
all three manuals**, so the index needs no rebuild. That upgrade also exposed a real leak:
`PDFDocumentProxy.destroy` was removed in pdfjs 6, and destroying the loading task is what releases
the worker. `fast-uri` and `qs` were fixed by `npm audit fix`.

**Two production libraries were running on .NET 10 previews** — `Microsoft.Extensions.Hosting` and
`System.Text.Json` — with 10.0.11 stable available and `net48`-compatible, checked in the `.nuspec`
before the change.

**And the one deliberately left open.** `Portal.cs` is 3558 lines and `McpServer.cs` 2346, against
the repository's own limit of 300. That is the same problem as the second half of F2 — Openness
types cross into the MCP layer because `Portal` hands them over — and breaking it apart in an audit
would trade a known, contained problem for an unknown one. It is the first recommendation instead.

**What must not be updated**: `Siemens.Collaboration.Net.TiaPortal.Packages.Openness` offers 21.0,
which is for TIA Portal V21. This project targets V20. Here "the latest version" is the wrong answer.

**Verified against TIA Portal afterwards**: 136/140, 4 skipped, 0 failing, 5 m 56 s, no orphan
process — the same result as before the audit, from a suite that really does drive the changed
filter code.

The full report is an artifact rather than a repository document, for the same reason the August one
was: it is a point-in-time review, not a living rule. What became a rule is in the code and its tests.

---

**2026-09-02, later — the safety rules now run on continuous integration, and the first half of F2
is what put them there.**

`TiaMcpServer.Portable` is a new assembly holding everything in the server that never touches TIA
Portal: `Governance/`, `Knowledge/`, `Spec/`, `Jobs/`, the error model, `GuardedTool`,
`ChangeTarget`, `OpennessGate`, `OpennessLease` and `SimulationTagValueParser`. Both test projects
reference it and no longer reference `TiaMcpServer`. **Governance 105/105 and specification 44/44 —
149 tests — build and run against an assembly whose dependency graph contains the word "Siemens"
zero times**, which was checked rather than assumed: `project.assets.json` and the output folder
were both searched.

**The audit's own claim about F2 was too small, and so was this document's.** Moving only
`Governance/` and `Knowledge/` would have left `JobStoreTests`, `OpennessGateTests`,
`GuardedToolTests` and the whole of `Spec.Test` behind — 53 of the 149 — because they cover files
that would have stayed in the assembly that needs a licence to compile. Every one of those files
turned out to be free of Openness too; `JobStore` only mentions it in a comment. So the cut is
wider, and it is still clean.

**Two analyzer findings were fixed rather than suppressed**, because the new project has no debt
ledger and is not going to grow one. `PortalErrorCode` and `PortalException` came from upstream
with no XML doc and now have it, including why the standard exception constructors are deliberately
absent: they would build a failure with no error code, and the default value of that enum is
`NotFound`, so a caller that forgot the code would report every failure as a missing item.
`SclTemplateExpander` held no instance state and is now static, which removed a pointless `new` at
sixteen call sites.

**What the CI job cannot prove from here.** It has not run: the only machine available has TIA
Portal installed, so "builds without TIA Portal" is shown structurally — no Siemens package in the
graph, no Siemens assembly in the output — and the first push is what proves it end to end. The job
is deliberately two `dotnet test` invocations rather than `dotnet build TiaMcpServer.sln`, which
would fail on the resolver, correctly.

**The rule that keeps it true is in `CLAUDE.md`**, because nothing enforces it: pointing the test
projects back at `TiaMcpServer` would break no build and would silently take 149 safety tests off
continuous integration.

---

**2026-09-02 — F4 is finished. The workshop gate now verifies the hash chain it used to ignore, and
the hard part was not the hashing.**

Criterion 3 read the audit trail and checked that every recorded backup was still on disk. It never
looked at the hashes the server chains the trail with, so **a trail with an entry rewritten, or one
removed, reported MET**. The chain made tampering detectable in August; this is what detects it.

`harness/src/auditChain.ts` recomputes what `JsonlAuditTrail` wrote: sequence, `prev`, SHA-256 over
the canonical JSON array of
`[seq, prev, timestamp, planId, mode, tool, target, value, backupPath, origin, outcome, detail]`.
`readAuditChain` reads the file, `gatherEvidence` gathers the verdict beside the entries, and
criterion 3 fails on a broken chain. The gate that says shut in a terminal and the one in the
browser share that gathering, so they cannot disagree.

**The hard part was that `JSON.stringify` and `System.Text.Json` do not produce the same bytes**,
and the hash is taken over bytes. The .NET default encoder escapes `<`, `>`, `&`, `'`, `"`, `+` and
a backtick, escapes every non-ASCII character, and writes its hexadecimal in upper case; JavaScript
escapes almost none of that. **Every entry ever written carries a plus sign** — it is the `+` of the
timestamp's UTC offset — so the obvious implementation would have reported line 1 of every trail in
existence as a forgery.

**That was measured, not reasoned about.** Every code point from U+0000 to U+00FF was serialised
through `System.Text.Json.dll` in `src/TiaMcpServer/bin/Debug/net48/` — the assembly the server
itself is built against, loaded into PowerShell — and the escaping was read off the result. The two
fixtures under `harness/test/assets/` were produced the same way and are committed as .NET wrote
them: the tests assert against bytes from the other side of the boundary, not against what the
TypeScript happens to produce.

**14 tests on the TypeScript side and 10 on the C#, and they were checked by breaking the code.**
Making the escaping match `JSON.stringify` fails 11 of the 14; removing the stripped-chain guard
fails the two tests that name it, in both languages; making criterion 3 ignore the chain again fails
exactly the gate test that names it. Every mutation was reverted and the suites re-run green:
harness **198/198**, governance **105/105**, both typechecks clean, solution **0 warnings**.

**The harness found a hole in the C# verifier, and the C# verifier was tightened to match.**
`JsonlAuditTrail.VerifyChain` used to skip any line with no `hash` field wherever it sat and count
it as history from before chaining, so the cheapest forgery of all was to edit an entry and then
delete its chain fields. **Unattested history can only be a prefix of the file** — chaining was
switched on once, and everything written afterwards carries it — so both verifiers now refuse a line
with no chain fields once the chain has begun, in the same words.

**How big the hole actually was, measured rather than asserted.** The mutation test says it
precisely, and the first version of this entry overstated it. Stripping an entry *in the middle* was
caught even by the old code, but by accident and with the wrong diagnosis: the **next** entry was
reported as removed or inserted, because its sequence no longer followed, which sends a person to
the one line that is genuine. Stripping the **last** entry was missed outright — nothing follows it
to leave a gap, and the trail reported intact. There is now a test for each, on both sides.

Writing the second verifier is what found this. Neither implementation checks the other by accident:
they were built from the same specification in different languages, and the disagreement was the
whole value.

**What it says about the real trail today**: 386 recorded backups all present, the chain intact over
**0** entries, and **8206** earlier entries that predate chaining and are not attested. That is
honest and it is also nearly empty: the server has not written a line since chaining shipped, so
nothing is attested yet. The first real run changes that.

---

**2026-08-29, late — an external audit was run over the whole repository, and its first finding was
a real data race on the write path. It is fixed, and the fix is proved.**

The audit raised twelve findings. The one that mattered was **F1**: `ChangePlanStore` held its
pending plans in a plain `Dictionary` with no lock, and `JsonlAuditTrail` appended with no lock,
while both are singletons reached from two threads:

```
StartAsJob -> JobStore.Start -> Task.Run(work)     thread pool
   -> GuardedWrite.Propose -> _plans.Add(...)
ApplyChange                                        protocol thread
   -> GuardedWrite.Confirm -> _plans.Take(id)
```

**This was not theoretical, and the proof is the point.** Five concurrency tests were written, the
locks were then removed, and the suite was run again: **all five failed, none passed.** Two of the
failures say exactly what was at stake:

- `Add` threw `ArgumentException: destination array is not long enough` from inside
  `Dictionary.Resize` — the dictionary corrupting its own storage rather than failing cleanly. In
  the server that is a hang, holding the TIA Portal licence.
- `Take` handed **the same plan to more than one thread on 7 of 200 rounds**. In production that is
  one human approval executing an approved change twice.

**A lock, not a concurrent collection**, and the reason is in the class doc: `Take` is a lookup, a
removal and an expiry check that have to happen together, and `Pending` has to describe one moment.
A `ConcurrentDictionary` makes each operation atomic and leaves both of those compound operations
racy. Nothing runs while the lock is held — `Take` hands the work out and it is executed outside, so
a slow compile never blocks another thread.

**Green**: solution builds with **0 warnings**, governance tests **95/95** (90 before, 5 new), none
skipped.

**The other findings, in the order they should be taken.** F7 continuous integration (327 of 453
tests need no TIA Portal and nothing runs them automatically today), F4 hash-chaining the audit
trail so tampering is detectable rather than merely discouraged, F2 the four `using
Siemens.Engineering` in the MCP layer that `CLAUDE.md` forbids, then F5 and F6, which depend on F2.
F3, F8 to F12 are smaller. The full report is an artifact rather than a repository document, because
it is a point-in-time review and not a living rule.

**F7 is done, and building it corrected the audit's own arithmetic.** `.github/workflows/ci.yml`
runs the harness and the dashboard on every push: typechecks, **193 tests**, and the dashboard bundle
— because a dashboard that type-checks and then fails to bundle is broken for everyone but the person
running the dev server. Both jobs were executed locally, step for step, before the file was written.

The audit claimed 327 tests could run on a hosted runner. **That was wrong, and the reason matters.**
`TiaMcpServer.csproj` references the Openness resolver, which locates `Siemens.Engineering.dll` from
an installed TIA Portal at *build* time, and both C# test projects reference that assembly. So the
139 governance and spec tests need no TIA Portal to **run** — that separation is real and holds — but
they cannot be **built** without one.

**Which weakens a stated safety property.** `CLAUDE.md` says a safety rule that can only be checked
on a licensed machine is a rule that stops being checked, and that is the situation today in
practice. It is fixable: `Governance/` and `Knowledge/` contain **no `using Siemens.Engineering` at
all**, and `PortalException` is a plain exception. Extracting them into an assembly that does not
reference the resolver would put every safety rule in the repository onto a hosted runner. That is
now the strongest argument for doing F2.

The workflow does not declare a self-hosted Windows job. A job whose runner does not exist queues
forever and reports nothing, which is worse than a gap that says what it is.

**F4 — the audit trail is now chained, and tampering is detectable.** Every line carries a sequence
number, the hash of the previous line, and its own SHA-256 over both plus its ten values. Editing an
entry breaks its own hash; removing one leaves a gap in the sequence; cutting and re-joining the file
breaks the back-pointer. Eight tests, and **each one tampers first and checks second** — a chain that
has never been shown to catch a forgery is decoration.

Two decisions worth recording:

- **The hash is taken over a canonical JSON array, not over the line's text.** Two serialisers can
  order keys differently and produce different bytes for the same record, which would report
  tampering that never happened — the worst possible failure for a check whose only value is that
  people believe it.
- **It detects, it does not prevent**, and it does not stop somebody who recomputes the whole chain.
  That needs a key this machine does not have and a place to keep it that is not this machine. The
  class says so rather than implying more.

**Run against the real trail**: 8 206 existing entries, appended to and verified —
*"chain intact over 1 entry; 8 206 earlier entries predate chaining and are not attested."* History
is reported as unattested rather than rewritten to look verified, and every existing reader still
parses the lines.

**Green**: solution **0 warnings**, governance **103/103** (95 before, 8 new), none skipped.

**Next action.** F4 has a second half: the workshop gate reads the trail in TypeScript, so criterion
3 still reports MET on a file whose chain is broken. Verifying the chain in `harness/src/auditTrail.ts`
and folding it into that criterion is what turns *detectable* into *detected*. After that, F2 — which
now carries the argument above as well as its own.

**2026-08-29, night — the retrieval gate is built, it opened, and it caught a defect in stage 1 on
the way.**

The brief says *do not proceed past this stage without it*, so this was the next thing after stage 2.
Fifty questions, two metrics, two thresholds fixed **before anything was measured**.

```
MET      citation precision: 90% of n=30, 70% required
MET      correct abstention: 95% of n=20, 90% required
```

**The thresholds were written in the cold**, the way `RequiredCompleteRuns` was: 70% precision, 90%
abstention. Abstention is the harder of the two on purpose — the brief says staying silent well is
what makes a retriever trustworthy — and a test asserts that ordering, so it cannot be inverted by a
diff. Both must be met, with no averaging between them.

**The ground truth comes from the documents, not from the retriever.** Pages were sampled across the
three manuals and read; the questions were written from that text without the search ever being
consulted, because authoring them by asking the index would approve it by construction. The runner
re-checks every recorded page and phrase against the corpus before scoring, and **refuses to report a
rate** if any has drifted.

**The one abstention failure is worth naming.** Asked about a **UR20** — right manufacturer, a robot
the indexed manual does not cover — it quoted the UR5e manual instead of staying silent. Brand-level
near misses are the residual weakness, and they are the class most likely to mislead somebody who
trusts the name on the citation. The three precision misses are all table-heavy pages: a C4000 pin
assignment and two DSBC ordering tables.

**The defect, and it is a real one.** Writing the ground truth turned up something no query can work
around: **PDF extraction leaves control characters inside tokens.** `EN ISO 13855` is stored as
`EN ISO 13855`, `IEC 61496-1` as `IEC 61496D1`. Measured: **1 626 occurrences on 165 of
538 pages**, all of it in C4000 (1 229) and DSBC (397); the UR5e manual has none.

That corrupts precisely what the lexical half of the search was built for. The stage 1 note justifies
the trigram vector by its ability to match `6ES7214-1AG40` against `6ES7 214-1AG40-0XB0` — and a
standard number split by a control character defeats BM25 tokenisation and trigrams alike. **An exact
technical reference into those two documents cannot match today.** It is a **stage 1 ingestion
defect**, it is written down rather than quietly fixed, and the 90% above was measured with it
present — so the number is honest and probably pessimistic.

**Green**: harness **172/172** (159 before, 13 new), typecheck clean. The server and the dashboard are
untouched by this stage.

**Then the control characters were fixed, and the fix exposed a hole in the questions.**

`repairPageText` runs on every page as it leaves the extractor. A control character **between two
digits is removed** — there it is splitting one number — and anywhere else it becomes a space, where
it stands in for a separator and joining the words would manufacture a token the document does not
contain. The rule is read off this corpus rather than derived from the PDF specification, and each of
its ten tests is one of the cases it was read off.

**It does not guess, deliberately.** The same `U+0002` stands for a space in `VDMA 24562` and for a
hyphen in `NF E 49-003.1`. Putting the hyphen back would be authoring text into a corpus whose entire
value is that it is quoted verbatim, so the hyphen stays lost and the reference becomes findable,
which is the half that can be had honestly. The leftover `IEC 61496D1` — a `D` where a hyphen belongs
— is the same class of fault and is left alone for the same reason.

Index rebuilt from the three PDFs: **0 control characters over 538 pages**, and `EN ISO 13855` is one
token again.

**And then the gate reported exactly the same two numbers, 90% and 95%.** A repair of 1 626
corruptions that moves no metric is a fact about the *questions*, not about the repair: not one of
the fifty depended on an exact standard number — the very thing the trigram vector exists for. Four
bare-reference questions were added and checked against the index as it was before the repair:

| question | before | after |
|---|---|---|
| `EN ISO 13855` | UR5e p84, C4000 p72, C4000 p10 | **C4000 p40** |
| `EN ISO 13857` | UR5e p84, C4000 p72 | **C4000 p40** |
| `IEC 61508` | **not found** | **C4000 p72** |
| `VDMA 24562` | **not found** | **DSBC p2** |

Four misses became four hits:

```
MET      citation precision: 91% of n=34, 70% required
MET      correct abstention: 95% of n=20, 90% required
```

**Green after the repair**: harness **182/182** (172 before, 10 new), typecheck clean.

**Next action.** Stages 0, 2b, 4 and 5 of the knowledge layer are unauthorised and stage 3 no longer
blocks them. Otherwise: the API budget is roughly €3.5 and only Haiku 4.5 fits
a publishable model sample; phase 5b is deployment; and criterion 5, the review with the teacher, is
still the only thing between this project and an answered workshop gate.

**2026-08-29, later — stage 2 of the knowledge layer is built: a change plan now cites the manual.**

Authorised this session and done. `GuardedWrite.Propose` asks the documentation index about a change
before it makes the plan, and the plan carries what came back — verbatim excerpts with document,
version and page, or an honest silence.

**Where the citation is attached is the whole safety argument.** Not in each write tool, where a
citation would be attached at sixteen call sites and forgotten at fifteen, but inside the one
execution path every write already passes through. A new write tool gets cited context by existing.

**Crossing the language boundary, and why it is a subprocess.** The index and its ranking are
TypeScript in `harness/src/knowledge/`; the plan is C#. `HarnessHardwareLookup` runs the harness
lookup that was already there and reads its JSON. The alternative — reimplementing BM25 and the
trigram vector in C# — would create two rankings that have to agree, and two rankings that have to
agree eventually do not. The cost is that Node becomes an **optional** dependency of the server.

**A failed lookup can never stop a write, and that has its own test.** Missing Node, missing index,
unreadable answer, timeout at 15 s: every one produces `Unavailable` carrying the reason, and the
change proceeds uncited. `HardwareContextOutcome` keeps `NotFound` and `Unavailable` apart on
purpose — the first says something about the corpus, the second about this machine, and one
sentence for both would let a broken lookup pass for a documented silence.

**A refused change is never looked up.** It is not going to happen, so its plan says the lookup never
ran rather than showing an empty result that reads like a silence.

**Measured, not assumed: one lookup costs 298 ms**, so a write phase whose mean was 2.28 s grows by
roughly 13%, and an iteration of 18.2 s by under 2%. **The phase timings published above are from
before this change and are not comparable with what the next run will record.**

**The honest limitation, and it is a large one.** The corpus is a UR5e manual, a SICK C4000 and a
Festo cylinder. The harness's own specifications write SCL to block paths, so for *those* changes the
index will almost always answer not-found — correctly. The S7-1200 system manual is the document that
would make these plans cite something, and Siemens still answers 403 without a login. The machinery
is built and verified end to end; what it has to talk about is missing.

**Verified by running it, not by compiling it.** `HarnessHardwareLookupTests` spawns the real Node
process against the real index: one question the corpus covers comes back cited with a page, and
*"what is the capital of France"* comes back not-found through the process boundary. It reports
**inconclusive** rather than failing on a machine with no Node or no index, so the governance suite
still runs anywhere.

**One rule was consciously not applied, and it is written down rather than hidden.** `GuardedWrite`
now takes five constructor parameters against the repository's limit of four. A parameter object
would move the same five arguments one level out and add a class that means nothing on its own; the
five are distinct interface types, so a wrong order does not compile, which is the hazard the limit
exists for. The reasoning is in the constructor's own doc comment.

**Green**: solution builds with **0 warnings**, governance tests **90/90** (67 before, 23 new), and
none skipped — the two real-lookup tests ran. Harness 159/159 and the dashboard are untouched.

**Next action.** Stage 2b, 0, 3, 4 and 5 of the knowledge layer are still unauthorised, and stage 3
— the retrieval gate, fifty questions with the correct page known in advance — is marked *do not
proceed past this without it*. Otherwise: the API budget is roughly €3.5 and only Haiku 4.5 fits a
publishable model sample; phase 5b is deployment; and criterion 5, the review with the teacher, is
the only thing between this project and an answered gate.

**2026-08-29 — the gate was reading two experiments as one, and it was the last place that did.**

The session began by going to recover criterion 2, which this document said the interrupted Sonnet
repetition had broken. **It had not broken it.** The store holds **0 iterations and 0 runs with no
outcome**; run 57's eighteen iterations all carry one and the run itself closed as `failed`. The
entry below was written from what the interruption looked like rather than from what it left, and
this is the correction. Criterion 2 is met, and nothing had to be done to it.

**What had actually failed was criterion 4**, and the reason is the one the measurement filter was
built to make impossible:

```
NOT MET  4. a stable clean-compilation rate across the last 20 runs
         100% over runs 1-10 of the window, 43% over runs 11-20, tolerated fall 10%
```

| half of the window | runs | generators | clean |
|---|---|---|---|
| first | 38–47 | 10 × `stub` | 100% |
| second | 48–57 | 3 × `stub`, 5 × `claude-opus-5`, 2 × `claude-sonnet-5` | 43% |

Nothing regressed. **The generator changed in the middle of the window**, and the criterion reported
the distance between a pattern expander and a model as a fall within one process. That 43% describes
neither the expander, which is still at 97%, nor the models.

**Why the filter did not reach it.** Yesterday's work gave `specificationStatistics` and
`phaseDurations` a `MeasurementFilter`, but the gate does not read either: it reads
`runStatistics()`, and that query did not select `r.generator` at all. The gate was the one consumer
left blending — and the most expensive one, because it is the query that decides whether Workshop
Mode may be enabled.

**What was done, and what was deliberately not done.** `RunStatistics` now carries the generator,
through both stores. Criterion 4 checks the window before judging it and, when it spans more than
one generator, **declines to produce a rate** and says so by name. It returns *unmet*, not *skipped*:
"cannot be judged" is a reason to keep the door shut, never a reason to stop counting the criterion.
Nothing was loosened, no threshold moved, and no generator was chosen as the one that counts —
choosing which experiment the gate judges is choosing the answer. It becomes judgeable again after
twenty consecutive runs of one generator, which is a run somebody has to do rather than a constant
somebody has to argue with.

Three tests name the rule: the refusal on a mixed window, a mixed window whose two halves *agree* on
the rate and would otherwise have read as stable, and a homogeneous window still being judged
normally. The second is the one that matters — without it the refusal could be deleted and the suite
would stay green on a coincidence.

**The gate now says the true thing:**

```
MET      1. 57 complete run(s) of 50 required
MET      2. 0 iteration(s) with no outcome
MET      3. 246 recorded backup(s), all present
NOT MET  4. the last 20 runs span 3 generators (claude-opus-5, claude-sonnet-5, stub),
            and a rate across them describes none of them
NOT MET  5. no review recorded, and no measurement can stand in for one
```

**Green**: harness **159/159** (156 before, 3 new), dashboard 11/11, both typechecks clean.

**Then the twenty runs were done, and criterion 4 is met. Only the teacher is left.**

Twenty repetitions of the six specifications with the pattern expander, runs 58–77, **120 of 120
passed**. The window is homogeneous again and the rate is judgeable:

```
MET      1. 77 complete run(s) of 50 required
MET      2. 0 iteration(s) with no outcome
MET      3. 386 recorded backup(s), all present
MET      4. 100% over runs 1-10 of the window, 100% over runs 11-20
NOT MET  5. an in-person design review with the supervising teacher
```

**Four of five met, and the fifth is the one no measurement can move.** The gate is still shut, which
is the correct answer.

**120 of 120 is not too good to be true, and it was checked rather than assumed.** The twenty runs
recorded 140 iterations: 120 `passed` and 20 `compiler-errors`. Those twenty are the first attempt of
`two-station-recovers-from-a-broken-first-attempt`, one per run — the specification that exists to
fail once and be fixed, averaging exactly 2.0 attempts. Everything else passed first time. What is
genuinely absent is **`download-failed`: zero across the twenty**, against 27 in the first fifty runs.
That is the PLCSIM download fix holding over a second, larger sample.

**The numbers moved and the README moved with them**, per generator, which is now something the store
can answer:

| `stub` | before | now |
|---|---|---|
| Runs | 50 | **70** |
| Attempts | 158 | **278** |
| Compiled cleanly | 154 (97%) | **274 (99%)** |
| Passed on a simulated CPU | 126 (80%) | **246 (88%)** |
| One iteration | 19.7 s | **18.2 s** |

The models are untouched beside it and stay unpublishable: `claude-opus-5` 6 of 30 clean over 5 runs,
`claude-sonnet-5` 2 of 12 over 2, and a third of Opus's generations were refusals that never happened.

No TIA Portal or `TiaMcpServer` process was left behind; the licence is free.

**Next action.** The API budget is roughly €3.5 and only Haiku 4.5 fits a publishable model sample;
stage 2 of the knowledge layer still needs authorising; phase 5b is deployment; and criterion 5 is
the review with the teacher, which is now the only thing between this project and an answered gate.

**2026-08-28, night — the model generator was measured for the first time, and it cost more in
findings than in euros.**

Two samples ran against a model instead of the pattern expander. Neither number is publishable yet,
and the reasons why are the work.

**Opus 5 refuses about a third of these requests.** Fifteen of forty-six generations came back with
`stop_reason: refusal`, no content blocks and **zero output tokens**. Isolated with four probes
costing about €0.25 in total, and the bisection is unambiguous: the same prompt on **Sonnet 5**
answers normally, and *"say hello"* on Opus 5 answers normally — what it refuses is being asked for
PLC code. So the raw 4 of 30 that Opus scored is **not a measure of capability**: a third of its
attempts never happened. It is not published anywhere and must not be.

**A defect of ours came out with it, and it was flattering the bill.** A generation whose answer
held no SCL threw a plain error and took its usage with it, so the store recorded the attempts that
worked and none of the ones that did not — 46 generations run, 31 costs recorded. `UnusableGeneration`
now carries the cost out of the failure, `loop.ts` records it on both paths, and the failure message
names `stop_reason`, the block types and the text length. That message is what turned ten euros of
mystery into four cheap probes, and it is why the next unexplained sample will not be paid for twice.

**Before either sample could run, the statistics had to learn what a generator is.**
`specificationStatistics` and `phaseDurations` read every iteration in the store with no filter, so
the first model run would have blended two experiments **in the README's table, in the dashboard and
in the copilot's brief at once** — the exact thing the README says produces a number about neither.
They now take a `MeasurementFilter`; `/api/metrics` takes `?generator=` and **refuses** an unknown one
rather than quietly returning the blend; the response always names the generator, `all` included; the
Metrics view has a picker and states the mix; and the copilot's brief warns when its rates span more
than one. The store separates by **model id**, not by `stub`/`model` — a run's generator is recorded
as `claude-opus-5`, so every model stays its own sample.

**What Sonnet 5 actually did**, n=11, sample stopped early on purpose when the budget got close:

| | `claude-sonnet-5` | `stub` |
|---|---|---|
| Passed | **1 of 11** | 126 of 158 (80%) |
| Compiler errors | 30 of 33 attempts | — |
| Compiled and the cell misbehaved | 1 | the gap between 97% and 80% |
| Cost | **$5.17** over 32 generations | nothing |

The one pass is worth looking at: `four-station-runs` failed to compile, was handed TIA's own errors,
and passed on the second attempt. That is the loop doing exactly what the README claims. What it does
not do is work every time.

**The 97% / 80% of the expander did not move**, and that is the separation working: still 50 runs,
still 158 attempts, untouched by 7 model runs sitting beside them.

**One consequence to carry forward: the interrupted run leaves a permanent mark.** Stopping the
second Sonnet repetition mid-iteration left one run and one iteration with no outcome, so **criterion
2 of the gate goes from met to not met** until a complete run follows. Nothing was deleted to tidy
that away — a run nobody knows the end of is exactly what that criterion exists to notice.

**Green**: harness **156/156**, dashboard 11/11, both typechecks clean, server 0 warnings. Two TIA
Portal processes and one `TiaMcpServer` were left behind by the kill and were closed by hand; the
licence is free.

**Next action.** The measurement to finish is a model sample big enough to publish, and the API
budget is down to roughly €3.5 — Haiku 4.5 at about €0.02 a generation is the only one that fits, and
it generates without thinking, which is a weaker generator and has to be said. Otherwise: stage 2 of
the knowledge layer, phase 5b, or criterion 5 with the teacher.

**2026-08-28, end of day — fifty runs. Criterion 1 is met, and only the teacher's review is left.**

Ten runs in twenty minutes took the store to **50 complete runs, 158 specification attempts**. Four
of the five gate criteria are now met, and the fifth is the in-person design review — the one thing
in this project no measurement can move.

```
MET      1. 50 complete loop runs in Study Mode — 50 of 50
MET      2. zero silent failures
MET      3. complete audit — 191 backups, all present
MET      4. a stable clean-compilation rate — 100% over both halves of the window
NOT MET  5. an in-person design review with the supervising teacher
```

**The numbers moved, and the README moved with them**: 154 of 158 compiled cleanly (97%, was 96%),
**126 of 158 ran on a simulated CPU and behaved as specified (80%, was 67%)**, one iteration 19.7 s
(was 22.0 s). The per-specification and per-phase tables were re-read from the store rather than
adjusted. The 80% is still the pattern expander's, and the README still says so above the table.

**The jump from 67% to 80% is not tuning.** The ten new runs had **zero failed downloads**, against
21% of attempts in the first forty. What changed was the PLCSIM download fix, and this is the sample
that shows it held.

**The first attempt at these runs was refused, correctly, and nothing was contaminated.** The command
went out without `--policy`, so the guard refused `UseTcpIpNetworkMode` with *"no policy is configured
for Study mode, so nothing is permitted in it"*, the run died before writing a single row, and the
store stayed at forty. `harness/policy.json` is part of the experiment, not a machine's
configuration — a run whose permissions differ from another's is not comparable with it. The same
mistake is recorded further down this file from a previous session, which is the second time it has
cost a run.

**2026-08-28, later — stage 1 of the knowledge layer is built, and it cites three real manuals.**

The only authorised stage of `docs/KNOWLEDGE-LAYER.md` is done. There is a local index in
`harness/src/knowledge/`, a `hardware-lookup` skill in `.claude/skills/` — the repository's first —
and a corpus of **three documents, 538 pages, 1 124 chunks**, indexed in eleven seconds: the UR5e
user manual (SW 5.16), the SICK C4000 operating instructions and the Festo DSBC documentation.

**No manual is in the repository.** `harness/corpus/recipe.json` holds each document's URL, version
and SHA-256, and everything else under `harness/corpus/` is ignored. Ingestion refuses a file whose
hash is not the hash in the recipe, and it fetches nothing itself — that is stage 4, with a
whitelist and a quarantine. The S7-1200 system manual is absent because Siemens Industry Online
Support answers **403** without a login; the other three manufacturers serve theirs directly.

**One thing is deliberately not what the brief said, and it is in the README-honesty category.** The
vector half of the "hybrid" search is a hashed character-trigram vector computed locally — a lexical
signal, **not a semantic embedding**. Anthropic publishes no embeddings API and the alternative was a
second provider and a second key. It catches `6ES7214-1AG40` against `6ES7 214-1AG40-0XB0`, which is
where BM25 scores zero; it does not understand a paraphrase, and a test asserts that it does not.

**The first version did not abstain**, and it was found by running it: *what is the capital of
France* returned three excerpts from a robot manual, because `capital` is rare enough to rank well
in five hundred pages. A ranking says which chunk is least bad, never whether any is good. Coverage
of the question's meaningful words is now a precondition of quoting anything, and that question is a
test by name.

**Green**: harness **146/146** (111 before, 35 new), typecheck clean, index builds with no warnings.
Everything in this stage runs without TIA Portal and without the corpus — the tests build their own
index from text they write themselves.

**Next action.** Nothing further in the knowledge layer is authorised: stage 2 (cited hardware
context in the `ChangePlan`) is the cheapest and most visible, stage 0 (project review on connect)
depends on nothing, and both need a decision. Otherwise the options from this morning still stand:
ten more runs closes criterion 1, phase 5b is deployment, and criterion 5 is the in-person review.

**2026-08-28 — phase 5 is done, and run 40 is on the board.**

PR #10 is merged, so the dashboard, the read API, model generation, schema version 2 and the docked
copilot are all on `main`. Phase 5 was the next thing chosen and it is finished: the README leads
with the pitch, carries the measured numbers, shows a real run end to end, states the security
model, and documents Workshop Mode as a roadmap item with its five entry conditions and their
current state.

**The recording is a real run, and it counted.** A full six-specification run went into `metrics.db`
while the README was being written, with the `stub` generator so the sample stays homogeneous.
**6 of 6 passed.** That makes it **run 40 of the 50** criterion 1 wants, and every number in the
README moved when it finished — 98 attempts, 94 clean compilations (96%), 66 passed (67%), one
iteration 22.0 s.

**What this phase actually caught was three false claims**, which is what "anything the repository
does not do comes out of the README" is for:

- **Tag table export/import was advertised and does not exist.** No tool, no import, and `TagTable`
  appears only inside `SourceSnapshotExporter`. It had been in the README for a long time.
- **My own draft put "an LLM writes PLC code" above the 96%/67% table**, which is the pattern
  expander's. A reader would have taken those as a model's numbers. Fixed before the table, not
  after.
- **A generation was priced at "$0.008 on Opus-class models".** The measured $0.0079 was Haiku 4.5.
  The Opus number is an estimate and now says so.

`McpServerWrites.cs` had a matching problem and was corrected: its doc comment claimed *everything*
in the file calls `GuardedTool.Run`, and `ApplyChange` does not — rightly, being the confirmation
half of the guard. The exception is now named, because the file exists to be checkable by eye.

**Green**: server builds with **0 warnings**, harness 111/111, dashboard 11/11, both typechecks
clean. `dashboard/.vite/` is now ignored.

**Next action — pick one.** Ten more runs closes criterion 1 and is roughly 40 minutes of machine
time; criterion 5, the in-person review with the teacher, is the one nothing here can move. Or
phase 5b, deployment, whose blocking criterion needs a machine that has never built this project.
Or stage 1 of the knowledge layer, the only stage authorised.

**2026-08-27, last thing — the copilot's chat exists, docked in the corner of every view.**

The chat was cut this morning because the machine had no API key, and the roadmap recorded the cut
with the condition that would reverse it: *"reversible for the price of a key"*. The key and the
credit arrived this afternoon, so the condition was met and the thing was built.

**It is not a tab.** It started as one and was moved on request: a round robot button bottom right,
on every view, opening a panel that keeps its conversation when you switch views. `CopilotDock` is
rendered by `App` as a sibling of the whole tab strip, which is what stops the tabs unmounting it -
and it is deliberately not in the address bar, because it is not a place, it is a thing that is
always there. Verified by asking a question on Metrics and finding the answer still on screen after
switching to Workshop gate.

**A turn costs $0.0019 to $0.0022** on Haiku 4.5 — measured, not estimated, and about a fifth of the
1.5 cents the roadmap guessed. Most of it is the brief, which is sent with every question.

**How it is kept honest**, because a chat on this dashboard is the one page that could invent
something about a cell a person later stands next to:

- **It is given no tools.** `copilotSender.ts` passes none. There is no MCP client behind it, no
  path to TIA Portal and no write to the store, so the dashboard's guarantee — every write goes
  through the guard in the server — is kept by construction rather than by asking the model nicely.
- **`/api/chat` is a POST, and the 405 was narrowed rather than removed.** Every other path still
  refuses anything but GET, and a POST anywhere but `/api/chat` is refused by name. A second endpoint
  taking a body would have to be added deliberately, in front of the comment that says why.
- **The brief is its whole world.** `copilotBrief.ts` assembles it from the same reader the GET
  endpoints serve, so a number in an answer and the same number in a table come from one query. Every
  rate carries its sample size, because the copilot cannot quote one it was never sent.
- **It refuses safety questions**, verified against one that insisted it already knew what it was
  doing: it answered that safety goes to the documentation and to the supervisor who is present, and
  stopped. That is the knowledge layer's cardinal rule holding before the knowledge layer exists.
- **The conversation lives in the tab.** No state on the server, nothing in the audit trail. What
  somebody typed into a box is not a measurement.

**A seventh defect, found the way the other six were — by running it.** The first version of rule 1
told the model to name the tab that *would* answer a question it could not. Asked for the PLC's IP
address and a sensor count, it invented that these were on *Live run* and *Workshop gate*. Small, and
on the one page whose entire purpose is not inventing. The rule now says to stop at "I do not have
that", and states that this dashboard holds runs of the loop and no hardware facts at all. Re-asked:
it declines and sends the question to the documentation.

**Green**: harness **111/111**, dashboard **11/11**, both typechecks clean, `vite build` clean, and
the dock rendered headless with **no console errors** — asked through the actual text box and got
*"The download phase takes longest: mean 13.66 s over 90 sample(s)"* and *"two-station-recovers-from-
a-broken-first-attempt passes least often: 12 passed over 27 attempts"*, both of which are what the
store holds.

**Next action is unchanged and now overdue: the branch and the pull request.** Five sessions of
uncommitted work. After that, a full six-specification run with retries into `metrics.db`.

**2026-08-27, later — the credit is in and the loop has run against a real model, end to end.**

Twenty euros of API credit were added, and the whole path was exercised for the first time. It
works: Haiku 4.5 was asked for a specification and wrote SCL, the server compiled it, and the run
reported what it cost. **The generation cost $0.0079.**

The run was deliberately the cheapest that proves anything: **one specification, one attempt, no
retry**, into a **scratch database in the temp directory rather than `metrics.db`**. That last part
is not tidiness. Criterion 1 of the workshop gate counts any run that has an outcome, *without
looking at how many specifications it ran* — so a one-specification smoke run would have counted
towards the fifty exactly as a six-specification run does, and inflated the gate with something that
never exercised it. The thirty-nine recorded runs are untouched.

- **The governance layer refused the first attempt, correctly.** `--policy` was not passed, and
  `UseTcpIpNetworkMode` came back `Refused`: *"no policy is configured for Study mode, so nothing is
  permitted in it"*. Fail-closed doing its job on the first real occasion it had, and it cost nothing
  because it stopped before a token was spent. The policy the harness runs under is
  `harness/policy.json`, which is versioned on purpose — it is part of the experiment.
- **The specification did not pass**, which is the honest and expected outcome of a single attempt:
  `Data type "FB_TwoStationCell" is not allowed here` in `DB_TwoStationDemo/Interface`, one error and
  three warnings. Whether the retry closes it is the next thing to measure, not to assume.
- **A defect found the way all six before it were found — by running code nobody had run.** The cost
  report said *"no price on file for that model"*. `--model claude-haiku-4-5` is answered by
  `claude-haiku-4-5-20251001`, and the resolved id is what the store records; the price table is
  keyed by the alias. **Every real generation would have reported no price** — the feature worked
  only for models nothing had ever asked. `modelPricing.ts` now resolves an eight-digit snapshot
  suffix and *only* that: a lookup that trimmed until it matched would price an unknown model at a
  known model's rate, which is the exact failure the file exists to prevent. Verified against the
  recorded row, not a fixture: 611 in and 1450 out at the 2026-08-27 list prices is **$0.007861**.

**Green**: harness **93/93** (two new, both of which fail without the fix), typecheck clean.

**Next action: the branch and the pull request.** This is four sessions of uncommitted work — the
dashboard, the read API, model generation, schema version 2, `KNOWLEDGE-LAYER.md` — and the diff is
the review. After that, a full six-specification run with retries enabled, into `metrics.db`, which
is the first one that legitimately counts towards the gate.

**2026-08-27, end of day — the key is in, the plumbing is proven, and the account has no credit.**

The key was added and the model path was run for real, which is the only reason any of the rest of
this is worth reading. It authenticates: `models.list` answers, and `claude-opus-5`,
`claude-sonnet-5` and `claude-haiku-4-5` are all reachable from the workspace it belongs to. A
generation then fails, and not on anything in this repository — **`400 invalid_request_error`:
"Your credit balance is too low to access the Anthropic API"** (request `req_011CeTfrmwFu`).

**The API console and claude.ai are two separate wallets, and the balance Samreen was looking at is
the other one.** The 81 € of usage credits belong to the Claude Code subscription; the API
organisation has its own, at zero. So `--generator model` remains unrun against a real generation,
and it now stops for a reason that costs nothing to fix and is written on the screen.

**Where the key lives changed, on purpose.** It was set as a Windows user environment variable,
which every process that account starts inherits — Claude Code included, which would then bill the
API instead of the subscription, quietly. It is now `harness/.env`, which `.gitignore` has covered
since line 56 and which nothing read until today, loaded by `node --env-file-if-exists=.env`
through the new `npm run run` script. The user variable was deleted. The doc comment that said not
to put a key in a file in this repository is updated rather than ignored: a gitignored file read by
one command is the smaller of the two exposures, and the other one was live for a day.

- **`--model <id>`**, so a run can be pointed at something other than Opus 5. Haiku 4.5 is a fifth
  of the price per generation, which matters for the many attempts it will take to get the loop
  through end to end. `--model` with `--generator stub` is **refused**, not ignored: a flag that
  silently did nothing would produce a run somebody files as a measurement of a model.
- **Every generation now records what it actually cost**, which `STATUS.md` asked for in as many
  words this morning. A `token_usage` row per attempt — the counts the API returns, not an
  estimate — and the run report prints tokens and dollars per model. **No cost is stored**: a price
  is a fact about a day, so it is applied where the number is read, from one dated table, and
  correcting that table corrects every run already recorded.
- **Schema version 2**, and it *migrates* rather than refusing. Version 2 only added a table, so no
  column of the thirty-nine recorded runs means anything different; the refusal is kept for changes
  that do reinterpret a column, and the list of what may be migrated from is one line a reader can
  check.
- **A 400 that would have arrived a minute into a run, found before it did.** The sender always
  sent `thinking: { type: 'adaptive' }`, which models older than the 4.6 generation reject — and
  `--model` is exactly what made those reachable. It is sent only where it is accepted now, and an
  unknown model gets no thinking parameter rather than a guessed one, because omitting it is valid
  everywhere and guessing is a request that cannot succeed.

**Green**: harness 91/91, typecheck clean. The estimates from this morning survive contact with the
price list — Opus 5 at $5/$25 per million against Haiku 4.5 at $1/$5 is the factor of five that was
quoted — but they are still estimates until a generation runs, and now the harness will record the
real number the first time one does.

**Next action: buy API credit in the console** (Plans & Billing, workspace `wrkspc_01B8y5kA`), set a
monthly spend limit there while doing it, then `npm run run -- --archive <zap20> --generator model
--model claude-haiku-4-5` for the first real generation. Then the branch and the pull request: this
is four sessions of work now.

**2026-08-27 — phase 4 is closed. The chat is cut, and the half that could be built is built.**

The roadmap named four views and one of them was *the plant copilot: chat plus live loop phase*. The
chat needs a model and this machine has no key, so building it would have meant shipping code nobody
had run — which this repository has paid for five times and written down as a lesson. **The chat is
cut, today, deliberately**; it was already cut number 1 on the roadmap's own list. The other half
exists as the **Live run** view, and it is named that rather than "Copilot" so the tab does not
promise something that is not behind it.

- **`/api/iterations/{id}/phases`** — what one attempt has got through, per iteration rather than
  averaged over a run.
- **The view never claims to know which phase is running.** A phase reaches the store when it *ends*,
  in a finally block — which is what makes a phase that threw get recorded at all. So the one in
  flight shows as "not reported yet", and the unaccounted time is stated as unaccounted. Inventing a
  current phase from elapsed time would be a guess dressed as a measurement, on the one screen
  somebody watches to know whether a controller is being written to.
- **Watched updating itself, not assumed**: with the page open, a phase written into the store
  appeared on screen without a reload — 3 finished phases became 4, 156 ms became 276 ms, and the
  indicator counted the update. Done against a **copy** of the store; the real one still holds 39
  runs and 0 iterations without an outcome.
- One duplication removed: the phase chart was on both Overview and Metrics, drawn from the same
  endpoint. Metrics is about specifications now.

**The model generator's seam was sharpened while it is still cold.** Samreen's decision of
2026-08-27: the API key waits until the product is further along, and the work is built on the
understanding that it *will* be integrated. So the one thing that would have gone wrong on the day
it is, was fixed today — a run asked for `--generator model` read the key **after** opening TIA
Portal, so a forgotten variable cost a forty-five second startup before saying so. It is read first
now, before the executable, the store or the portal, and an empty value counts as missing: a profile
that sets `ANTHROPIC_API_KEY=` to nothing otherwise fails much later as an authentication error that
blames the key rather than saying there is not one. Three tests name the rule.

Cost, so the decision can be revisited on numbers rather than nerves: **around 5-10 $ a month** at
two sessions a week — a generation is roughly 4.5 cents on Haiku and 22 on Opus 5, a chat turn about
1.5 cents on Haiku. There is no subscription; an unused month costs nothing. **These are estimates
from inferred token counts, not measurements.** The API returns real usage on every response and the
store already has a row per attempt, so the first thing to do with a key is record what it actually
costs and stop guessing.

**Everything green before the branch**: harness 76/76, dashboard 11/11, `vite build` clean, and the
C# solution builds with **0 warnings**.

**Next action: the branch and the pull request.** Three sessions of work. Then the knowledge layer's
stage 1, whose first file was written and then moved back out — it does not belong in this commit.

**2026-08-27, later — the scope grew, on purpose and in writing.**

`KNOWLEDGE-LAYER.md` is a new work brief: hardware documentation retrieval and a cited pre-flight
review, delivered as Claude Code skills. It is not numbered as a roadmap phase — it runs alongside
and is cut from the bottom up. **Only stage 1 is authorised**; nothing of it is built yet.

Read back against the request that produced it, the brief was missing two things, and both were
added as stages rather than folded into others: **stage 0**, a read-only review of a project the
server has just been pointed at, and **stage 2b**, showing the plan for a whole job before the first
write instead of only recording it. Three places where the brief deliberately differs from what was
asked are now written down as such, the first being that it will not tell you how to connect a
cable — it shows you the page of the manual that does.

`ROADMAP.md` also gained **phase 5b, deployment**, which carries the user guide with it: a *Guide*
view in the dashboard that renders `INSTALL.md` itself rather than a second copy of it, plus a live
check of what the machine it is running on actually has. The file is what somebody reads on GitHub
before any of this runs; the view is what they keep open once it does. The constraint that shapes it: the server cannot
run anywhere except a Windows machine that already has TIA Portal licensed, so this is a desktop
tool to distribute, not a service to host. Its blocking criterion is that the smoke path runs from
the release artefact on a machine that has never built the project.

**2026-08-27 — the dashboard is live, and it is built to be understood rather than only correct.**

Two things landed today. The API now **streams**: a run in progress moves on screen instead of
needing to be reopened. And the interface was rebuilt on Tailwind, shadcn/ui and Chart.js, on
Samreen's instruction — a dashboard that explains itself, not a set of tables that happen to be
accurate.

- **Server-sent events on `/api/live`**, and the event carries *no data*: it says the store changed,
  and the page re-reads the endpoints that already serve the numbers. One path produces a number, so
  the stream cannot disagree with the table.
- **Five views**: Overview (new), Runs, Metrics, Audit trail, Workshop gate. Every panel carries one
  sentence saying what it is claiming — and that sentence is a required parameter of the panel, not
  an optional one.
- **Charts chosen by the job the data does**: stacked bars for part-to-whole, single-hue bars for
  magnitude, a line for the trend, a meter for 39-of-50, stat tiles where the data is one number.
  The palette is a validated colour-blind-safe ordering, and nothing on the page says anything by
  colour alone.
- **A dark theme**, whose steps are chosen for a dark surface rather than flipped.
- **Harness: 72 cases, 72 passing. Dashboard: 11, 11 passing.** Neither needs TIA Portal.
- **Running it found five more defects that compiling and testing had not.** All five below.

**Next action: this is still uncommitted.** Two sessions of work sit in the working tree on `main`;
it needs a branch and a pull request before anything else.

**2026-08-26 — phase 4 begins: the dashboard exists and shows the real 39 runs.**

Built in two halves, data first. An HTTP API in `harness/` over the store the runs already write —
six endpoints, read-only by construction, loopback only — and then `dashboard/`, React and Vite
against it. Three views draw recorded data and one draws the gate; the fourth the roadmap names, the
copilot's chat, is not there and is the roadmap's own first thing to cut.

- **Harness: 66 cases, 66 passing**, up from 50. **Dashboard: 11 cases, 11 passing.** Neither suite
  needs TIA Portal.
- **`npm run api`** in `harness/`, then **`npm run dev`** in `dashboard/`, and the page is on
  `http://127.0.0.1:5173`.
- The workshop gate gives the same verdict in the browser as `npm run gate` does in a terminal,
  because both go through one gathering rather than two copies of it. Both were run: 39 of 50, shut.
- **Running it found three defects that reading it had not.** All three below, all fixed.
- Nothing in the C# solution was touched.

**Next action: the live loop.** A run in progress is only visible by reopening it, which is where the
WebSocket half deferred out of phase 3 belongs. Then the copilot's chat, if it is not cut.

**2026-08-26 — the download is fixed and phase 3 is all but closed.**

Two things happened, in this order. The download that had failed six runs in a row turned out to be
ours: a virtual controller keeps a storage directory named after the instance, `UnregisterInstance`
leaves it behind, and the next controller of that name inherits it. `DeleteInstance` now calls
`IInstance.CleanupStoragePath()`. Then everything phase 3 still owed was built on top of that.

- **The loop passes 2 of 2** on a simulated CPU again, and the fix is covered by a test that would
  have failed before it.
- **Harness: 50 cases, 50 passing**, no TIA Portal needed. **Six specifications**, up from two.
- **`npm run gate`** answers the five workshop criteria from recorded data, and answers *no*.
- **59 tools**, counted from the source: `DescribeSimulationConnection` is the one this session added.
- **0 warnings** across the solution, `TreatWarningsAsErrors` on.

**2026-08-21 — the cell runs. A numbered piece goes through both stations on a virtual CPU.**

Phase 2's last open item is closed, and it is the one that mattered: until today the handshake
between one station and the next was asserted nowhere but in the compiler. It is now asserted on a
controller in RUN.

- **TIA suite: 136 cases, 132 passing, 4 skipped, 0 failing, 9 m 50 s.** No orphan portal.
- **Governance suite: 63/63**, and **Spec suite: 35/35**, both without TIA Portal.
- **55 tools**, up from 52: `ListSimulationTags`, `ReadSimulationTags`, `WriteSimulationTag`.
- **0 warnings**, `TreatWarningsAsErrors` on, across the whole solution.

Phase 1 was merged as pull request #6, phase 2 and the tag work as #7, the phase 3 harness as #8,
and **the download fix and the rest of phase 3 as #9 on 2026-08-26**. Nothing is left unmerged.

What closed it was tag access, which the server did not have. Note 14 of 2026-08-13 had measured
that reading tags was not independent of the download; the download works since 2026-08-17, so it
was possible now. See "The cell runs" under phase 2, including the two things that could only be
settled by running and the one property of the pattern that a test found.

See "Phase 2" below. What follows is the phase 1 record, kept because the diff of #6 is large and
this is written to be read alongside it.

**Phase 1: every deliverable it promised exists, and the calendar is gone.**

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

**Phase 2 and the tag work were merged as pull request #7 on 2026-08-21.**

**2026-08-26 — the download is fixed, and the cause was ours. The loop passes 2 of 2 again.**

A virtual controller keeps a storage directory named after the instance, under
`Documents\Siemens\Simatic\Simulation\Runtime\Persistence\`. `UnregisterInstance` does not remove
it, so `DeleteSimulationInstance` was leaving half a megabyte of state behind, and the next
controller created with the same name **adopted it** — its first download then failed with
`Connect to module failed`. `DeleteInstance` now calls `IInstance.CleanupStoragePath()`, and so does
the rollback path when creating one fails. See "The download, and the state a name inherits" below.

**Phase 3 is complete except for two things that need something this machine does not have:** an
unattended batch to turn the loop into a rate, and an `ANTHROPIC_API_KEY` so the model generator
can run against the real API. Everything else the roadmap named for the phase exists and is
tested — repetitions, the workshop gate, six specifications, the model generator behind a seam.
See "2026-08-26 — what phase 3 still owed" under Phase 3.

**Phase 4 is where the WebSocket telemetry was deferred to.** Everything before it is merged.

**Standing debt, deliberate and dated:** the model generator has never run against the API. See
"Deferred on purpose" under Phase 3. Nothing depends on it.

One decision is waiting and is the user's: **the mode selector**, see "The mode may only be changed
with the cell empty" under phase 2. It blocks nothing.

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

## Phase 4 — the dashboard (started 2026-08-26)

`dashboard/` does not exist yet. What exists is the half it reads from, built first on the user's
decision: an API over the data the harness already records, so the front end is written against
endpoints that return real numbers rather than against a shape somebody imagined.

`npm run api` in `harness/` serves it. It is **read-only, and that is a property rather than a
setting**: there is no endpoint here that changes anything and there will not be one. Every write in
this project goes through the guard in the MCP server, and a second door into the same project that
did not would be exactly the untested branch the governance rules forbid. When the dashboard needs
to confirm a change it will call the server's own `ApplyChange`.

### What it serves

| Endpoint | Answers |
|---|---|
| `/api/runs` | every run, newest first, with what it attempted and how much of it compiled |
| `/api/runs/{id}` | one run, its iterations, and what each phase cost inside it |
| `/api/metrics` | per specification: attempts, clean compilations, passes, mean iterations to a clean compilation — each with its sample size |
| `/api/audit` | the audit trail, filtered by `mode`, `tool`, `outcome` or `target` |
| `/api/gate` | the five workshop criteria and the verdict |
| `/api/mode` | what the permanent banner shows |

It was run against the real store of the 39 recorded runs, not only against tests. The numbers it
returns for run 39 are download 11.2 s and verify 2.8 s mean over six samples each, compile 611 ms
over seven — which is the first time the shape of where the minute goes has been visible at all.

### Four decisions, and the reason for each

1. **The interface is `127.0.0.1` and is not a flag.** What this serves is a record of everything
   the server has changed and where every backup of it lives. Making the interface configurable
   would put exposing it to a classroom network one typo away.
2. **The mode banner never guesses.** `/api/mode` reports what the audit trail was *observed* in.
   An empty trail answers `unknown`, a mode the harness does not recognise answers `unknown`, and
   Workshop wins over Study when both appear. It never answers `Study` because it does not know: a
   safety notice that assumes the safe answer is worse than no notice. The authority on a live
   session stays `GetOperationMode` on the server, and the endpoint says so in its own payload.
3. **An unknown filter is a refusal, not an empty filter.** `?tol=CreateBlock` answers 400 and lists
   the filters that exist. Answering it with the whole trail would show an audit that looks as
   though it recorded no such restriction, which is the one thing an audit view may never do.
4. **The gate is gathered in one place.** `gateEvidence.ts` assembles what `evaluateGate` judges,
   and both `npm run gate` and `/api/gate` go through it. Two callers that each assembled the
   evidence themselves would agree until one was changed, and a gate that says *shut* in a terminal
   and *open* in a browser is worse than no gate. Both were run: both answer 39 complete runs of 50,
   and shut.

### What the reader is, and one thing it would have got backwards

`MetricsReader` opens its own connection to the store the harness writes, which is what the WAL
journal mode set a week ago was for: the dashboard can read while a run is still going.

Two rules in it are worth naming, because both are the kind that a test catches and reading does
not. **A missing store is refused, not created** — a mistyped path would otherwise read as a run
that measured nothing, which looks exactly like a run that measured nothing. And **the list handed
to the gate is reversed**: runs are listed newest first for a screen, but criterion 4 slices the end
of its list to get the last twenty, so handing it that order would compare the two halves of the
window backwards and call a falling rate a rising one. There is a test on each.

**Harness: 64 cases, 64 passing**, up from 50, none of them needing TIA Portal.

### 2026-08-27 — the live stream, and what it refuses to carry

`/api/live` is server-sent events, which is what the roadmap deferred into this phase as "the
WebSocket half". SSE rather than a WebSocket because the traffic goes one way — a browser has
nothing to tell this server that a GET does not already say — and because it is plain HTTP that
reconnects on its own, which matters when the API is restarted as often as it is while being
written.

**The event carries the token and nothing else.** Not the new run, not the row that changed. What is
on screen is re-read from the endpoints that already serve it, so there is exactly one path that
produces a number. A stream carrying its own copy of a measurement would be a second way to produce
it, and two ways to produce a number become two different numbers the day one of them is changed.

The token is `r39:39/i123:123/p520`: the highest identifier in each table, and the count of rows
that have an outcome. Both halves are needed and the second is the subtle one — an iteration *ends*
by having `outcome` written into a row that already exists, and no identifier moves when that
happens. A token built from identifiers alone would show a run advancing through its phases and
then never notice it finish. Both events were watched arriving while a run was written.

`ChangeWatcher` owns no clock — `poll` is called by whoever does — which is what let every rule it
has be tested without waiting for a real second: that it says nothing while unchanged, that a
listener joining is told nothing until something happens, that one dead socket does not silence the
other four, and that a store which breaks mid-watch is reported rather than read as "nothing is
happening".

### 2026-08-27 — the interface, rebuilt to be understood

On Samreen's instruction: Tailwind, shadcn/ui and Chart.js, and a dashboard that explains itself.
The rebuild follows a documented data-visualisation method rather than taste, and the parts of it
that became rules in the code are these:

- **The form is picked by the job the data does.** Part-to-whole is a stacked bar (how every attempt
  at each specification ended); magnitude is a single-hue bar (where an iteration spends its time);
  a trend is a line (the last twenty runs); one number is a stat tile, never a one-bar chart; a
  ratio against a limit is a meter (39 of 50 complete runs).
- **Colour is assigned by job, in a fixed order, and never cycled.** `seriesColour` throws past the
  last slot rather than wrapping, because wrapping is how a chart ends up drawing two different
  things in the same colour while looking perfectly fine.
- **Nothing says anything by colour alone.** Every series has a legend entry in HTML — not on the
  canvas, where a screen reader cannot reach it — and every status carries its word.
- **The rate axis is fixed at 0-100%, never fitted.** An axis that rescales itself turns a wobble
  between 98% and 100% into a mountain range.
- **A panel's explanation is a required parameter.** A chart with a title and no sentence saying
  what it is claiming gets misread, and the cheapest place to prevent that is where the panel is
  declared.

The one genuinely new view is **Overview**, because the roadmap's "metrics and charts" was doing two
jobs: *how is it going* at a glance, and *what did each specification cost*. Those want different
forms, and on one page neither was readable.

### 2026-08-27 — five defects that only running it could find

Compiling was clean, both suites were green, and `vite build` was happy. None of that saw any of
these.

1. **Charts drew nothing in a screenshot.** Chart.js animates by default and a resize restarts the
   animation, so every capture caught the bars at width zero. Animation is off now — and that is
   right independently: these charts redraw whenever the live stream fires, and a bar that regrows
   from zero every second turns a dashboard into a fidget.
2. **The dark theme drew every chart in the light palette.** Charts read their colours out of the
   document with `getComputedStyle`, and the theme class was applied in an effect — which runs
   *after* the render that read them. Dark grey labels on a near-black surface, invisible, and a
   grid meant to recede at 12% opacity glaring at full strength. The class is now applied before the
   first render and synchronously inside the toggle. Everything else on the page looked fine, which
   is exactly why it took a screenshot to see.
3. **The trend chart was a flat line at 100% and said nothing.** True, and useless alone. It now
   carries a second line, the pass rate, which is the one that varies — and the flatness of the
   first became the point rather than an empty chart. That needed a number the API did not have, so
   `/api/runs` now counts passes per run: derived in the browser it could only have been a coarse
   0 or 1.
4. **The audit view stretched the page to eight thousand pixels.** Two hundred rows, and the filters
   scrolled off the top the moment you started reading. The log scrolls inside its own box now.
5. **An occupied port arrived as a stack trace.** A copy of the API left running from an earlier
   session made the next one die with `EADDRINUSE` buried in an unhandled event. It says so in a
   sentence now, and names the fix.

A sixth thing was not in the code. The test that watched the live stream wrote a real run into the
real measurement store; it was removed the same minute. A fabricated run in a store whose whole
point is honest measurement would have made the gate's count of complete runs a lie.

### The front end

`dashboard/`, React and TypeScript with Vite, written by hand rather than scaffolded — the template
would have arrived with a logo, a counter and a README to delete. It is 205 kB of JavaScript and one
stylesheet, and its only dependency beyond React is Vite itself. The bar chart is inline SVG: five
bars do not justify a charting library, and the whole claim of phase 3 is that these numbers were
measured, so the arithmetic that draws them should be readable.

Four views — Runs, Metrics, Audit trail, Workshop gate — plus the permanent mode banner. The
copilot's chat is **not** among them: it needs the live loop rather than recorded data, and the
roadmap's own list of what gets cut first puts it first. The data views are the ones that cannot be
faked, and they are the ones that exist.

Three things in it are rules rather than styling, and each has a test:

- **A failed read is its own state.** Every panel goes through `WhenLoaded`, which shows the reason
  the API gave, verbatim. A view that could not tell "the API is down" from "nothing is recorded"
  would show an empty audit table for both, and one of those is a lie about whether anything was
  changed.
- **No bare percentage, and no mean without its sample size.** `format.ts` is a module of pure
  functions for exactly this reason: a rule that lives inside a component is checked by looking at a
  browser, and this one is checked by `npm test`. `9 of 11 (82%)`, `1.0 over 11`, and
  `none attempted` where zero of zero would otherwise print as 0%.
- **The types come from `harness/src/`, not from a copy.** The dashboard imports the response
  contracts the API exports. A front end with its own description of the same payload keeps
  compiling long after the API stopped sending it, and then renders `undefined` into a number
  somebody is about to decide something on.

### What running it found that reading it had not

The rule of this repository, earned five times over: execute before believing. Three defects, none
of which any test would have caught, because all three are about what the thing looks like when it
meets real data.

1. **The audit view rendered the entire trail — 2291 rows, no limit.** The API now takes `limit`,
   defaults to 200, sends the *most recent* of what matched, and reports how many matched so a
   truncated answer can never read as a complete one. A limit that is not a count is refused rather
   than falling back to the default: `?limit=all` quietly becoming 200 would answer a request for
   everything with a fifth of it.
2. **The chart shouted.** The SVG was scaled to the width of the window, which scales its text too,
   so the phase labels came out at twice the size of the table above them. It now draws at its own
   size and is allowed to shrink, never to stretch.
3. **The audit timestamps wrapped onto two lines**, being raw ISO 8601 from the C# side. They are
   formatted local now — and a timestamp that cannot be parsed is shown exactly as recorded rather
   than as "Invalid Date", because an audit line rendered as an error message is a line whose
   evidence was destroyed by the thing displaying it.

A fourth thing was not a defect but a gap: the views had no address. They are on the fragment now,
`#/workshop-gate`, which makes a view something you can send somebody a link to — and, not
incidentally, something that can be checked without a browser to click in. All four were verified by
rendering them headless against the real store.

### Next in phase 4

The plant copilot's chat, which is the one view the roadmap names and this does not have — and which
is also cut 1 on the roadmap's own list of what goes first. It needs the live loop rather than
recorded data.

Before that: the work of two sessions is still uncommitted on `main`, and needs a branch and a pull
request.

## Phase 3 — the harness (started 2026-08-21)

`harness/`, Node and TypeScript, outside the solution as the roadmap says. Started with the client
and the loop rather than the generator, on the user's decision: a generator wired in on day one
means a failure could be the loop, the MCP server or the prompt, and separating those afterwards
costs more than building them in order.

### 2026-08-26 — what phase 3 still owed, and the four loose ends

Everything the roadmap named for this phase now exists except the WebSocket telemetry, which was
deferred on the user's decision: its only consumer is the phase 4 dashboard, and an event stream
nobody reads is exactly the untested code this repository has been bitten by three times. SQLite
already holds everything that stream will carry.

**`--repeat <n>`, and a report that carries its sample size.** One run of a set answers "did it
pass"; a rate needs repetitions. Each repetition is its own run in the store, opens a project
nothing has written to, and the report ends with `n=12 (6 repetitions of 2 specifications)` rather
than a percentage. Connecting to TIA Portal and setting the network mode stay outside the loop:
they cost a minute and would otherwise be measured instead of the work.

**The workshop gate exists and answers no.** `harness/src/gate.ts` evaluates the five criteria the
roadmap fixed "in the cold", `npm run gate` prints them with the numbers behind each. It reads the
metrics store, the audit trail and the backups on disk, and touches neither TIA Portal nor a
controller — anyone can check the claim rather than take it. Two decisions inside it are judgements
and are written down as constants rather than buried: what "stable" means arithmetically (a fall of
no more than ten points between the two halves of the twenty-run window), and that an iteration
outcome the harness does not recognise counts as unknown rather than as probably fine.

Criterion 5 cannot be answered by data — a person has to have reviewed the design in the room — so
it is read from a file that does not exist yet, and the gate therefore says no. That is the correct
answer, arrived at without anything failing.

**Six specifications, up from two**, which is inside the 5–10 the roadmap asks for. The four new
ones assert things the pattern actually does: the four-station cell, two pieces in order, manual
mode not running the line, and no piece admitted without Enable. The last two needed a fourth
acceptance action, **`hold`**: `expect` reads one instant, and "no piece completed" checked a
millisecond after the cell started passes whether or not it was about to complete one.

**`generator.ts` against the Anthropic API.** `createModelGenerator` takes a sender, so the prompt,
the answer and the rules for turning one into the other are tested without a network; `createApiSender`
is the half that calls the Messages API, streamed because a request this size is minutes of
generation. The compiler's errors go into the next attempt unedited — summarising them would throw
away the line numbers, which are the part that makes a fix possible. `--generator model` selects it,
and it has not yet been run against the real API: there is no key on this machine.

### Deferred on purpose: the model generator is written and unexecuted

`--generator model` has never run against the real API, and that is a decision of the user's taken
on 2026-08-26, not an unfinished task. The Anthropic API is billed separately from a Claude
subscription, so measuring it means opening a billing account, and the roadmap's own cut list
already names this as the third thing to drop if something has to go: *"the harness uses
pre-generated specifications instead of calling the model."*

**The pattern expander is the main path, and not as a consolation.** It is deterministic — the same
cell specification produces the same SCL, every time, with the same diff — and it has 48 passing
specification runs behind it. A model is non-deterministic by construction, which is the right
property for drafting and the wrong one for the program that will eventually command a station.

What is owed when somebody picks this up: nothing but a key and a command. The generator, its prompt
building, its extraction rules and its error propagation are written and covered by tests against a
double; `createApiSender` is the only part that has never executed. Roughly $0.20 per attempt at
current Opus 5 pricing, so one specification at `--repeat 1` answers "what does the SCL look like"
for about twenty cents before anybody commits to a batch.

It is worth doing when there is a reason — the phase 5 pitch wants the number, or the supervising
teacher asks for it, or the school has API access of its own. It is not worth doing to tick a box:
it answers "can a model close the loop unaided", which is a different question from "does this cell
run", and only the second one is on the way to a machine.

### The first rate, with its sample size

Three repetitions of the six specifications, unattended, on the pinned server:

```
18 of 18 specification run(s) passed on a simulated CPU, n=18 (3 repetitions of 6 specifications).
30 of 30 specification run(s) passed on a simulated CPU, n=30 (5 repetitions of 6 specifications).
```

**48 specification runs on 2026-08-26, all of them passing**, across two unattended batches. The
store now holds **39 complete runs and zero iterations without an outcome**, which is criterion 2
of the workshop gate met by construction rather than by tidying.

Every specification passed all three times. `two-station-recovers-from-a-broken-first-attempt`
averages exactly 2.0 iterations, which is the number it should have: it breaks its first attempt on
purpose and recovers on the second, every time.

Time per phase over the five-repetition batch, n as shown:

| phase | n | mean | share of the cost |
|---|---|---|---|
| download | 30 | 12.2 s | 65.0% |
| write | 35 | 1.9 s | 12.1% |
| verify | 30 | 2.9 s | 15.3% |
| compile | 35 | 1.2 s | 7.6% |
| generate | 35 | ~0 s | 0.0% |

**This supersedes the earlier figure of "46 s and 91% of the cost".** The download is still the
expensive phase and the loop's order still exploits it — an attempt that does not compile never
downloads — but it is three times cheaper than it was on 2026-08-21, and a stale number is worse
than none. `generate` is ~0 because the pattern expander is not a model; it is the one row that will
change completely when `--generator model` runs.

**The gate answers no, on two criteria and for good reasons.** 39 complete runs of the 50 it asks
for, and no in-person review recorded. The other three are met: zero silent failures, 113 recorded
backups all present on disk, and a clean-compilation rate of 100% against 100% across the two halves
of the twenty-run window.

### One download per session, which closes the last loose end

Chasing why the four-station specification failed on its second attempt produced the rule the whole
day had been circling. Measured, four downloads in a row against one open project:

| download | controller | result |
|---|---|---|
| first | freshly created | succeeds |
| second | same one, in RUN | `Connect to module failed` |
| third | same one, stopped | `Connect to module failed` — so it was never the RUN |
| fourth | deleted and recreated | fails later, in the text libraries, `InvalidVersion` |

**Within one open project, only the first download to an address succeeds**, and recreating the
controller alone does not undo it. What does is what a repetition already did: reopen the project
from the archive *and* create the controller fresh. `LoopOptions.resetSession` now does exactly that
between attempts, and only after an attempt that reached the download — a compile failure costs the
controller nothing, and resetting after one would spend a retrieval per compiler error.

It also explains **the controller found in RUN before a download**, which had survived two
eliminations: the attempt before it had downloaded and started the controller. Nothing was drifting
into RUN; the loop had put it there and then tried to download again. That loose end is closed by
cause rather than by elimination.

### The four loose ends, and what happened to each

1. **Repeatability** — the instrument now exists; the measurement is the next thing to run.
2. **The controller in RUN before a download** — closed, and by cause: a previous attempt had
   downloaded and started it. Two other explanations were eliminated by measurement first — a
   controller left alone stays in `Stop` for ninety seconds, and one created on inherited storage
   is created in `Stop` and stays there. See "One download per session" above.
3. **The PLCSIM runtime reporting itself unavailable** — happened once, cleared on retry, and left
   **no trace in the Windows event log**: the Siemens entries that day are the boot at 18:20 and
   nothing else. So it was not a process crash. No mitigation was added: a retry around a symptom
   nobody can reproduce would hide the next one.
4. **The whitelist dialog on every rebuild** — unchanged, and it is TIA Portal's behaviour rather
   than ours. The working practice is the pinned copy in `.tia-mcp/harness/server/` and
   `TIA_MCP_SERVER` pointing at it; rebuild it deliberately, accept once, and an unattended batch
   then runs without anybody at the screen.

**Done: the harness talks to the server, and records what happens.** `npm run check` — type-check
plus **9 tests, 9/9**, no TIA Portal needed.

```
harness/src/mcpClient.ts        owns the process, the transport, and the result shape
harness/src/serverLocation.ts   finds TiaMcpServer.exe; TIA_MCP_SERVER overrides
harness/src/telemetry.ts        runs, iterations and phase timings, in SQLite
harness/test/*.test.ts          9 cases
```

Three decisions in that, each measured rather than assumed:

- **No build step.** Node runs the TypeScript by stripping types, so `tsconfig.json` type-checks and
  emits nothing. The cost is the features that need a real transform — no enums, no namespaces, no
  decorators, no parameter properties — and `erasableSyntaxOnly` makes the compiler enforce it
  rather than leaving it to memory. What it buys is that there is no `dist/` to be stale.
- **A refusal is not a failed call, and the harness had to learn the difference the hard way.** The
  first version asserted it with `WriteScl`, which throws when no project is open — that is an
  invalid state, not a policy decision, and it arrives as `isError`. A governance refusal comes back
  as an ordinary response with `meta.success` false and `meta.outcome` `Refused`. Both are now
  tests, one per side, and the refusal one also asserts that nothing was created.
- **The server's stderr is empty unless logging is on.** Measured. The transport pipes it anyway,
  because that is where diagnostics appear when there are any, but the comment says what an empty
  log means: nothing was logged, never nothing went wrong.

The test asserts the toolset is large rather than exactly 55. A count would fail for the one reason
that is never a defect.

### The telemetry store

`node:sqlite`, which ships with Node, chosen over `better-sqlite3` for one reason: a native module
has to be compiled, and a harness that needs a C++ toolchain before it can record a number is a
harness that does not get run on the machine that has TIA Portal on it.

Three things in it are deliberate:

- **Every timestamp is epoch milliseconds.** These get subtracted, sorted and compared across runs,
  and a local-time string does none of those correctly twice a year.
- **A phase is timed in a `finally`, so a phase that threw is still timed.** "The compile took
  ninety seconds and then failed" is a different problem from "the compile failed at once", and only
  one of them is visible if a failed phase records nothing.
- **Opening a store written by another schema version is refused.** The alternative is not a crash,
  it is a number computed from columns that used to mean something else.

**A defect the tests found, in the harness's own code:** when `Telemetry.open` refused a schema
version it threw without closing the database, so on Windows the file stayed locked and the next
thing to touch it failed with a permission error that said nothing about the schema. The test for
the refusal could not delete its own temporary directory, which is how it surfaced. Same shape as
the orphan TIA portal and the unheld PLCSIM handle: a handle not released on the failure path.

`SUM` over no rows is NULL in SQL, so a run with no iterations is read as zeroes rather than passed
on to become a NaN in a report. That has its own test, because a crashed run is exactly the case
that produces it.

**Not built yet, and deliberately:** the `audit` table the roadmap names. Creating it now would add
a table nothing writes to; it wants an ingester for `.tia-mcp/audit.jsonl`, which is its own piece.

### The loop closes, end to end (2026-08-21)

**2 of 2 specifications passed on a simulated CPU, n=2, 3 iterations, 2 m 21 s.** The chain is
generate, write, compile, download, start, and check the cell behaves — the whole thing, on the
user's decision. One of the two cases is deliberately broken on its first attempt and recovers on
its second, so the corrective half of the loop is exercised and not merely present.

First real numbers, which is what this phase exists to produce:

| phase | n | mean |
|---|---|---|
| download | 2 | **46.1 s** |
| write | 3 | 5.1 s |
| compile | 3 | 2.3 s |
| verify | 2 | 0.8 s |
| generate | 3 | 0.013 s |

**The download is 91% of an iteration.** Compiling is 2.3 seconds and downloading is 46, which is
worth knowing before anyone designs a bigger measurement — and the loop's ordering already exploits
it: `download n=2` for three iterations, because the attempt that did not compile never reached the
download and saved the whole 46 seconds.

```
harness/src/specification.ts   the cases, and their acceptance language
harness/src/generator.ts       the stub, which proves nothing about generation and is not meant to
harness/src/verification.ts    write, waitFor and expect against a running controller
harness/src/loop.ts            which failures continue and which stop
harness/src/toolContract.ts    the harness's calls, checked against the server's schemas
harness/src/run.ts             the CLI
harness/policy.json            versioned, because it is part of the experiment
harness/specs/                 two cases
```

`npm run check` — type-check plus **18 tests, 18/18**, none of them needing TIA Portal.

#### It took ten runs, and not one failure was in the loop or in the cell

Worth listing, because the pattern is the argument for having built the harness before the
generator. Every one of these would have looked like "the model generates bad code":

1. **The MCP SDK times out requests after 60 s.** Connecting to TIA alone takes 45. A timeout
   shorter than the work turns every measurement into a measurement of the timeout.
2. **Openness refuses relative paths** — "The argument 'sourcePath' cannot be a relative path". The
   CLI now resolves every path itself; a CLI that only works with absolute paths is a trap.
3. **A failing tool arrives in two shapes**, and the client handled one: a result with `isError`, or
   a JSON-RPC error the SDK raises as an exception. `GetProject` took the second path and killed the
   run outright instead of recording an outcome and carrying on.
4. **`--logging` selects a destination, not a level.** 1 is stderr; 0, which is what a flag called
   "logging" invites, turns it off — which is why the log was empty when it was needed.
5. **Two parameter names were guessed wrong**: `scl` where `WriteScl` takes `sclCode`, and
   `projectPath` where `OpenProject` takes `path`. See "The contract" below.
6. **A block written into a project stays in it.** The stub broke the first attempt by *appending* a
   block that could not compile, so the next attempt omitting it repaired nothing: the project kept
   two errors for every remaining attempt, and for the next specification too. It now breaks the
   `Main` OB, which every attempt regenerates. Regenerating a block overwrites it; not mentioning one
   does not delete it.
7. **The report said "2 compiler error(s)" and not which.** A count says something is wrong and
   nothing about what, which is exactly what this repository's error model exists to prevent.
8. **Four gaps in the server**, below.
9. **`ResetModule`**, below.
10. **A dialog, twice.** See "The whitelist" below.

#### The contract

`toolContract.ts` lists every tool the harness calls with the arguments it sends, and checks that
against the server's own schemas in **74 ms without TIA Portal**. It exists because finding one wrong
parameter name cost a full download-length run, the server's logging turned on, and a stack trace:
the protocol carries no detail, so `WriteScl` failing arrives as "An error occurred invoking
'WriteScl'".

It checks three things, and the third matters most later: every **required** parameter of a tool is
one the harness sends. A parameter that becomes required would otherwise break the loop mid-run with
no explanation. Its test also asserts that the check can fail, because a contract check that always
passes is worse than none.

#### Four gaps in the server, all found by trying to close the loop

None was visible by reading. **58 tools**, up from 55.

- **`CompileHardware` and `EnableSimulationSupport` existed in the `Portal` layer and were not
  exposed.** They are the two steps `Test20CellRuns` does internally, and without them a download
  fails blaming the target rather than the project. Both guarded.
- **`UseTcpIpNetworkMode` was called by nobody** — only by the C# tests, in-process. Without it the
  runtime stays on Softbus, where a controller is reachable only by PLCSIM itself. Nor can it be
  fixed from outside: the code records, measured, that setting it from another process reads back as
  applied and has no effect. So it is a tool now, and guarded.
- **`CreateSimulationInstance` could not be given a CPU type.** It created the unspecified
  controller, the hardware download succeeded, and the text libraries then failed with `InvalidAID`.
  `Test11Download`'s own comment had recorded why it creates a `CPU1511`; without that comment this
  would have cost hours.

That last one forced a governance decision: **a fourth family of change targets**,
`simulation-runtime`. The network mode is machine-wide and affects every PLCSIM user on the computer,
while a controller affects only itself, so a policy that permits creating controllers must not
thereby permit reconfiguring the runtime they all live in. `simulation/*` deliberately does not cover
it.

#### ResetModule, the seventh answer

The download asked `ResetModule` for the first time, because the controller is now created as the
project's CPU rather than the unspecified one — the previous fix opened this door. Answered
`DeleteAll`: a virtual controller starts empty so there is nothing to lose, and `NoAction` leaves
whatever is on the module beside what is being written. Against hardware that answer would erase a
machine's program, which is one more reason downloading there is not implemented.

The diagnostic is why this cost one line instead of hours. It did not say the download failed; it
said *"The download asked something this server cannot answer: ResetModule. Add it to the answer
table in SimulationDownloader."* That sentence was written by the work of 2026-08-17, and it paid for
itself today.

#### The whitelist, and why unattended measurement is not solved

**TIA Portal asks for Openness confirmation again every time the server executable is rebuilt**, and
while that dialog is open `Connect` blocks. Measured: run 7 hung right after a rebuild, run 8
connected with no dialog and no rebuild, run 9 hung again after a rebuild. It cost twenty minutes
across two runs, and the visible symptom was `Request timed out` — which points at the server when
the cause is on the screen.

This is a constraint on the phase, not an anecdote. The roadmap wants `n=10, 3 repetitions`, and a
loop that hangs for ten minutes whenever the server is rebuilt cannot run unattended. The way out is
to pin `TIA_MCP_SERVER` to an already-confirmed copy of the executable and not rebuild it between
measurements.

`Connect` should also get a timeout of its own, shorter than the ten-minute default: it takes a known
forty-five seconds, so waiting ten minutes protects nothing and only delays the news.

#### RESOLVED 2026-08-26: the download, and the state a name inherits

**The cause was in this repository, not in the environment.** A PLCSIM Advanced controller keeps a
storage directory named after the instance, and `UnregisterInstance` leaves it behind. The next
controller created with that name inherits it, and its first download fails with `Connect to module
failed`. `SimulationRuntime.DeleteInstance` now calls `IInstance.CleanupStoragePath()` before
unregistering, and the failed-creation rollback does the same.

Proved twice, in both directions, before anything was changed:

- A probe that downloaded successfully was made to fail by **changing nothing but the instance
  name** to the harness's `HarnessTwoStation`. `HarnessTwoStationX`, one character apart and never
  used before, downloaded fine.
- The harness passed 2 of 2 with **no code change at all**, by moving the two stale directories
  aside.

Why no test caught it: every instance name in the suite carries a GUID, so no run had ever reused
one. `Test10Simulation.DeleteInstance_CreatedInstance_LeavesNoStorageBehind` now asserts the rule,
and would have failed before the fix. The harness takes its controller name from the specification,
which is what made it the first thing to reuse a name — and therefore the first to be poisoned.

Two things were built while chasing it and both stay:

- **`DescribeSimulationConnection` is an MCP tool.** The diagnostic existed in the portal layer and
  only the test suite could reach it, which is why six harness runs reported one sentence with no
  state behind it. The harness now calls it whenever a download fails, and never otherwise.
- **The harness runs the server with logging on** and writes `server.log` beside its other output.

What was ruled out along the way, each by measurement, and all of it wrong: a degraded environment
(the machine was restarted and failed identically), the controller being in RUN before the download
(stopping it first changed nothing), the network (the controller answered ping and ARP throughout),
the MCP path itself, and the generated program. Five probes reproducing the harness step by step all
downloaded successfully; what finally separated them was the one argument nobody had varied.

The record of the search is below, kept because what it eliminated is worth as much as the answer.

#### The search, as it stood before the cause was found

The loop passed 2 of 2 on run 10.
Runs 11 to 15 all failed at the download with `Connect to module PLC_0 failed` — the single symptom
this project has spent the most time on, and whose known cause (a controller unregistering itself
because no handle was held) was fixed on 2026-08-17.

What has been ruled out, by measurement rather than by reasoning:

- **Not the network mode.** The suspicion was that `UseTcpIpNetworkMode` reported success while the
  runtime stayed on Softbus. It now fails closed in both layers — see below — and the run reached
  the download anyway, so the mode was TCP/IP.
- **Not a stale controller.** `ListSimulationInstances` reports none before a run.
- **Not the virtual adapter.** "Siemens PLCSIM Virtual Ethernet Adapter" is Up at 192.168.0.100,
  the same subnet as the controller at 192.168.0.1.
- **Not an orphan process.** No `Siemens.Automation.Portal` and no `TiaMcpServer` afterwards.
- **Not the pinned executable.** Runs 11 and 12 used the ordinary build and failed the same way.

What changed between run 10 and run 11, in order of how much they are worth trying:

1. **The machine had started TIA Portal about fourteen times by then.** This repository already
   records that the environment degrades; a restart of the PLCSIM Advanced runtime, or of the
   machine, is the cheapest thing to try and was not tried because of the hour.
2. **The server's audit trail and backup root moved.** Run 10 let them default beside the harness's
   working directory; they are now named explicitly under `.tia-mcp/harness/`. Nothing about that
   should touch a download — and "should" is what this session disproved five times, so reverting
   to run 10's conditions is a real experiment.
3. **`Describe` was extracted** in `McpServerWrites.cs`, which touches how a download's *response*
   is built and not the download. The 139-case suite passed after it.

Two things were fixed while chasing this, and both are worth keeping whatever the cause turns out
to be:

- **`UseTcpIpNetworkMode` reported success for having been called**, not for the mode it left behind.
  A runtime still on Softbus then let a caller spend a compile and a download before failing with
  `Connect to module failed`. It now refuses when the mode is not TCP/IP, in the tool and again in
  the harness.
- **`Connect` has its own three-minute timeout**, against ten for everything else. A slow Connect is
  almost never slow: it is the Openness confirmation dialog waiting for somebody. The message says
  so now.

**Two harness runs at once do not work.** Openness attaches to the TIA Portal already running, finds
the other run's project open, and `RetrieveProject` fails with "Another project is already open".
Found by accident, worth knowing before anyone tries to parallelise measurements.

**Next in this phase:** `generator.ts` against the Anthropic API. It is the only piece missing, and
the one whose failures can finally be attributed to it, because everything around it has been
measured. The download included, as of 2026-08-26.

## Phase 2 — FB_Station and multi-station (2026-08-18)

The coursework content, standing on phase 1. Everything cell-specific is data in `spec/`; nothing
in `src/` knows what a station does or how many there are.

```
spec/patterns/station.scl.tmpl       FB_Station, one block, one instance per station
spec/patterns/coordinator.scl.tmpl   FB_<Cell>, owns the instances and moves the pieces
spec/cells/two-station-demo.json
spec/cells/four-station-cell.json
src/TiaMcpServer/Spec/                CellSpecification, its loader, SclTemplateExpander
```

**Both cells write into a real project and compile with 0 errors in TIA Portal V20.** That is the
only claim worth making about generated PLC code, and `Test19CellPattern` is what makes it: SCL
that looks right and does not compile is the normal outcome of writing it from memory. It goes
through `WriteScl` and `CompileSoftware`, so phase 2 is the first thing that actually needed the
loop phase 1 built.

### The handshake, which is the whole of the coordination

```
Ready   "I am empty and able to take a piece."
        The coordinator writes PieceId and raises Start. Start high means "this piece is yours".
Busy    "working on it."
Done    "finished, and the piece is still mine until you take it."
        The coordinator drops Start once it has moved the piece. Only then does the station idle.
```

Two decisions in that are the difference between a coordinator and a diagram:

- **A station holds its piece until Start drops.** Returning to idle the moment it finished would
  release a piece nobody had accepted, and a piece in no station is exactly the state a
  traceability number cannot describe.
- **If station N+1 has faulted, station N keeps its piece** and the cell reports `BlockedAtStation`.
  The roadmap names this as the question that decides whether the coordination is real. Releasing
  anyway would put a piece into a station that cannot work it.

Handovers run **from the far end backwards**, so the space the last station frees is available to
its neighbour in the same scan and a piece advances one station per scan. Forwards would move a
piece into a station not yet emptied.

### The interface is fixed, and what that forced

`IN Start, Reset, Enable, ModeAuto, ModeManual`; `OUT Busy, Done, Error, ErrorId, Ready`;
`IN_OUT PieceId`. Nothing was added to it.

That ruled out the obvious way to give each station its own step count. There is one `FB_Station`
and one instance per station, so a block constant cannot differ between them, and an input would
change the interface. `WorkStepCount` and `DwellCycles` are therefore **statics**, which are per
instance, written once by the coordinator on its first scan. Their defaults are zero and an
unconfigured instance faults with `ERR_NOT_CONFIGURED` rather than running a sequence of no steps
and reporting Done without having done anything.

The steps themselves do nothing, and that is the honest shape for generated code: what a station
physically does is cylinders, sensors and interlocks, and none of it can be inferred from a JSON
file. The steps are where that work goes, and the handshake around it already works without it.

### The template language is deliberately not a language

Two constructs: `{{name}}`, and `{{#stations}}` / `{{#handovers}}` regions. No conditionals, no
expressions.

The one thing a coordinator template would want a conditional for is "the last station hands over
to nobody", and `CellSpecification.Handovers()` answers that in C# by returning a list one shorter.
A template language would need its own parser, its own error messages and its own tests, and a
person debugging generated PLC code would then have two languages to hold in their head.

Three details that are load-bearing rather than tidy:

- **An unreplaced placeholder is an error.** Left alone it reaches the SCL compiler as
  `{{stationNmae}}` and comes back as a syntax error in generated code, which is the least useful
  place to be told about a typo. All of them are reported at once, so three typos take one round
  trip.
- **A region tag alone on its line takes the whole line with it.** Otherwise every region leaves a
  blank line behind, and generated code nobody wants to read is generated code nobody checks.
- **Names are validated as SCL identifiers** when the specification is built. "Drill 1" would
  otherwise generate a block that does not compile, and the compiler would blame the generated
  code rather than the JSON. Accented letters are accepted: TIA allows them, and rejecting them
  would be inventing a rule the platform does not have.

### ExpandCellScl, and why it writes nothing

One new tool, and it is a read: it returns the SCL and touches no project. Writing it is `WriteScl`,
which is guarded, audited and takes a backup. So phase 2 added no new way to change anything, and
the agent can look at the generated code before any of it lands.

It also takes **no Openness gate**, deliberately: it reads two text files and does string work, so
queueing it behind a running compile would cost something and buy nothing.

### A third test project

`tests/TiaMcpServer.Spec.Test/`, 35 cases, no TIA Portal. The reason is the one that created the
second project: cell specifications and template expansion are text in and text out, and making
them wait for a portal would mean nobody could check the patterns on a laptop.

Not in `TiaMcpServer.Governance.Test` because that project holds one explicit test per safety rule
and its value comes from being exactly that list.

What that project cannot cover is whether the SCL compiles. `Test19CellPattern` does, in the TIA
suite. The split is worth stating: 35 tests say the text came out as intended, and 2 say the
patterns are correct.

### The cell runs (2026-08-21)

The item below that said "nothing has been downloaded and watched running" is closed. The
two-station cell is downloaded to PLCSIM Advanced, the CPU is in RUN, and **a numbered piece goes
through both stations** — asserted, not watched by hand. `Test20CellRuns`, 3/3.

What it took was tag access, which the server did not have: reading and writing a controller's tags
is how a downloaded program is observed, and note 14 of 2026-08-13 had correctly measured that it
was not independent of the download. The download has worked since 2026-08-17, so it was possible
now.

- `ListSimulationTags`, `ReadSimulationTags`, `WriteSimulationTag`. **55 tools**, up from 52.
- `ExpandCellScl` takes `includeEntryPoint`: the instance data block and a `Main` OB that calls it
  every scan. Off by default, because it replaces the project's `Main`.
- `spec/patterns/main.scl.tmpl`, the third pattern.

**The OB calls the instance with no parameters, and that is the whole design of that file.** Passing
constants would assign the inputs every scan, and a tag write would be overwritten before the next
call — the cell would be undrivable from outside, which is to say untestable.

Two things could not be known by reading and were settled by running:

1. **TIA accepts an `ORGANIZATION_BLOCK "Main"` generated from an SCL source**, and it replaces the
   project's own.
2. **PLCSIM exposes the whole instance data block, nested instances included**, as
   `DB_TwoStationDemo.Feeder.Step` — no quotes in the name, whatever SCL requires when writing it.

#### The mode may only be changed with the cell empty

Found by a test that tried the obvious thing: hold the piece in manual mode, then switch to
automatic and watch it finish. The second station faulted every time, with the piece in it.

`ModeAuto` and `ModeManual` are two bits, and two tag writes cannot land in the same scan. Between
them both hold the same value, and `FB_Station` treats `ModeAuto = ModeManual` as a wiring fault —
which is right, because two modes at once is one. Dropping `CellStart` first does not help: that
gates admission at the first station, and a handover to the second is not gated at all.

So this is not a test detail, it is a property of the pattern: **a mode change belongs to a stopped
cell, not to a running one.** An operator turning an Auto/Manual selector with a piece on the line
faults the station that piece is entering.

Three ways out, and the choice is the user's:

1. **Leave it and write it down** — what is done now. Does not touch the interface, which this
   document declares fixed.
2. **One `Mode : Int`** instead of two bits: one write, no intermediate state, impossible by
   construction. Changes `FB_Station`'s interface.
3. **Tolerate the ambiguity transiently**, keeping the last valid mode. Rejected: it would hide a
   genuinely miswired machine, which is what the check exists to catch.

#### The build without PLCSIM Advanced does not work, and the project claims it does

Measured on 2026-08-21 by building with `PlcSimApiPath` pointed at nothing. The comment in
`TiaMcpServer.csproj` says a machine without PLCSIM Advanced "still builds — the simulation tools
then report that the runtime is unavailable instead of failing to compile", and
`SimulationRuntime`'s remarks say the same. **Neither is true**, and it predates the tag work.

Two layers of it:

1. `StartInstance`, `StopInstance` and `PowerCycleInstance` delegate to `WithInstance`, whose
   non-PLCSIM overload takes `Action<object>`. The lambdas then call `.Run()`, `.Stop()` and
   `.OperatingState` on `object`, which does not compile.
2. Guarding those three is not enough. With the API absent, nearly every private member of
   `SimulationRuntime` becomes unreachable — `_logger`, `Execute`, `IsMissingRuntime`,
   `TransitionTimeoutMilliseconds` — and this repository runs the analysers as errors, so the
   build fails on IDE0051, IDE0052 and CA1823 instead.

So the fix is not a few `#if`s. It is either guarding every private member of that class, which
makes it unreadable, or turning specific analysers down for the configuration where the API is
absent, which is a build-level decision. **Attempted and reverted on 2026-08-21** rather than
smuggled into a feature commit; `SimulationRuntime.cs` is back to a one-line diff.

Worth doing, because the promise is load-bearing: it is what lets somebody work on the cell pattern
or the governance layer on a laptop with no PLCSIM licence.

### Still open in phase 2

- **The work steps are empty.** By design, but the coursework needs real actuation in them, and
  that is hardware-specific work rather than pattern work.
- **The mode selector question above** is a decision waiting, not a defect.
- **The no-PLCSIM build**, above. A real defect, scoped and not started.
- **MQTT**, the remaining piece of phase 8, is generated code rather than configuration and belongs
  in `spec/` the same way these do.
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
