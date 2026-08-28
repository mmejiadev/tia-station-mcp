import { Bm25Index, queryCoverage } from './bm25.ts';
import { cosine } from './lexicalVector.ts';
import type { Citation, LookupResult } from './citation.ts';
import type { EmbeddingModel } from './embeddingModel.ts';
import type { KnowledgeStore } from './knowledgeStore.ts';

/**
 * The lookup: two rankings, one fusion, and a rule for staying silent.
 *
 * @remarks
 * The brief says the abstention rate matters more than the precision — *a retriever that never
 * stays silent is dangerous; one that stays silent well is trustworthy* — so silence is a decision
 * this class makes explicitly, in {@link isAdmissible}, rather than something that happens to fall
 * out of an empty result set. A rank on its own qualifies nothing.
 *
 * Fusion is reciprocal rank, not a weighted sum of the two scores. BM25 scores and cosines are not
 * on the same scale and never will be; adding them requires a weight nobody can justify, and the
 * weight silently decides everything. Ranks are comparable by construction.
 */

/** Reciprocal-rank constant. 60 is the published default and there is no reason to tune it here. */
const RankConstant = 60;

/** How many candidates each ranking contributes before fusion. */
const CandidatesPerRanking = 20;

/**
 * The BM25 score a chunk needs before it may be cited at all.
 *
 * @remarks
 * Fixed here, in code, deliberately: the brief asks for the retrieval threshold to be set in the
 * cold, the way `RequiredCompleteRuns` was, and not adjusted by whoever wants an answer today.
 * Below it, the query terms are either absent or so common that their presence says nothing.
 */
const AdmissibleBm25Score = 1.0;

/**
 * The cosine a chunk needs to be admitted on the vector side alone.
 *
 * @remarks
 * High, because {@link TrigramModel} is a lexical signal: a low trigram cosine between two
 * technical passages is near-meaningless overlap of common letter sequences, not a weak semantic
 * match. This threshold is what stops that noise becoming a citation.
 */
const AdmissibleCosine = 0.55;

/**
 * How much of the question a chunk must actually contain before it may be quoted.
 *
 * @remarks
 * The rule that makes abstention work, and it was added because the first version did not abstain.
 * *What is the capital of France* came back with three excerpts from a robot manual: `capital`
 * occurs somewhere in five hundred pages, it is rare, so BM25 ranked it well and the threshold above
 * let it through. A ranking answers "which of these is least bad", never "is any of these good".
 *
 * Two thirds, so a question whose terms are mostly absent is refused while one asked in a full
 * sentence still lands. It is set here, in code, for the same reason the other two are.
 */
const MinimumTermCoverage = 2 / 3;

/** How a lookup is narrowed. Both are optional; neither invents anything when absent. */
export type LookupOptions = {
  /** Restricts to one device as the recipe names it. Compared case-insensitively. */
  readonly device?: string;
  readonly limit?: number;
};

/** How many citations a lookup returns when it is not narrowed. */
const DefaultLimit = 3;

type Candidate = {
  readonly chunkId: number;
  readonly bm25Score: number;
  readonly cosine: number;
  readonly fusedScore: number;
};

/** Hybrid retrieval over one opened index. Built once, queried many times. */
export class HybridSearch {
  private readonly store: KnowledgeStore;

  private readonly model: EmbeddingModel;

  private readonly bm25: Bm25Index;

  private readonly vectors: readonly { chunkId: number; vector: Float32Array }[];

  constructor(store: KnowledgeStore, model: EmbeddingModel) {
    this.store = store;
    this.model = model;
    this.bm25 = new Bm25Index(store.readIndexableChunks());
    this.vectors = store.readVectors();
  }

  /** How many chunks are searched. Reported with every answer, so no result hides its corpus. */
  get indexedChunks(): number {
    return this.bm25.size;
  }

  /**
   * Answers a question with excerpts, or says it cannot.
   *
   * @param query The question as it was asked. Never rewritten.
   * @returns Citations in fused-rank order, or `not-found` with no citations at all.
   */
  lookup(query: string, options: LookupOptions = {}): LookupResult {
    const citations = this.rank(query)
      .map((candidate) => this.toCitation(query, candidate))
      .filter((citation): citation is Citation => citation !== undefined)
      .filter((citation) => matchesDevice(citation, options.device))
      .slice(0, options.limit ?? DefaultLimit);

    if (citations.length === 0) {
      return { outcome: 'not-found', query, citations: [] };
    }

    return { outcome: 'cited', query, citations };
  }

  /** The two rankings, fused. Every candidate keeps both raw scores so admission can read them. */
  private rank(query: string): Candidate[] {
    const lexical = this.bm25.search(query, CandidatesPerRanking);
    const vector = this.rankByVector(query);
    const merged = new Map<number, { bm25Score: number; cosine: number; fusedScore: number }>();

    for (const [rank, scored] of lexical.entries()) {
      const entry = merged.get(scored.chunkId) ?? { bm25Score: 0, cosine: 0, fusedScore: 0 };
      merged.set(scored.chunkId, {
        ...entry,
        bm25Score: scored.score,
        fusedScore: entry.fusedScore + 1 / (RankConstant + rank + 1),
      });
    }

    for (const [rank, scored] of vector.entries()) {
      const entry = merged.get(scored.chunkId) ?? { bm25Score: 0, cosine: 0, fusedScore: 0 };
      merged.set(scored.chunkId, {
        ...entry,
        cosine: scored.cosine,
        fusedScore: entry.fusedScore + 1 / (RankConstant + rank + 1),
      });
    }

    return [...merged.entries()]
      .map(([chunkId, entry]) => ({ chunkId, ...entry }))
      .sort((left, right) => right.fusedScore - left.fusedScore);
  }

  /** Brute-force cosine over every vector. No index, by the arithmetic in the brief. */
  private rankByVector(query: string): { chunkId: number; cosine: number }[] {
    const queryVector = this.model.embed(query);

    return this.vectors
      .map((entry) => ({ chunkId: entry.chunkId, cosine: cosine(queryVector, entry.vector) }))
      .sort((left, right) => right.cosine - left.cosine)
      .slice(0, CandidatesPerRanking);
  }

  /**
   * Turns a ranked chunk into a citation, or refuses it.
   *
   * @remarks
   * Admission happens here rather than before, because the coverage rule needs the chunk's words
   * and only the store has them. The excerpt is the stored text, unchanged.
   */
  private toCitation(query: string, candidate: Candidate): Citation | undefined {
    const stored = this.store.readChunk(candidate.chunkId);

    if (stored === undefined || !isAdmissible(candidate, queryCoverage(query, stored.text))) {
      return undefined;
    }

    return {
      device: stored.document.device,
      title: stored.document.title,
      version: stored.document.version,
      page: stored.pageNumber,
      excerpt: stored.text,
      score: candidate.fusedScore,
    };
  }
}

/**
 * The silence rule, in one place.
 *
 * @remarks
 * Both halves must hold. Coverage says the chunk is about what was asked; the score says it is a
 * good instance of that. Either one alone was tried and was not enough — coverage alone quotes a
 * passage that mentions every word and answers nothing, and the score alone quoted a robot manual
 * about the capital of France.
 */
function isAdmissible(candidate: Candidate, coverage: number): boolean {
  if (coverage < MinimumTermCoverage) {
    return false;
  }

  return candidate.bm25Score >= AdmissibleBm25Score || candidate.cosine >= AdmissibleCosine;
}

function matchesDevice(citation: Citation, device: string | undefined): boolean {
  if (device === undefined) {
    return true;
  }

  return citation.device.toLowerCase() === device.toLowerCase();
}
