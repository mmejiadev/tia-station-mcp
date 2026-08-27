# Knowledge layer — hardware documentation and pre-flight safety

> Work brief. Agreed on 2026-08-27. Companion to `ROADMAP.md`, which orders the phases, and to
> `STATUS.md`, which records where the work stopped.
>
> This is deliberately **not** numbered as a roadmap phase. It runs alongside the existing order and
> is cut from the bottom up if time runs short — every stage below delivers value on its own.

## What this adds, in one sentence

The agent gains two things it does not have today: **the ability to look up what a given piece of
hardware actually requires**, and **a pre-flight review that has to be acknowledged before anything
is written or run**.

## Non-goals, stated first

These are constraints, not omissions. An implementation that quietly relaxes one of them has
misunderstood the brief.

- **Nothing here touches physical hardware.** Study Mode only, PLCSIM Advanced only. Workshop Mode
  remains unwritten and uncompiled, exactly as `ROADMAP.md` says.
- **No safety text is generated at run time.** See "The cardinal rule" below.
- **No vendor documentation is committed to this repository.** See "Corpus provenance".
- **This is not a conformance claim.** Nothing here asserts compliance with ISO 13849, IEC 62061,
  IEC 60204-1 or anything else. Standards are named as the *provenance* of a checklist item so a
  reader can go and check it, never as evidence that the system meets them.

## The cardinal rule — the system cites, it does not author

The project's thesis is in the README: *reliability does not come from the model, it comes from the
compiler and the tests*. A safety briefing written by a language model has no compiler. It is
ungrounded generation placed exactly where being wrong injures somebody.

Worse, a fluent and well-formatted briefing that is subtly wrong is **more** dangerous than none at
all, because it displaces the manual it paraphrases. The reader trusts the screen and does not open
the PDF.

So:

- Retrieval returns **verbatim excerpts** with source document, document version and page. Never
  model prose about safety.
- When nothing can be cited, the answer is **"not found, open the manual"**. The gap is never filled.
- The system never says "connect terminal X to terminal Y". It says "this robot's manual covers
  safety I/O wiring in section 7.2" and shows the paragraph.
- Fixed, unskippable footer on every safety surface: this does not replace the manufacturer's manual
  or the supervisor who is physically present.

This rule is **testable**, which is the point of stating it this way. A test asserts that a safety
response contains no sentence that is not a literal span of an indexed document. The compiler comes
back.

## Three tiers, and only one of them retrieves

| Tier | Source | Example |
|---|---|---|
| Universal — always, no exceptions | Static, authored, reviewed | Supply, protective earth, emergency stop |
| Equipment class | Static, selected by class | Articulated robot: enclosure and reduced speed. Pneumatics: residual pressure |
| Specific equipment | Retrieved, cited verbatim | UR5e manual section 7.2, safety I/O wiring |

Tiers 1 and 2 are repository constants under `spec/safety/`, versioned in Git and reviewed in a pull
request like any other file. **They carry a named author and a review date.** A safety checklist is
worth what the person who signed it is worth; an unsigned one is decoration.

Only tier 3 retrieves, and it only cites.

## Checklist design — the real hazard here is not hallucination

It is **normalisation of deviance**: a list that always looks identical gets clicked through unread,
and then manufactures confidence instead of safety. This is well documented in aviation and surgery,
and those fields study it precisely because checklists work *when designed against it*.

Three design decisions follow, and they are requirements:

- **At most ten items in the universal tier.** A long checklist is an ignored checklist. Pressure to
  add an eleventh item is pressure to rewrite, not to append.
- **Item-by-item acknowledgement. No "accept all" button.** This is layer 2 of the ROADMAP defence
  table — *no batching, no apply-all* — applied to safety rather than to writes.
- **The acknowledgement is audited**: who, which checklist version, when. Reuse `IAuditTrail`. An
  audited checklist is evidence; evidence is what makes people take it seriously.

And a fourth, about **timing**: the review appears bound to the transition it guards — immediately
before execution, at the same point where a `ChangePlan` is already confirmed — never at server
start-up, where it is furniture.

The universal tier ends with an explicit statement that it is the floor and not the ceiling.

## Architecture

The dependency rule in `CLAUDE.md` holds unchanged. The knowledge layer is a **sibling**, never a
dependency of the Openness adapter:

```
McpServer  ──►  Portal  ──►  Openness  ──►  Siemens.Engineering
    │
    └────────►  Knowledge   (retrieval, checklists — knows nothing about Openness)
```

`Knowledge` must not contain a `using Siemens.Engineering`, and `Siemens/` must not know the
knowledge layer exists.

**Build the ingestion and the index in `harness/`, in TypeScript.** .NET Framework 4.8 has a poor
ecosystem for PDF parsing and embeddings, and the harness already ships an HTTP API and a dashboard
that consume it. The C# server queries it as one more evidence source. This keeps the layer that
touches the controller free of new dependencies.

## Retrieval — no vector database

Do the arithmetic before reaching for infrastructure. Twenty devices, some 300 pages each, some 3
chunks per page is roughly 18 000 chunks. Brute-force cosine over 18 000 vectors is
**milliseconds**. A vector database contributes nothing at this scale and costs a service to deploy,
version and explain.

