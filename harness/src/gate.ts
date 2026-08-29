import { isKnownOutcome, type AuditReadResult } from './auditTrail.ts';
import type { RunStatistics } from './telemetry.ts';

/**
 * How many complete runs the first criterion asks for.
 *
 * @remarks
 * Fifty, fixed in the roadmap "in the cold" — before anybody wanted the door open. It is not
 * adjustable from the command line for exactly that reason.
 */
const RequiredCompleteRuns = 50;

/** How many recent runs the stability criterion looks at. */
const StabilityWindow = 20;

/**
 * How far the clean-compilation rate may fall between the two halves of the window.
 *
 * @remarks
 * Ten percentage points, and the number is a judgement rather than a measurement — the roadmap says
 * "a stable rate" and stability has to be given an arithmetic meaning before it can be evaluated.
 * It is stated here, in one place, so that arguing with it means changing a documented constant
 * rather than reinterpreting a sentence.
 *
 * Only a *fall* fails the criterion. A rate that improves is not instability worth blocking on.
 */
const StabilityToleranceRatio = 0.1;

/**
 * The mode every recorded run has to have been in.
 *
 * @remarks
 * The gate exists to decide whether Workshop Mode may be enabled, so evidence gathered in it would
 * be the conclusion smuggled into the premise.
 */
const RequiredMode = 'Study';

/** Everything the gate reasons about, gathered by the caller so the evaluation stays pure. */
export type GateEvidence = {
  /** Every run in the store, newest last. */
  readonly runs: readonly RunStatistics[];
  /** Iterations recorded with no outcome at all. */
  readonly unfinishedIterations: number;
  readonly audit: AuditReadResult;
  /** Whether a backup path recorded in the audit is actually on disk. */
  readonly backupExists: (path: string) => boolean;
  /** The in-person review, or undefined when none has been recorded. */
  readonly review: ReviewRecord | undefined;
};

/** A design review that happened in a room, with a person, on a date. */
export type ReviewRecord = {
  readonly date: string;
  readonly reviewer: string;
};

/** One criterion and whether the data says it is met. */
export type Criterion = {
  readonly number: number;
  readonly name: string;
  readonly met: boolean;
  /** The numbers behind the verdict, so a "no" says what would change it. */
  readonly evidence: string;
};

/** The gate's answer. */
export type GateVerdict = {
  readonly open: boolean;
  readonly criteria: readonly Criterion[];
};

/**
 * Answers whether Workshop Mode may be enabled.
 *
 * @param evidence What was recorded. Gathering it is the caller's job; judging it is this one's.
 * @returns Every criterion with its verdict, and whether all five are met.
 * @remarks
 * Every criterion is evaluated, including when an earlier one has already failed. Stopping at the
 * first "no" would answer the question — the door stays shut — while hiding how far the rest are
 * from being met, and the point of the gate is to say what is missing, not merely to refuse.
 *
 * All five must be met. There is no majority, no weighting and no override: a gate that can be
 * argued past is a gate in name only.
 */
export function evaluateGate(evidence: GateEvidence): GateVerdict {
  const criteria = [
    completeRuns(evidence),
    noSilentFailures(evidence),
    completeAudit(evidence),
    stableCleanCompilationRate(evidence),
    inPersonReview(evidence)
  ];

  return { open: criteria.every((criterion) => criterion.met), criteria };
}

/** Criterion 1: at least fifty complete loop runs, all of them in Study Mode. */
function completeRuns(evidence: GateEvidence): Criterion {
  const complete = evidence.runs.filter((run) => run.outcome !== undefined).length;
  const foreignModes = new Set(
    evidence.audit.entries.filter((entry) => entry.mode !== RequiredMode).map((entry) => entry.mode)
  );

  if (foreignModes.size > 0) {
    return {
      number: 1,
      name: `${RequiredCompleteRuns} complete loop runs in Study Mode`,
      met: false,
      evidence: `the audit trail records operations in ${[...foreignModes].join(', ')}, not only ${RequiredMode}`
    };
  }

  return {
    number: 1,
    name: `${RequiredCompleteRuns} complete loop runs in Study Mode`,
    met: complete >= RequiredCompleteRuns,
    evidence: `${complete} complete run(s) of ${RequiredCompleteRuns} required`
  };
}

/**
 * Criterion 2: nothing ended in an unknown state.
 *
 * @remarks
 * Three ways to fail, and the third is the one that matters: an outcome this harness does not
 * recognise counts as unknown rather than as something new that is probably fine. A criterion that
 * ignored what it could not classify would be met by a trail full of records nobody can read.
 */
function noSilentFailures(evidence: GateEvidence): Criterion {
  const unknownOutcomes = evidence.audit.entries.filter((entry) => !isKnownOutcome(entry.outcome));
  const unreadable = evidence.audit.unreadableLines.length;
  const met = unknownOutcomes.length === 0 && unreadable === 0 && evidence.unfinishedIterations === 0;

  return {
    number: 2,
    name: 'zero silent failures',
    met,
    evidence:
      `${evidence.unfinishedIterations} iteration(s) with no outcome, ` +
      `${unknownOutcomes.length} audit entr(ies) with an unrecognised outcome, ` +
      `${unreadable} unreadable audit line(s)`
  };
}

