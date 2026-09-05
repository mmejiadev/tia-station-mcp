import type { LookupResult } from './citation.ts';

/**
 * Whether the retrieval is good enough to be shown to anybody.
 *
 * @remarks
 * The knowledge layer's brief asks for its own gate, in the spirit of the five criteria in
 * `gate.ts`, and for the same reason: this repository built a gate that tells its own author no, and
 * adding a large unmeasured component would put a hole in the middle of that.
 *
 * Two numbers, and the thresholds below are fixed **in the cold** — written before any of them had
 * been measured, so that a failing run is a result rather than an invitation to move a constant.
 * Changing one is a pull request with a name on it.
 */

/**
 * How often an answer that *is* in the corpus has to be found.
 *
 * @remarks
 * Seventy percent, and it is a judgement rather than a derived figure. A retriever that finds two
 * answers in three is useful to somebody who still has the manual open, which is the only way this
 * is ever meant to be used — the citation names a page so a person can go and read it.
 *
 * It is deliberately the *lower* of the two thresholds. Missing an answer wastes a reader's time;
 * quoting a confident irrelevance can send them to the wrong page of the wrong manual.
 */
export const RequiredCitationPrecision = 0.7;

/**
 * How often a question the corpus cannot answer has to be met with silence.
 *
 * @remarks
 * Ninety percent, and higher than the precision threshold on purpose. The brief is explicit that
 * this matters more: *a retriever that never stays silent is dangerous; one that stays silent well
 * is trustworthy*. A ranking always has a best row, so staying silent is never the default — it is
 * a decision the search has to make, and this is the number that says whether it makes it.
 */
export const RequiredAbstentionRate = 0.9;

/**
 * How far from the recorded page a citation may land and still count.
 *
 * @remarks
 * One page. Chunks are cut by length rather than by page boundary, so the chunk carrying an answer
 * can begin on the page before the one a reader would cite. Zero tolerance would score that as a
 * miss for a citation that takes the reader to the right place.
 *
 * It is not a way of being generous: on a 357-page manual, one page either side is still an exact
 * answer, and a citation whose excerpt contains the expected phrase counts regardless of page.
 */
const PageTolerance = 1;

/** A question whose answer is in the corpus, with where it is. */
export type AnswerableQuestion = {
  readonly question: string;
  readonly device: string;
  /** The page a reader would cite, one-based. */
  readonly page: number;
  /** A phrase that page contains, checked against the corpus before scoring. */
  readonly contains: string;
};

/** A question the corpus cannot answer, with why it cannot. */
export type UnanswerableQuestion = {
  readonly question: string;
  /** Why it is unanswerable, so a reader can disagree with the judgement rather than the score. */
  readonly why: string;
};

/** The whole evaluation set. */
export type EvaluationSet = {
  readonly answerable: readonly AnswerableQuestion[];
  readonly unanswerable: readonly UnanswerableQuestion[];
};

/** What one answerable question scored, and why. */
export type CitationJudgement = {
  readonly question: string;
  readonly found: boolean;
  /** What the search did: cited something, or stayed silent. */
  readonly outcome: LookupResult['outcome'];
  /** The pages it cited, so a miss can be looked at rather than only counted. */
  readonly citedPages: readonly number[];
};

/** What one unanswerable question scored. */
export type AbstentionJudgement = {
  readonly question: string;
  readonly abstained: boolean;
  /** What it cited instead, when it did not abstain. Empty otherwise. */
  readonly citedInstead: readonly string[];
};

/** Both rates, the judgements behind them, and whether the gate opens. */
export type RetrievalVerdict = {
  readonly citationPrecision: number;
  readonly abstentionRate: number;
  readonly citations: readonly CitationJudgement[];
  readonly abstentions: readonly AbstentionJudgement[];
  readonly open: boolean;
};

/**
 * Judges one answerable question.
 *
 * @param question The question and where its answer lives.
 * @param result What the search returned for it.
 * @returns Whether the answer was found, and what was cited.
 * @remarks
 * A citation counts when it comes from the expected document **and** either lands within
 * {@link PageTolerance} of the recorded page or quotes the recorded phrase. Requiring the device to
 * match is what stops a right-looking page number from another manual scoring a point.
 */
export function judgeCitation(question: AnswerableQuestion, result: LookupResult): CitationJudgement {
  const fromDevice = result.citations.filter((citation) => citation.device === question.device);

  const found = fromDevice.some(
    (citation) =>
      Math.abs(citation.page - question.page) <= PageTolerance ||
      citation.excerpt.includes(question.contains)
  );

  return {
    question: question.question,
    found,
    outcome: result.outcome,
    citedPages: result.citations.map((citation) => citation.page)
  };
}

/**
 * Judges one unanswerable question.
 *
 * @param question The question the corpus cannot answer.
 * @param result What the search returned for it.
 * @returns Whether it stayed silent, and what it quoted if it did not.
 */
export function judgeAbstention(question: UnanswerableQuestion, result: LookupResult): AbstentionJudgement {
  if (result.outcome === 'not-found') {
    return { question: question.question, abstained: true, citedInstead: [] };
  }

  return {
    question: question.question,
    abstained: false,
    citedInstead: result.citations.map((citation) => `${citation.device} p${citation.page}`)
  };
}

/**
 * Adds the judgements up and answers whether the retrieval may be shown to anybody.
 *
 * @param citations What each answerable question scored.
 * @param abstentions What each unanswerable question scored.
 * @returns Both rates and the verdict.
 * @exception Error Either list is empty.
 * @remarks
 * An empty list is refused rather than scored as a perfect one. A gate that opens because nothing
 * was asked is the failure mode this whole file exists to prevent, and `0 / 0 = NaN` compares false
 * against every threshold, which would have made it fail for the wrong reason and been fixed by
 * someone into passing for the wrong reason.
 *
 * Both thresholds must be met. There is no averaging between them: a retriever that finds
 * everything and never stays silent is precisely the dangerous one the brief describes.
 */
export function evaluateRetrieval(
  citations: readonly CitationJudgement[],
  abstentions: readonly AbstentionJudgement[]
): RetrievalVerdict {
  if (citations.length === 0 || abstentions.length === 0) {
    throw new Error(
      'The retrieval gate needs both answerable and unanswerable questions. ' +
        'A rate over no questions is not a measurement.'
    );
  }

  const citationPrecision = citations.filter((one) => one.found).length / citations.length;
  const abstentionRate = abstentions.filter((one) => one.abstained).length / abstentions.length;

  return {
    citationPrecision,
    abstentionRate,
    citations,
    abstentions,
    open: citationPrecision >= RequiredCitationPrecision && abstentionRate >= RequiredAbstentionRate
  };
}