- Store embeddings in SQLite as blobs; scan linearly in memory.
- Keep the retrieval call behind one interface, so growing out of this is one class, not a migration.
- **Hybrid search, not embeddings alone.** Technical queries are exact references — `6ES7
  214-1AG40-0XB0`, `X20 connector`, `PROFINET`. Vector search is weak precisely there. BM25 plus
  embeddings is markedly better in this domain, and BM25 is a hundred lines.

Document this decision in the project memoir. Choosing *not* to use a vector database, with the
calculation shown, is a stronger engineering result than having used one.

## Corpus provenance and ingestion governance

*Let the agent go and read the documentation for a new robot* opens a channel for prompt injection
into a system that plans actions on machinery. A PDF can contain text a model reads as instructions.
This is the same class of hole the governance layer exists to close, entered through the back door.

The existing patterns apply unchanged, to documents instead of writes:

- **Deny by default, with a source whitelist**, mirroring `WritePolicy`. Known manufacturer domains,
  or a PDF the user supplies by hand.
- **Quarantine.** A newly ingested document is not searchable until approved. In Study Mode approval
  may be automatic *with notice*; on any path towards Workshop Mode, never.
- **Retrieved content is data, never instructions**, and is marked as such wherever it enters a
  prompt. No exceptions.
- **Ingestion is audited**: who ingested what, when, from which URL, with which hash.

The absence of a decision is a refusal, never a permission — the same rule as everywhere else.

**Copyright**: manuals from KUKA, ABB, FANUC, Universal Robots and Siemens are copyrighted. Do not
commit them. Version the ingestion *recipe* — URLs, hashes, document versions — and let each user
build the index locally. It also keeps the repository from growing by gigabytes.

## Enriching the change plan

`GuardedTool.Run` already produces a `ChangePlan` saying **what** will change. Add cited hardware
context saying **why it fits this equipment**: supported protocol, signal levels, I/O configuration.

This is the highest-value, lowest-risk part of the whole brief. It enriches an artefact that already
exists, already audits and already confirms. A student who reads *"writing this handshake; per the
UR5e manual section 4.3 the acknowledgement signal is active low"* learns far more than one who
watches SCL appear.

## How it is measured — this needs its own gate

This repository built a gate that tells its own author *no*. Adding a large unmeasured component
would put a hole in the middle of that thesis.

Build an evaluation set of roughly fifty questions with the correct page known in advance, and
report two figures:

- **Citation precision** — does the returned excerpt actually contain the answer?
- **Correct abstention rate** — when the answer is *not* in the corpus, does it say so?

The second matters more than the first. A retriever that never stays silent is dangerous; one that
stays silent well is trustworthy.

**Blocking criterion, in the spirit of the five already in `gate.ts`**: the safety surface is shown
to nobody until citation precision clears its threshold. Fix the threshold in code, in the cold,
before anyone wants the door open — as `RequiredCompleteRuns` was.

## Surfacing it — skills are the primary delivery

**These capabilities ship as Claude Code skills**, in `.claude/skills/`. The repository has none
today, and that is the gap this closes: a tool is only reached when a caller decides to call it,
whereas a skill carries its own trigger and reaches an agent working *on* the repository, not only a
user talking to the server.

Four skills, each named for what it does and each a directory holding a `SKILL.md`:

| Skill | Triggers when | What it does |
|---|---|---|
| `project-review` | The server has just been pointed at a project, or somebody asks what is in one | Reads the project and reports it: CPU, firmware, modules, existing blocks and tags, and what could not be checked against cited hardware. Writes nothing. |
| `preflight-safety` | Before anything is written to a project, compiled, downloaded or run | Renders the universal and class checklists, takes item-by-item acknowledgement, writes the audit entry. Refuses to continue unacknowledged. |
| `hardware-lookup` | A question names a device, a part number, a protocol or a connector | Hybrid search over the local index. Returns verbatim excerpts with document, version and page, or says nothing was found. |
| `hardware-ingest` | A device is named that the index does not hold | Whitelist check, fetch, chunk, embed, **quarantine**. Never makes a document searchable without approval. |
| `cell-review` | Before generating or expanding SCL for a cell | Reviews the cell specification against cited hardware context and reports what it could not verify. |

Rules that bind every one of them:

- A skill **never restates safety content in its own words**. It renders the static file or the
  cited excerpt. The cardinal rule is not relaxed for being inside a skill.
- `preflight-safety` and `hardware-ingest` **fail closed**. No acknowledgement, no continuation; no
  approval, no corpus entry.
- Skill descriptions state their trigger precisely enough to fire without being asked, and narrowly
  enough not to fire on everything.

**MCP prompts in `McpPrompts.cs` mirror them** for hosts that are not Claude Code. Both surfaces
render the same static checklists and call the same retrieval — **one body of content, never a
second copy**. A checklist that exists twice will diverge, and the divergent copy is the one that
gets shown.

## The commission, answered point by point

The brief above was written from a spoken request. This section puts that request back beside it, so
that where the two differ it is visible and deliberate rather than lost.

