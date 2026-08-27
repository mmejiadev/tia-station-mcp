import type { SpecificationResult } from './loop.ts';
import { estimateCost } from './modelPricing.ts';
import type { TokenUsage } from './telemetry.ts';

/** One pass over the whole specification set, and how it went. */
export type Repetition = {
  /** Counting from one, as it is reported. */
  readonly index: number;
  readonly results: readonly SpecificationResult[];
  /**
   * What each generation of this repetition cost, one entry per attempt.
   *
   * @remarks
   * Empty for a stub run, which asked nothing of anybody. Empty is not the same as unknown here:
   * a run with no generations to pay for cost nothing, and the report says so rather than going
   * quiet.
   */
  readonly usage: readonly TokenUsage[];
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
  reportCost(repetitions);
}

/**
 * What the run cost, from the token counts the API reported.
 *
 * @remarks
 * `STATUS.md` closed phase 3 with a cost per generation inferred from token counts nobody had
 * counted, and said in as many words that the first thing to do with a key is measure it. This is
 * that. The tokens are measured; the dollars are those tokens times a published price read on one
 * day, which is why the line says so out loud instead of presenting a total as a fact.
 *
 * Per model, never summed across them, and the reason is the flag that made this possible: a run
 * asked to compare two models has two prices, and one total would hide which of them was spending.
 */
function reportCost(repetitions: readonly Repetition[]): void {
  const usage = repetitions.flatMap((repetition) => [...repetition.usage]);

  if (usage.length === 0) {
    return;
  }

  console.log('');

  for (const [model, entries] of groupByModel(usage)) {
    console.log(describeModelCost(model, entries));
  }

  console.log(
    'Token counts are what the API reported. The dollars are those counts at the list prices read ' +
      'on 2026-08-27, so check them against the invoice before quoting them.'
  );
}

/** The generations of one run, gathered by the model that produced them. */
function groupByModel(usage: readonly TokenUsage[]): Map<string, TokenUsage[]> {
  const byModel = new Map<string, TokenUsage[]>();

  for (const entry of usage) {
    const recorded = byModel.get(entry.model) ?? [];

    recorded.push(entry);
    byModel.set(entry.model, recorded);
  }

  return byModel;
}

/**
 * One model’s share of the bill, with the count it was averaged over.
 *
 * @remarks
 * A model whose price is not in the table reports its tokens and says the price is missing. The
 * alternative is a zero, and a zero is a number somebody would add up and believe.
 */
function describeModelCost(model: string, entries: readonly TokenUsage[]): string {
  const inputTokens = entries.reduce((total, entry) => total + entry.inputTokens, 0);
  const outputTokens = entries.reduce((total, entry) => total + entry.outputTokens, 0);
  const counted =
    `${model}: ${entries.length} generation(s), ` +
    `${inputTokens.toLocaleString()} tokens in and ${outputTokens.toLocaleString()} out`;

  const total = entries.reduce<number | undefined>(toTotalCost, 0);

  if (total === undefined) {
    return `${counted}, and no price on file for that model, so no cost is given`;
  }

  return `${counted}, about ${total.toFixed(4)} in total and ${(total / entries.length).toFixed(4)} per generation`;
}

/** Adds one generation to the bill, or gives up on the total the moment a price is missing. */
function toTotalCost(total: number | undefined, entry: TokenUsage): number | undefined {
  const cost = estimateCost(entry);

  return total === undefined || cost === undefined ? undefined : total + cost;
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
