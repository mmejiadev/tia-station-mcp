# GRAFCET in set/reset LAD on an S7-1200

## Why set/reset on this CPU

S7-GRAPH, Siemens' native GRAFCET language, runs on S7-300, S7-400 and **S7-1500 only**. The TIA
Portal V20 S7-1200 manual (publication date 11/2024) lists LAD, FBD and SCL for the S7-1200 and no
GRAPH. So on a 1200 a chart is programmed by hand, and the method taught in class is set/reset:
one memory bit per step.

Other hand methods exist (an integer step number with comparisons, a CASE in SCL, a bit shifted
along a word) and are fine; this skill reads and writes set/reset because that is what students hand
in and what the checker was measured against.

## The method

1. **One Bool per step**, `X0`, `X1`, … Give the steps of one sequence **consecutive addresses**
   (`X30..X45` → `%M10.0..%M11.7`) if anything will ever clear them as a range.
2. **Initialisation**, on `FirstScan` (the system memory bit): `S` every initial step, `R` every
   other step. Prefer one `R` per step. `R_BF "X31", n := 15` clears fifteen consecutive **bits of
   memory**, not fifteen steps: it is only right if those steps sit at consecutive addresses.
3. **One network per transition**: the source step(s) and the transition-condition in series, then
   `S` on every target step and `R` on every source step, all from the same junction.

   ```text
   | X1   CE        +--( S )-- X2
   |-] [--] [-------+
   |                +--( R )-- X1
   ```

4. **Outputs in a separate block**, as plain coils `( )`: each output once, fed by the OR of every
   step that drives it. Never `S`/`R` on a continuous action, and never two coils on one output.
5. **Timers**: a `TON` whose `IN` is the step and whose `PT` is the time; its `Q` is the condition
   of the transition that leaves the step. Each TON needs its own IEC_TIMER instance.
6. **Call order in Main**: initialisation first, then the sequences, then the outputs.

## Faults found in real student code, and how the checker names them

| Fault | What happens | Checker says |
|---|---|---|
| A step written with a plain coil `( )` instead of `S`/`R` | The step drops the instant its condition goes false; the sequence falls apart | `step Xn is driven by a plain coil` |
| `S` next step without `R` of the current one | Two steps of one sequence stay active | `has no source step` |
| Unquoted operand, e.g. `I` typed without its tag | TIA reads it as the start of an address: "The specified value "I" is invalid" | `operand I is not a tag` |
| Two plain coils on one output | The one written last wins every scan | `has a second plain coil` |
| `R_BF` over non-consecutive step addresses | Clears another sequence's steps, misses its own; it compiles | `covers …, which also holds …` / `does not reach …` |
| Same level condition on two consecutive transitions | The middle step is crossed in one scan and its outputs never appear | `transient-evolution` |
| Selection branches that can both be true | The network written first wins, silently | `non-exclusive-selection` |

## Scan order and transient evolution

A PLC runs the networks top to bottom once per scan. In set/reset code, if the network for 1→2
comes *before* the network for 2→3 and both conditions are true, the sequence advances two steps in
one scan: step 2 is never active at the end of a scan, so its outputs never reach the field. That is
the PLC form of GRAFCET's transient evolution.

It is right when the chart means it and wrong when it does not. When it must not happen:

- use an **edge** (`P_TRIG`/`R_TRIG`) on the condition that repeats, or
- make the second condition impossible on arrival (for example require the sensor to go false and
  true again), or
- evaluate every transition into temporary bits first and apply all `S`/`R` afterwards — one
  evolution per scan, which is also what rules 3 and 4 of IEC 60848 ask for.

## Facts about TIA Portal V20 measured while building this skill (2026-09-24)

- `ExportAsDocuments` (SIMATIC SD, `.s7dcl`) exports a LAD block **even when it does not compile**;
  SimaticML `Export` refuses ("Inconsistent blocks … cannot be exported"). The MCP server's bulk
  document export skipped them by its own choice until `work/export-inconsistent-docs`, which exports
  them and lists them in `Inconsistent`.
- In an exported `.s7dcl`, an operand TIA could not resolve appears unquoted: `Contact( I )`.
- A TON whose output feeds the transition is written `"IEC_Timer_0_DB".TON( pt := T#30S, et => )`
  followed by `wire#w1`; its `Q` can only be true while its `IN` is, which is how `X36 → TON →
  S X37 / R X36` reads as the transition 36 → 37 on `T36`.
- A project cannot be opened from a folder path longer than **143 characters**.
- TIA Portal asks again for Openness access **every time the calling executable is rebuilt**; until
  someone answers, the connection waits and then fails with "Connection to TiaPortal failed".
- Generated documents, imported into a copy of a real project: all three blocks import
  ("Import of block(s) succeeded"); the initialisation and outputs blocks compile. The sequence
  block with a timer compiles only once its **IEC_TIMER instance DB exists**: until then TIA reports
  "Missing instance DB". Import does not create it.
- A junction (`wire#w1`) that nothing else uses is refused on import: "Instruction 'Coil' : Pin
  'in' connection is missing". The generator only writes a junction that is shared.
- **Not yet measured**: creating the timer DB automatically (Openness `CreateInstanceDB`, or an SCL
  source declaring `IEC_TIMER`). Until it is, create each one by hand in TIA — a data block of type
  IEC_TIMER named as the generated code expects, e.g. `T72_DB` — and compile.

## Sources

- SIMATIC S7-1200 manual collection for TIA Portal V20 (es-ES), programming languages, 11/2024:
  https://docs.tia.siemens.cloud/r/simatic_s7_1200_manual_collection_eses_20/programming-concepts/programming-language/ladder-logic-lad
- Set and reset instructions, same manual:
  https://docs.tia.siemens.cloud/r/simatic_s7_1200_manual_collection_eses_20/basic-instructions/bit-logic-operations/set-and-reset-instructions
- "Grafcet en TIA Portal: qué es y 5 formas de crearlo", programacionsiemens.com, updated
  2026-09-07 — the five hand methods and why GRAPH is S7-1500 only:
  https://programacionsiemens.com/grafcet-en-tia-portal-5-formas-de-crearlo
- González-Rodríguez et al. (2025), section 4.3: repeated conditions on consecutive transitions and
  rule 5 in an if/else-if transcription. https://doi.org/10.21203/rs.3.rs-7752548/v1
