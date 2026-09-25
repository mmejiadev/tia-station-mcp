# The text notation

One statement per line. `#` starts a comment. The same file is read by the checker, the drawing and
the LAD generator, and `formatGrafcet` writes it back unchanged, so it is safe to hand to a person
for correction.

The example shows every construct, not a working chart: run through the checker it reports its
loose ends (unreachable steps, the `?`), which is the checker doing its job.

```text
title Weighing cell                    # the page title

sequence CINTA E                        # starts a connected chart; its steps follow
step 0 initial                          # double frame
step 1 : MCE                            # continuous action
step 2 : ESPERA
step 3 : MCE
step 36 : timer T36 30s                 # on-delay started by the step; T36 is its done bit
step 5 : LAMP if /DOOR                  # conditional action
step 7 : set ALARM, reset HORN          # stored actions on activation
0 -> 1 : I                              # transition: from -> to : condition
1 -> 2 : CE
1 -> 0 : /I                             # a second way out: a selection
2 -> 3 : CE.PE
3 -> 0 : /CE
5,6 -> 7 : 1                            # AND convergence, always-true condition
7 -> 8,9 : GO                           # AND divergence
37 -> 42 : PS.?/BASCULA                 # ? = not read with certainty; must be confirmed
```

## Conditions

`.` `·` `*` AND · `+` OR · `/` `!` `¬` NOT · `↑` `↓` edges · `1` always true · parentheses group ·
`?` before a variable or a negation marks it unconfirmed · `Xn` reads step n.

## Rules the reader enforces

- A `sequence` line comes before any step or transition.
- A step named only by a transition is created with no actions; an **initial step is never
  created implicitly** — it must be declared with `initial`.
- A step is declared once. Step numbers are unique across the whole chart (the checker reports a
  duplicate as `duplicate-step`).
- Every error names its line; nothing is half-read.
