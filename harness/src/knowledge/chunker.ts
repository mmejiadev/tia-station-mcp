/**
 * Cuts one page into the units retrieval ranks and quotes.
 *
 * @remarks
 * Every chunk this produces is a **literal slice** of the page it came from — `text.slice(start,
 * end)` and nothing else. No trimming, no whitespace collapsing, no repair of the hyphenation a
 * PDF puts at a line break. That is not laziness: a citation is only verbatim if the thing stored
 * is the thing printed, and the moment chunking is allowed to tidy, the excerpt on screen stops
 * being what the manual says. `citationVerbatim.test.ts` holds the line.
 *
 * Chunks never span a page, because a citation names one page and an excerpt that straddled two
 * would name the wrong one for half of itself.
 */

/** A slice of a page, with the coordinates that prove it is one. */
export type PageChunk = {
  readonly pageNumber: number;
  readonly startOffset: number;
  readonly endOffset: number;
  readonly text: string;
};

/** Roughly a paragraph. Long enough to answer, short enough to be quotable on screen. */
const TargetCharacters = 900;

/** A hard ceiling, used when a page has no boundary to cut at — a table, or one long line. */
const MaximumCharacters = 1400;

/** Carried into the next chunk so an answer sitting on a cut is still whole somewhere. */
const OverlapCharacters = 150;

/** Below this a chunk is page furniture — a header, a page number — and indexing it adds noise. */
const MinimumCharacters = 40;

/**
 * Offsets where a cut does not fall inside a sentence.
 *
 * @remarks
 * Blank lines first, then line breaks, then sentence ends. All three are recorded in one pass and
 * sorted, so the cut logic can simply take the last boundary that fits.
 */
function findBoundaries(text: string): number[] {
  const boundaries: number[] = [];
  const pattern = /\n{2,}|(?<=[.:;!?])\s+|\n/g;

  for (const match of text.matchAll(pattern)) {
    boundaries.push(match.index + match[0].length);
  }

  boundaries.push(text.length);

  return boundaries;
}

/** The last boundary that fits the ceiling, or the ceiling itself when none does. */
function chooseEnd(boundaries: readonly number[], start: number, textLength: number): number {
  const ceiling = Math.min(start + MaximumCharacters, textLength);
  const target = start + TargetCharacters;
  let chosen = ceiling;

  for (const boundary of boundaries) {
    if (boundary > start && boundary <= ceiling) {
      chosen = boundary;
    }

    if (boundary >= target) {
      break;
    }
  }

  return chosen;
}

/**
 * Cuts one page into chunks.
 *
 * @param pageText The page exactly as it was extracted. Passed through untouched.
 * @param pageNumber One-based PDF page index, carried into every citation.
 * @returns Chunks in reading order. Empty for a blank page, which is normal and not an error.
 */
export function chunkPage(pageText: string, pageNumber: number): PageChunk[] {
  const boundaries = findBoundaries(pageText);
  const chunks: PageChunk[] = [];
  let start = 0;

  while (start < pageText.length) {
    const end = chooseEnd(boundaries, start, pageText.length);
    const text = pageText.slice(start, end);

    if (text.trim().length >= MinimumCharacters) {
      chunks.push({ pageNumber, startOffset: start, endOffset: end, text });
    }

    start = end >= pageText.length ? pageText.length : Math.max(start + 1, end - OverlapCharacters);
  }

  return chunks;
}
