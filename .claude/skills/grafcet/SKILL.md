---
name: grafcet
description: Read, check, draw and program GRAFCET (IEC 60848) sequential charts. Use whenever a request involves a GRAFCET, SFC or sequential function chart, a photo or whiteboard of steps and transitions, set/reset ladder built from a chart, "etapas/transiciones/receptividades", or a PLC sequence that misbehaves - to transcribe a chart, find design faults, draw it cleanly, recover the chart a TIA Portal LAD program really runs, or generate set/reset LAD for an S7-1200.
---

# grafcet

Turns a GRAFCET into something checkable. A chart comes in from one of three places — a photo or a
statement, a TIA Portal program, or a text file — and goes out as a drawing with the IEC 60848
symbols, a list of design faults, and, when asked, set/reset LAD that TIA Portal V20 imports.

## The rule that matters more than the drawing

**Never guess a chart.** A misread overbar produces code that compiles and sends a good part down
the reject line. So:

- Transcribe what is drawn, and mark every doubt with `?` (`?/PS`). Ask about each one by name.
- When the student's code or tag table exists, it outranks a photo of the board.
- Nothing is generated while a `?` remains; the generator refuses, and that is intended.
- Report findings as findings. Fix a student's project only when asked, and then with the smallest
  change that fixes it (see "Correcting a class project").

## Workflows

All commands run from `harness/`.

### A chart from a photo, a whiteboard or a statement

1. Read `reference/reading-charts.md` and follow its protocol.
2. Write the chart in the text notation (`reference/notation.md`) to a scratch file.
3. `npm run grafcet -- --text chart.txt --html chart.html` — prints the chart back, the findings,
   and writes a page with the drawing. Open it or screenshot it headless and look at it.
4. Put every `?` and every error to the person before going further.

### The chart a TIA Portal program really runs

1. Export the program blocks as SIMATIC SD documents (`ExportBlocksAsDocuments`, or
   `ExportAsDocuments` per block). A block that does not compile can still be exported this way —
   measured through Openness on 2026-09-24. Builds before `work/export-inconsistent-docs` skip
   such blocks in the bulk tool; if a block is missing from the export, export it on its own, or
   check the response's `Inconsistent` list on newer builds.
2. Export the tag table (`ExportSourceSnapshot` writes `tags/*.xml`).
3. `npm run grafcet -- --s7dcl <folder> --tags "<folder>/tags/Default tag table.xml" --html chart.html`
4. Compare the drawing with the intended chart. Differences are where the bugs are: a sequence drawn
   in pieces means steps written with plain coils; an `R_BF` finding means a range that clears the
   wrong steps.

### Generating set/reset LAD

1. The chart must pass without `?`, with an initial step in every sequence.
2. `npm run grafcet -- --text chart.txt --lad out/` writes `GRAFCET_INIT`, one block per sequence,
   and `GRAFCET_OUTPUTS` as `.s7dcl`.
3. Create the tags first (`CreateTag`: every `Xn`, every input and output, each timer's done bit),
   then `ImportBlocksFromDocuments`, then `CompileSoftware`. Call the blocks from Main in that order.
   A timer also needs its IEC_TIMER instance DB (`T72_DB` for `T72`); import does not create it,
   and creating it automatically has not been measured yet, so say so and have it created in TIA.
4. Read `reference/set-reset-lad.md` for what the generated code does and does not do.

### Correcting a class project

The process that worked, and that the user asked to keep:

1. **Read before writing**: compile to get the error list (it names the tags the student used),
   export the blocks as documents, read them.
2. **Minimal corrections**: edit the exported text, change only what is wrong, show the diff.
3. **The student's own table wins** over any reading of a photo.
4. **Back up the whole project folder** first; **save only if the compile ends with 0 errors**.
5. **Look for what compiles but is wrong**: run this checker with the tag table — it found an
   `R_BF` clearing another conveyor's steps that no compiler reports.
6. **Report network by network**: what changed, why, and what the student must confirm.

## What the checks mean

| Rule | Meaning |
|---|---|
| `initial-step` | A sequence with no initial step never runs. |
| `duplicate-step` | A step number used in two sequences. |
| `unreachable-step` | Nothing leads to the step from an initial step. |
| `dead-end-step` | The step has no way out. |
| `uncertain-reading` | A `?` still waiting for confirmation. |
| `non-exclusive-selection` | Two branches of a selection can clear together. |
| `transient-evolution` | A step can be crossed without stopping: its outputs may never appear. |
| `unknown-step-reference` | A condition reads a step nobody drew. |

LAD findings (plain coil on a step, missing reset, unquoted operand, double coil, `R_BF` range) are
described in `reference/set-reset-lad.md`.

## Limits

- **No hardware or safety answers.** What a sensor, a cylinder or a light curtain requires — wiring,
  signal levels, safety functions — goes to `hardware-lookup`, which quotes the manual. This skill
  explains a method; it does not describe devices.
- Macro-steps, enclosing steps and forcing orders are recognised in reading but not modelled, drawn
  or generated. Say so rather than flatten them.
- Edges are drawn and checked but not generated: the SD syntax for them has not been measured.
- S7-GRAPH is S7-1500 only; on an S7-1200 this set/reset method, or SCL, is the way.
- The rules of the language are summarised, with sources and dates, in `reference/iec-60848.md`.
  The standard itself (IEC 60848:2013, stable until 2028) is copyrighted and is not in this
  repository.
