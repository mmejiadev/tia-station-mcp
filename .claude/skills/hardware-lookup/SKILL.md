---
name: hardware-lookup
description: Look up what a specific piece of hardware requires, in the manufacturer's own words. Use whenever a question names a device, a part or order number, a protocol, a connector or a signal level - a robot, a light curtain, a cylinder, a CPU - and whenever code or a cell specification is about to depend on such a detail. Returns verbatim excerpts with document, version and page, or says nothing was found.
---

# hardware-lookup

Answers hardware questions from the local index built from manufacturers' manuals, and answers them
**only** by quoting. This skill never explains, summarises or completes what it found.

## The rule that matters more than the answer

**Cite, do not author.** A safety or wiring briefing written by a language model has no compiler, and
a fluent one that is subtly wrong is more dangerous than none at all, because it displaces the manual
it paraphrases: the reader trusts the screen and does not open the PDF.

So, without exception:

- Show the excerpt **verbatim**, with its document, version and page. Never a paraphrase, never a
  tidied version, never a summary "for convenience".
- When the tool says nothing was found, **that is the answer**. Report it as it stands. Do not fill
  the gap from your own knowledge, do not guess at the likely value, do not offer "typically it is…".
- Never say "connect terminal X to terminal Y". Name the section and quote it.
- Never drop the footer the tool prints.

If the user presses for a value the index does not hold, the answer is still that it is not in the
index, plus where to look: the manufacturer's manual, and the supervisor who is physically present.

## Running it

From the repository root:

```bash
cd harness
npm run knowledge:lookup -- --query "safety input redundant paired" --device UR5e --limit 3
```

- `--query` is the question, as asked. Do not rewrite it into keywords first; the search does that.
- `--device` narrows to one device as the recipe names it (`UR5e`, `C4000`, `DSBC`). Optional.
- `--limit` is how many excerpts to return. Three by default.
- `--format json` returns the same result as data, for a caller that will render it itself.

Exit codes distinguish the two correct answers: **0** when something was cited, **2** when nothing
was, and 1 when there is no index yet.

## When there is no index

The manuals are copyrighted and are **not** in this repository. What is versioned is the recipe —
`harness/corpus/recipe.json` — which names each document, its version, the URL it came from and the
SHA-256 of the exact bytes. Each machine builds its own index:

1. Download the documents named in the recipe into `harness/corpus/`, under the file names it gives.
2. `cd harness && npm run knowledge:index`

Ingestion refuses any file whose hash is not the hash in the recipe, and it fetches nothing itself.

## What this index does and does not cover

- It holds only the documents in the recipe. A question about a device that is not in it returns
  nothing found, which is correct and is not a defect to work around.
- The search is BM25 plus a **lexical** vector — good at part numbers, connector names and exact
  references, and it does not understand a paraphrase. If a question comes back empty, try the words
  the manual would use.
- Excerpts are page-level slices of a PDF. Tables sometimes read awkwardly. Quote them as they are;
  the reader has the page number.