| What was asked for | Where it is |
|---|---|
| A review when the agent is pointed at a project | Stage 0, and the `project-review` skill. Added 2026-08-27; it was missing |
| A RAG in the project | Stage 1 |
| Documentation for the common robots | Stage 1, with the corpus built locally rather than shipped — see divergence 2 |
| That it feeds itself: a new robot gets read and remembered | Stage 4, through quarantine — see divergence 3 |
| A plan before the agent starts moving things in TIA Portal | Stages 2 and 2b. Added 2026-08-27; only half of it existed |
| A review of wiring, safety rules and protocol before running the machine | Stage 5 — cited, never authored, see divergence 1 |

### Three places this brief deliberately differs from what was asked

**1. It will not tell you how to connect the cables. It shows you the page of the manual that
does.** The request was for a run-through of how the cables should be connected. What this system
does instead is name the section and quote it: *"the UR5e manual covers safety I/O wiring in section
7.2"*, and the paragraph, verbatim, with its page. You get the same information in the
manufacturer's words rather than the model's.

This is the cardinal rule applied to the request that most tempts you to break it, and the reasoning
is in "The cardinal rule" above: a fluent, well-formatted briefing that is subtly wrong displaces the
manual it paraphrases, and generated text has no compiler at exactly the point where being wrong
injures somebody. **If this decision is ever revisited, it is revisited in a pull request with a
name on it, not by an implementation quietly deciding the excerpt reads better as a summary.**

**2. There is no vector database, and the corpus does not ship loaded.** The request named a vector
database holding the common robots. The arithmetic in "Retrieval" answers the first half: at this
scale a brute-force scan is milliseconds, so a vector database buys nothing and costs a service to
deploy and explain. The capability is identical; the infrastructure is not there.

The second half is copyright, not engineering: manuals from KUKA, ABB, FANUC, Universal Robots and
Siemens may not be redistributed. What is versioned is the *recipe*. Somebody who clones this
repository does not find a library — they find a list and a command that builds one on their own
machine, from documents they are entitled to have.

**3. A new robot does not walk straight in.** The feedback loop is real and is stage 4, but a newly
fetched document lands in quarantine and is not searchable until approved — automatically *with
notice* in Study Mode, never on any path towards Workshop Mode. "Let the agent go and read the
documentation" is also a way to get attacker-controlled text into a system that plans actions on
machinery, and that hole is closed the same way every other one here is.

## Stages

Each stage stands on its own and is cut from the bottom. Stage 0 and stage 2b were added on
2026-08-27, after the brief was read back against the request that produced it; they are numbered
this way so that no existing stage number moves.

0. **Project review on connect.** Read-only, and it depends on nothing else, which is why it is
   numbered zero rather than appended. The server is pointed at a project and reports what is in it:
   CPU order number and firmware, modules, existing blocks, tag tables — and, once stage 1 exists,
   what it could and could not corroborate against cited hardware. It writes nothing, so it needs no
   guard; it composes the read tools the server already has, and only earns a new one where something
   genuinely cannot be read today. Without an index it still gives the inventory and says plainly
   that the hardware context is unavailable.

1. **Local index with citations.** Three to five devices, PDFs supplied by hand. No ingestion from
   the web. Hybrid search, answers carry page numbers.
2. **Cited hardware context in the `ChangePlan`.** Cheap, and the most visible.

2b. **The work plan is shown before the first write, not only recorded.** Half of this exists
   already and has since phase 1: every write produces and audits a `ChangePlan` before it runs. What
   is missing is that in Study Mode those plans confirm themselves one at a time, so a plan for the
   *job* — these six blocks, this tag table, this download — is never put in front of anybody. This
   stage renders that plan before the first write of a batch and requires an acknowledgement to
   continue.

   It follows the quarantine rule rather than the checklist rule, and the difference is deliberate:
   in Study Mode the plan is rendered and audited and then auto-acknowledged *with notice*, because
   the harness runs batches unattended and a stall there would be a defect; on any path towards
   Workshop Mode the acknowledgement is a person's, item by item. It does not replace the per-write
   plan. It precedes it.

3. **The retrieval gate.** Fifty questions, two metrics, one blocking threshold. **Do not proceed
   past this stage without it.**
4. **On-demand ingestion**, with whitelist, quarantine and audit.
5. **The cited safety checklist**, universal and class tiers static, specific tier cited. Only if
   stage 3 cleared.

## Definition of done

The repository's existing definition applies in full — no warnings, tests covering the happy path
and at least one error case, XML doc on public members of the portal layer, no dead code, errors
mapped to the model in `CLAUDE.md`, and `STATUS.md` updated.

Three additions specific to this work:

- **Every rule in "The cardinal rule" and "Checklist design" has a test that names it** and would
  fail if the rule were removed. Incidental coverage does not count.
- **The knowledge tests must run without TIA Portal**, for the same reason the governance tests do:
  a rule that can only be checked on a licensed machine stops being checked.
- **The universal checklist is reviewed and signed by a person** before it is shown to anybody. An
  unreviewed checklist ships disabled.
