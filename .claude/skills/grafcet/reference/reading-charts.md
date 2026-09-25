# Reading a chart from a photo, a whiteboard or a statement

Handwriting is where charts get misread, and a misread chart produces code that compiles and does
the wrong thing. The protocol below exists because on 2026-09-24 two negation bars on a
photographed whiteboard could not be told apart, and guessing would have swapped the good and the
reject branch of a weighing station.

## The protocol

1. **Transcribe, do not interpret.** Write the chart in the text notation (`notation.md`) exactly
   as drawn, sequence by sequence, before thinking about what it should do.
2. **Mark every doubt with `?`.** An overbar you are not sure of is `?/PS`; an unsure name is
   `?PS`. Never resolve a doubt by what "makes sense": a reject branch taken on a good part also
   makes sense to whoever drew it wrong.
3. **Run the checker and draw it.** `npm run grafcet -- --text chart.txt --html chart.html`. The
   drawing shows every `?` in the warning colour.
4. **Ask about each `?` by name**, quoting the transition: "37 → 42: is it PS·BASCULA or
   PS·/BASCULA?". Ask the person, or say that the teacher's statement decides.
5. **Prefer the code or the I/O table over the photo** when both exist. A tag table the student
   typed is a better source for names and addresses than a picture of a board.
6. **Generate nothing while a `?` remains.** The generator refuses, and that refusal is the point.

## What to look at on the drawing

| Element | Look for | Common misreading |
|---|---|---|
| Initial step | Double frame | A single frame read as initial because it is at the top |
| Step number | The number inside the frame | 38 vs 36, 41 vs 11 in handwriting |
| Transition bar | Short thick line across the link | A crossing link mistaken for a bar |
| Condition | Text right of the bar | `·` (AND) lost, two variables read as one name |
| Overbar | A line *above* the name | An underline of the line above, or none at all |
| Edge | `↑` / `↓` before the name | Dropped: an edge becomes a level and the chart turns transient |
| Action box | Rectangle right of the step | A condition written above the box is part of the action |
| Timer | `T36 = 30 s`, `30s/X36`, `T36=30"` | Seconds vs minutes (`"` vs `'`) |
| Selection | Single horizontal line under the step | Read as parallel: a parallel split is a *double* line |
| Return | Line going up with an arrow, or an arrow to `X30` | The target step of a jump |
| Cross-sequence condition | `X43` in another sequence's transition | Read as an input called X43 |

## Naming

Keep the names on the drawing (`MPC↑` → `MPC_U`, `MPTE` → `MPT_E`) and write the mapping down;
the tags the student already created win over anything the skill would choose. Symbols that are
not valid tag names are renamed with the mapping stated, never silently.