/**
 * Criterion 3: every write that saved previous state can still be found on disk.
 *
 * @remarks
 * Only entries that were actually applied are checked. A refused change never exported anything, so
 * demanding a backup for it would make the criterion permanently unmeetable by working correctly.
 */
function completeAudit(evidence: GateEvidence): Criterion {
  const applied = evidence.audit.entries.filter(
    (entry) => entry.outcome === 'Applied' && entry.backupPath.length > 0
  );
  const missing = applied.filter((entry) => !evidence.backupExists(entry.backupPath));

  return {
    number: 3,
    name: 'complete audit: every recorded backup is on disk',
    met: missing.length === 0,
    evidence:
      missing.length === 0
        ? `${applied.length} recorded backup(s), all present`
        : `${missing.length} of ${applied.length} recorded backup(s) are gone, first: ${missing[0]?.backupPath}`
  };
}

/**
 * Criterion 4: the clean-compilation rate is not falling across the last twenty runs.
 *
 * @remarks
 * Compared as two halves rather than as a trend line: ten runs against the ten before them is
 * something a person can check by hand from the same table, and a regression line over twenty
 * points would be a statistic nobody could argue with without re-deriving it.
 *
 * The comparison assumes the twenty runs came from one generator. When they did not, it declines to
 * produce a number at all — see {@link mixedGenerators}.
 */
function stableCleanCompilationRate(evidence: GateEvidence): Criterion {
  const name = `a stable clean-compilation rate across the last ${StabilityWindow} runs`;
  const window = evidence.runs.slice(-StabilityWindow);

  if (window.length < StabilityWindow) {
    return {
      number: 4,
      name,
      met: false,
      evidence: `${window.length} run(s) recorded, and the rate is judged over ${StabilityWindow}`
    };
  }

  const generators = [...new Set(window.map((run) => run.generator))].sort();

  if (generators.length > 1) {
    return mixedGenerators(name, generators);
  }

  const half = StabilityWindow / 2;
  const earlier = cleanCompilationRate(window.slice(0, half));
  const later = cleanCompilationRate(window.slice(half));

  return {
    number: 4,
    name,
    met: later >= earlier - StabilityToleranceRatio,
    evidence:
      `${percentage(earlier)} over runs 1-${half} of the window, ${percentage(later)} over runs ` +
      `${half + 1}-${StabilityWindow}, tolerated fall ${percentage(StabilityToleranceRatio)}`
  };
}

/**
 * Criterion 4 when the window spans more than one generator: no verdict, and the door stays shut.
 *
 * @param name The criterion's name, so the refusal is reported under the criterion it belongs to.
 * @param generators What produced the SCL across the window, named so the reader can see the mix.
 * @returns The criterion, unmet, saying why no rate was computed.
 * @remarks
 * A pattern expander and a model do not compile at the same rate, so a window holding both compares
 * two experiments and reports the difference between them as a fall within one. That number
 * describes neither, and a gate that decides on it decides on nothing.
 *
 * Unmet rather than skipped, deliberately: the criterion has not been satisfied, and "cannot be
 * judged" is a reason to keep the door shut, never a reason to stop counting it. It becomes
 * judgeable again as soon as {@link StabilityWindow} consecutive runs share a generator — which is
 * the run that has to happen, not a constant that has to be argued with.
 */
function mixedGenerators(name: string, generators: readonly string[]): Criterion {
  return {
    number: 4,
    name,
    met: false,
    evidence:
      `the last ${StabilityWindow} runs span ${generators.length} generators ` +
      `(${generators.join(', ')}), and a rate across them describes none of them`
  };
}

/** Criterion 5: a person reviewed the design in the room. */
function inPersonReview(evidence: GateEvidence): Criterion {
  const { review } = evidence;

  return {
    number: 5,
    name: 'an in-person design review with the supervising teacher',
    met: review !== undefined,
    evidence:
      review === undefined
        ? 'no review recorded, and no measurement can stand in for one'
        : `reviewed on ${review.date} by ${review.reviewer}`
  };
}

/**
 * What fraction of the specifications attempted in these runs reached a clean compilation.
 *
 * @remarks
 * Weighted by specification rather than by run, so a run of six cases counts for six. Runs that
 * attempted nothing contribute nothing instead of counting as a perfect score.
 */
function cleanCompilationRate(runs: readonly RunStatistics[]): number {
  const attempted = runs.reduce((total, run) => total + run.specifications, 0);

  if (attempted === 0) {
    return 0;
  }

  return runs.reduce((total, run) => total + run.cleanCompilations, 0) / attempted;
}

function percentage(ratio: number): string {
  return `${(ratio * 100).toFixed(0)}%`;
}
