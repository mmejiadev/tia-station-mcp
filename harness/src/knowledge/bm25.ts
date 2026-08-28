/**
 * BM25 over the indexed chunks — the exact-match half of the hybrid search.
 *
 * @remarks
 * The brief calls this a hundred lines, and it is. It is also the half that does most of the work
 * in this domain: technical questions are exact references — an order number, a connector name, a
 * protocol — and a ranking built on term frequency and rarity is very good at those and needs no
 * model, no key and no network.
 *
 * The index is rebuilt in memory when the store is opened. At eighteen thousand chunks that is a
 * few tens of milliseconds and it removes a whole class of bug: there is no persisted posting list
 * that can fall out of step with the chunks it describes.
 */

/** Saturation of term frequency. 1.2 is the standard value and there is no reason to differ. */
const TermFrequencySaturation = 1.2;

/** How much document length is normalised. 0.75, likewise standard. */
const LengthNormalisation = 0.75;

/** One ranked chunk, keyed by the identifier the store gave it. */
export type ScoredChunk = {
  readonly chunkId: number;
  readonly score: number;
};

/** What the index needs of a chunk: an identifier and words. */
export type IndexableChunk = {
  readonly chunkId: number;
  readonly text: string;
};

/**
 * Splits text into the terms the index counts.
 *
 * @remarks
 * Exported because the tests pin its behaviour on part numbers, which is the case it exists for.
 * Digits stay attached to letters — `6es7` is one term, not two — because splitting them apart
 * turns every order number in the corpus into the same handful of tokens.
 */
export function tokenise(text: string): string[] {
  return text.toLowerCase().match(/[a-z0-9]+/g) ?? [];
}

/** A BM25 ranking over a fixed set of chunks. Rebuilt, never migrated. */
export class Bm25Index {
  private readonly postings = new Map<string, Map<number, number>>();

  private readonly lengths = new Map<number, number>();

  private readonly averageLength: number;

  constructor(chunks: readonly IndexableChunk[]) {
    for (const chunk of chunks) {
      this.add(chunk);
    }

    const total = [...this.lengths.values()].reduce((sum, length) => sum + length, 0);
    this.averageLength = this.lengths.size === 0 ? 0 : total / this.lengths.size;
  }

  /** How many chunks are ranked. Reported with results so no rate arrives without its sample. */
  get size(): number {
    return this.lengths.size;
  }

  private add(chunk: IndexableChunk): void {
    const terms = tokenise(chunk.text);
    this.lengths.set(chunk.chunkId, terms.length);

    for (const term of terms) {
      const posting = this.postings.get(term) ?? new Map<number, number>();
      posting.set(chunk.chunkId, (posting.get(chunk.chunkId) ?? 0) + 1);
      this.postings.set(term, posting);
    }
  }

  /** Inverse document frequency, in the form that cannot go negative on a common term. */
  private inverseDocumentFrequency(term: string): number {
    const documentsWithTerm = this.postings.get(term)?.size ?? 0;

    if (documentsWithTerm === 0) {
      return 0;
    }

    return Math.log(1 + (this.lengths.size - documentsWithTerm + 0.5) / (documentsWithTerm + 0.5));
  }

  private scoreTerm(term: string, scores: Map<number, number>): void {
    const posting = this.postings.get(term);

    if (posting === undefined) {
      return;
    }

    const idf = this.inverseDocumentFrequency(term);

    for (const [chunkId, frequency] of posting) {
      const length = this.lengths.get(chunkId) ?? 0;
      const normalisation = 1 - LengthNormalisation + (LengthNormalisation * length) / (this.averageLength || 1);
      const saturated = (frequency * (TermFrequencySaturation + 1)) / (frequency + TermFrequencySaturation * normalisation);
      scores.set(chunkId, (scores.get(chunkId) ?? 0) + idf * saturated);
    }
  }

  /**
   * Ranks the chunks against a query.
   *
   * @param query The question as it was typed. Not expanded, not corrected.
   * @param limit How many to return.
   * @returns Chunks with a non-zero score, best first. Empty when no query term appears anywhere,
   * which is a real answer and is what the abstention rule reads.
   */
  search(query: string, limit: number): ScoredChunk[] {
    const scores = new Map<number, number>();

    for (const term of new Set(tokenise(query))) {
      this.scoreTerm(term, scores);
    }

    return [...scores.entries()]
      .map(([chunkId, score]) => ({ chunkId, score }))
      .sort((left, right) => right.score - left.score)
      .slice(0, limit);
  }
}

/**
 * Words that carry no reference and must not count as evidence that a chunk answers a question.
 *
 * @remarks
 * Short and deliberately English-only: the corpus is English, and a list that tried to be general
 * would start deciding things about languages nobody has indexed. It exists for {@link queryCoverage}
 * alone — BM25 already handles common words correctly, by giving them almost no weight.
 */
const StopWords = new Set([
  'a', 'an', 'and', 'are', 'as', 'at', 'be', 'by', 'can', 'do', 'does', 'for', 'from', 'how', 'i',
  'in', 'is', 'it', 'me', 'must', 'of', 'on', 'or', 'shall', 'should', 'that', 'the', 'their',
  'they', 'this', 'to', 'what', 'when', 'where', 'which', 'why', 'with', 'you', 'your',
]);

/**
 * What fraction of the question's meaningful words actually appear in a chunk.
 *
 * @remarks
 * This is the measure the abstention rule reads, and it exists because a BM25 score alone cannot
 * carry it. *What is the capital of France* scores respectably against a robot manual: `capital` is
 * rare in the corpus, so one accidental occurrence lifts a chunk to the top of a ranking that has
 * nothing else to offer. The rank says which chunk is least bad; it never says whether any of them
 * is good. Coverage answers the second question, which is the one that decides whether to speak.
 *
 * @returns 0 to 1. A query made entirely of stop words has no meaningful words and returns 0, which
 * abstains — the correct answer to a question that asked nothing.
 */
export function queryCoverage(query: string, chunkText: string): number {
  const terms = new Set(tokenise(query).filter((term) => !StopWords.has(term)));

  if (terms.size === 0) {
    return 0;
  }

  const present = new Set(tokenise(chunkText));
  let found = 0;

  for (const term of terms) {
    found += present.has(term) ? 1 : 0;
  }

  return found / terms.size;
}
