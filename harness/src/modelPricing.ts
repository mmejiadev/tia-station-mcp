import type { TokenUsage } from './telemetry.ts';

/** What a model costs per million tokens, in US dollars. */
type ModelPrice = {
  readonly input: number;
  readonly output: number;
};

/**
 * The published list prices, in US dollars per million tokens.
 *
 * @remarks
 * **Dated on purpose: read from Anthropic's model table on 2026-08-27.** Prices change, and a cost
 * computed from a stale table is wrong in a way nothing complains about — so this is the one number
 * here that is not measured, and it is kept in one place where it can be checked against the
 * invoice rather than scattered through the report.
 *
 * The token counts it multiplies *are* measured: they come back on every response, and they are what
 * the store records. Recomputing the cost from a corrected table therefore fixes every run already
 * recorded, which is why no cost is written into the store.
 */
const Prices: Readonly<Record<string, ModelPrice>> = {
  'claude-opus-5': { input: 5, output: 25 },
  'claude-sonnet-5': { input: 2, output: 10 },
  'claude-haiku-4-5': { input: 1, output: 5 }
};

/**
 * What a cached token costs relative to a fresh input token.
 *
 * @remarks
 * Writing a token into the cache costs more than sending it once; reading it back costs almost
 * nothing. The harness does not cache today, so both multipliers apply to zero — they are here so
 * that the day it does, the reported cost does not quietly stay wrong.
 */
const CacheWriteMultiplier = 1.25;
const CacheReadMultiplier = 0.1;

const TokensPerMillion = 1_000_000;

/**
 * A snapshot suffix: the release date the API appends to an alias when it resolves one.
 *
 * @remarks
 * Asking for `claude-haiku-4-5` gets an answer from `claude-haiku-4-5-20251001`, and the answer is
 * what the store records - correctly, because a price belongs to the snapshot that served the
 * request, not to whatever the alias points at today. The table is keyed by alias, so the lookup
 * has to undo exactly that one transformation and nothing else.
 */
const SnapshotSuffix = /-\d{8}$/;

/**
 * The price table's key for a model the API named, or the name itself when it is already a key.
 *
 * @remarks
 * Only an exact eight-digit date is removed. Anything else stays whole and therefore stays unpriced,
 * because a lookup that trims until it finds a match would price an unknown model at a known model's
 * rate - which is the failure this whole file is written to avoid.
 */
function priceKey(model: string): string {
  if (Prices[model] !== undefined) {
    return model;
  }

  return model.replace(SnapshotSuffix, '');
}

/**
 * What one recorded usage cost, or nothing when the model's price is not known.
 *
 * @param usage Measured token counts.
 * @returns The cost in US dollars, or `undefined` for a model absent from the table.
 * @remarks
 * An unknown model returns `undefined` rather than zero. Zero is a number a report would add up and
 * present as a total, and "this run cost $0.00" is a far worse answer than "the price of
 * claude-whatever is not in the table".
 */
export function estimateCost(usage: TokenUsage): number | undefined {
  const price = Prices[priceKey(usage.model)];

  if (price === undefined) {
    return undefined;
  }

  const input =
    usage.inputTokens +
    usage.cacheCreationTokens * CacheWriteMultiplier +
    usage.cacheReadTokens * CacheReadMultiplier;

  return (input * price.input + usage.outputTokens * price.output) / TokensPerMillion;
}

/** Whether a cost can be given for this model at all, so a caller can say so instead of guessing. */
export function hasPrice(model: string): boolean {
  return Prices[priceKey(model)] !== undefined;
}
