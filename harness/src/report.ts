import type { SpecificationResult } from './loop.ts';

/** One pass over the whole specification set, and how it went. */
export type Repetition = {
  /** Counting from one, as it is reported. */
  readonly index: number;
  readonly results: readonly SpecificationResult[];
};

/** What the report needs to know about how the run was asked for. */
export type ReportContext = {
  readonly repetitions: number;
};

/** Whether every specification of one repetition passed. */
export function passedEverySpecification(repetition: Repetition): boolean {
  return repetition.results.every((result) => result.outcome === 'passed');
}

/**
 * Prints what happened, with the sample size attached.
 *
 * @remarks
 * Never a bare percentage: the roadmap says so, and it is right. "5 of 6 passed, n=6 (6
 * specifications x 1 repetition)" is a sentence somebody can argue with, and "83%" is not - it
 * hides whether it was five out of six or fifty out of sixty.
 */
export function report(repetitions: readonly Repetition[], context: ReportContext): void {
  console.log('');

  for (const repetition of repetitions) {
    reportOne(repetition, repetitions.length);
  }

  reportRates(repetitions, context);
}

/** One repetition's specifications, one line each. */
function reportOne(repetition: Repetition, total: number): void {
  if (total > 1) {
    console.log(`--- repetition ${repetition.index}`);
  }

  for (const result of repetition.results) {
    const detail = result.detail.length > 0 ? ` — ${result.detail}` : '';

    console.log(
      `${result.outcome === 'passed' ? 'PASS' : 'FAIL'}  ${result.specification}  ` +
        `(${result.attempts} attempt(s), ${result.outcome})${detail}`
    );
  }
}

/** The rates the phase exists to produce, per specification and overall. */
function reportRates(repetitions: readonly Repetition[], context: ReportContext): void {
  const attempts = new Map<string, number[]>();

  for (const repetition of repetitions) {
    for (const result of repetition.results) {
      const recorded = attempts.get(result.specification) ?? [];

      recorded.push(result.outcome === 'passed' ? result.attempts : 0);
      attempts.set(result.specification, recorded);
    }
  }

  console.log('');

  for (const [specification, outcomes] of attempts) {
    console.log(`${specification}: ${describeSpecification(outcomes)}`);
  }

  const cases = repetitions.reduce((total, repetition) => total + repetition.results.length, 0);
  const passed = repetitions.reduce(
    (total, repetition) => total + repetition.results.filter((result) => result.outcome === 'passed').length,
    0
  );

  console.log('');
  const perRepetition = repetitions[0]?.results.length ?? 0;

  console.log(
    `${passed} of ${cases} specification run(s) passed on a simulated CPU, ` +
      `n=${cases} (${context.repetitions} repetition(s) of ${perRepetition} specification(s)).`
  );
}

/**
 * How one specification did across the repetitions.
 *
 * @remarks
 * The mean counts only the passing runs, and says how many it averaged over. Averaging in the ones
 * that never got there would report a number of attempts for work that was never finished, which
 * reads as though it took fewer iterations the worse it went.
 */
function describeSpecification(outcomes: readonly number[]): string {
  const passes = outcomes.filter((attempts) => attempts > 0);
  const rate = `passed ${passes.length} of ${outcomes.length}`;

  if (passes.length === 0) {
    return `${rate}, so there is no iteration count to average`;
  }

  const mean = passes.reduce((total, attempts) => total + attempts, 0) / passes.length;

  return `${rate}, ${mean.toFixed(1)} iteration(s) on average over the ${passes.length} that passed`;
}
