import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { Citation, LookupResult } from '../src/knowledge/citation.ts';
import {
  evaluateRetrieval,
  judgeAbstention,
  judgeCitation,
  RequiredAbstentionRate,
  RequiredCitationPrecision,
  type AbstentionJudgement,
  type AnswerableQuestion,
  type CitationJudgement
} from '../src/knowledge/retrievalGate.ts';

/**
 * The gate that decides whether the retrieval may be shown to anybody. Every test here names one
 * way it must not be talked into opening — a score it should not have given, or a rate it should
 * not have computed. The questions themselves live in `harness/knowledge-eval/`; what is tested
 * here is the judging, which is the part that could quietly become generous.
 */
describe('retrieval gate', () => {
  it('counts an answer found on the page it was recorded on', () => {
    const judgement = judgeCitation(question(), cited([citation({ page: 47 })]));

    assert.equal(judgement.found, true);
  });

  it('counts an answer one page away, because chunks are not cut on page boundaries', () => {
    const judgement = judgeCitation(question(), cited([citation({ page: 46 })]));

    assert.equal(judgement.found, true);
  });

  it('does not count an answer two pages away', () => {
    const judgement = judgeCitation(question(), cited([citation({ page: 49 })]));

    assert.equal(judgement.found, false);
  });

  it('does not count the right page number in the wrong manual', () => {
    // The failure this check exists for: page 47 of a cylinder catalogue is not the answer to a
    // question about a robot, and without the device test it would have scored as one.
    const judgement = judgeCitation(question(), cited([citation({ device: 'DSBC', page: 47 })]));

    assert.equal(judgement.found, false);
  });

  it('counts an excerpt that quotes the recorded phrase, wherever it landed', () => {
    // A phrase can legitimately appear on a page the author did not think of first. The page is
    // where a reader would look; the phrase is the answer itself.
    const judgement = judgeCitation(
      question(),
      cited([citation({ page: 300, excerpt: 'configurable I/O can be set as safety-related' })])
    );

    assert.equal(judgement.found, true);
  });

  it('counts a silence on an answerable question as a miss, and says it was silent', () => {
    const judgement = judgeCitation(question(), { outcome: 'not-found', query: 'q', citations: [] });

    assert.equal(judgement.found, false);
    assert.equal(judgement.outcome, 'not-found');
  });

  it('counts staying silent on an unanswerable question as correct', () => {
    const judgement = judgeAbstention(
      { question: 'what is the capital of France', why: 'off domain' },
      { outcome: 'not-found', query: 'q', citations: [] }
    );

    assert.equal(judgement.abstained, true);
  });

  it('names what was quoted instead when it should have stayed silent', () => {
    // A rate nobody can look behind is a rate nobody can fix.
    const judgement = judgeAbstention(
      { question: 'KUKA KR 6 payload', why: 'not in the corpus' },
      cited([citation({ page: 47 })])
    );

    assert.equal(judgement.abstained, false);
    assert.deepEqual(judgement.citedInstead, ['UR5e p47']);
  });

  it('refuses to score a rate over no questions', () => {
    // A gate that opens because nothing was asked is the failure this whole file exists to prevent.
    assert.throws(() => evaluateRetrieval([], [abstained(true)]), /not a measurement/);
    assert.throws(() => evaluateRetrieval([found(true)], []), /not a measurement/);
  });

  it('stays shut when abstention fails, however good the precision', () => {
    // The brief's ordering, asserted: a retriever that finds everything and never stays silent is
    // the dangerous one. No averaging between the two rates would hide exactly that.
    const verdict = evaluateRetrieval(
      Array.from({ length: 10 }, () => found(true)),
      Array.from({ length: 10 }, (_unused, index) => abstained(index < 5))
    );

    assert.equal(verdict.citationPrecision, 1);
    assert.equal(verdict.open, false);
  });

  it('stays shut when precision fails, however good the abstention', () => {
    const verdict = evaluateRetrieval(
      Array.from({ length: 10 }, (_unused, index) => found(index < 5)),
      Array.from({ length: 10 }, () => abstained(true))
    );

    assert.equal(verdict.abstentionRate, 1);
    assert.equal(verdict.open, false);
  });

  it('opens only when both rates clear their own threshold', () => {
    const verdict = evaluateRetrieval(
      Array.from({ length: 10 }, (_unused, index) => found(index < 7)),
      Array.from({ length: 10 }, (_unused, index) => abstained(index < 9))
    );

    assert.equal(verdict.open, true);
  });

  it('asks for silence more strictly than for answers', () => {
    // The one thing about these constants that is not arbitrary, so it is pinned. If somebody ever
    // raises precision above abstention, the brief's central claim has been inverted by a diff.
    assert.ok(
      RequiredAbstentionRate > RequiredCitationPrecision,
      'staying silent well is what makes a retriever trustworthy; it must be the harder threshold'
    );
  });
});

function question(): AnswerableQuestion {
  return {
    question: 'UR5e safety I/O configurable inputs',
    device: 'UR5e',
    page: 47,
    contains: 'configurable I/O can be set as safety-related'
  };
}

function citation(overrides: Partial<Citation>): Citation {
  return {
    device: 'UR5e',
    title: 'Universal Robots e-Series User Manual UR5e',
    version: 'SW 5.16',
    page: 47,
    excerpt: 'the horizontal Digital Inputs block',
    score: 0.03,
    ...overrides
  };
}

function cited(citations: Citation[]): LookupResult {
  return { outcome: 'cited', query: 'q', citations };
}

function found(hit: boolean): CitationJudgement {
  return { question: 'q', found: hit, outcome: hit ? 'cited' : 'not-found', citedPages: [] };
}

function abstained(silent: boolean): AbstentionJudgement {
  return { question: 'q', abstained: silent, citedInstead: silent ? [] : ['UR5e p1'] };
}
