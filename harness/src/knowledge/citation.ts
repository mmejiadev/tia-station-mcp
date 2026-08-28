/**
 * What retrieval is allowed to return.
 *
 * @remarks
 * Every type here exists to make the cardinal rule of `docs/KNOWLEDGE-LAYER.md` structural rather
 * than a matter of discipline: the system cites, it does not author. A citation carries the words
 * of a document and the coordinates a reader needs to go and check them; there is no field on it
 * for a summary, a paraphrase or an answer, because a field that existed would eventually be
 * filled.
 */

/** Identifiers are their own types here for the same reason they are in `telemetry.ts`. */
export type DocumentId = number & { readonly brand: 'DocumentId' };

/** A chunk is a slice of one page of one document, never a stitch of two. */
export type ChunkId = number & { readonly brand: 'ChunkId' };

/** One indexed document, described by where it came from rather than by what it says. */
export type CorpusDocument = {
  readonly documentId: DocumentId;
  /** The equipment the document is about, as the recipe names it: `UR5e`, `C4000`, `DSBC`. */
  readonly device: string;
  readonly title: string;
  /** Software or edition the document itself states. Part of every citation. */
  readonly version: string;
  /** Where a reader gets their own copy. The file is never committed; see the recipe. */
  readonly sourceUrl: string;
  readonly sha256: string;
  readonly pageCount: number;
  readonly ingestedAt: number;
};

/**
 * One verbatim excerpt, with everything needed to find it in the original.
 *
 * @remarks
 * `excerpt` is a literal span of the page it names — not normalised, not trimmed of the awkward
 * hyphenation a PDF puts in, not tidied. `citationVerbatim.test.ts` asserts exactly that, and it
 * is the test that would fail first if anything ever started rewriting what it found.
 */
export type Citation = {
  readonly device: string;
  readonly title: string;
  readonly version: string;
  /** One-based, as printed in the document's own page furniture is not guaranteed — this is the
   * PDF page index, which is what a reader can jump to. */
  readonly page: number;
  readonly excerpt: string;
  /** Fused rank score. Reported so a weak match can be seen to be weak. */
  readonly score: number;
};

/**
 * The two answers retrieval is permitted to give.
 *
 * @remarks
 * There is no third. A lookup that found nothing does not degrade into a general answer, and the
 * absence of `cited` is why `NotFoundAnswer` is a constant rather than a sentence composed at the
 * call site: one wording, in one place, that a test can pin.
 */
export type LookupOutcome = 'cited' | 'not-found';

/** What every lookup returns, found or not. */
export type LookupResult = {
  readonly outcome: LookupOutcome;
  readonly query: string;
  /** Empty exactly when the outcome is `not-found`. */
  readonly citations: readonly Citation[];
};

/**
 * The answer when nothing can be cited.
 *
 * @remarks
 * The gap is never filled. This sentence is the whole of the response, and it points at the only
 * thing that is authoritative when the index is silent.
 */
export const NotFoundAnswer =
  'Not found in the indexed corpus. Open the manufacturer\u2019s manual for this equipment.';

/**
 * The footer that accompanies every rendered citation.
 *
 * @remarks
 * Fixed and unskippable, per the brief. It is here rather than in the skill because the skill is
 * one surface of two — the MCP prompts are the other — and a second copy would diverge.
 */
export const CitationFooter =
  'Excerpts are quoted verbatim. They do not replace the manufacturer\u2019s manual or the supervisor who is physically present.';
