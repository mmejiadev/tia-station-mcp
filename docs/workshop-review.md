# Workshop review

Criterion 5 of the workshop gate: **an in-person design review with the supervising teacher, before
a single line of Workshop Mode code is written.**

It is the one criterion no measurement can answer. The other four are queries against the metrics
database and the audit trail; this one is a person having looked at the design in the same room as
the machine, and the only thing software can do about it is refuse to pretend otherwise.

## How the gate reads this file

`npm run gate` looks for one line, anywhere in this file:

```
reviewed: YYYY-MM-DD by <who reviewed it>
```

with a real date in `YYYY-MM-DD` form. Until that line is here, criterion 5 is not met and the gate
answers no — which is the correct answer, not a failure.

Everything else in this file is for people. Notes from the review, what was raised, what was
changed afterwards: all of it belongs here, under the line.

## What the review is for

Workshop Mode is the only part of this project that commands physical machinery. The rules it runs
under are in `CLAUDE.md` and the first of them is the one no software enforces: **it may only be
used with a teacher or workshop supervisor physically present, with access to the emergency stop.**

The review exists so that rule is agreed by the person it depends on, before the code that would
need it exists — not after, when finishing it is the thing everybody wants.

## Notes

_None yet: the review has not happened._
