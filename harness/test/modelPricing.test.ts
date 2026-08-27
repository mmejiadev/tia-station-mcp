import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { estimateCost, hasPrice } from '../src/modelPricing.ts';
import type { TokenUsage } from '../src/telemetry.ts';

/**
 * The one place in the harness where a number is not measured. Everything else here comes back from
 * the API or from the store; a price comes from a table read on a day, so these tests are about the
 * arithmetic and about what happens when the table has nothing to say.
 */
describe('model pricing', () => {
  it('prices a generation at the published rate', () => {
    // A million in and a million out of Opus 5: $5 plus $25.
    const cost = estimateCost(usage('claude-opus-5', { inputTokens: 1_000_000, outputTokens: 1_000_000 }));

    assert.equal(cost, 30);
  });

  it('prices the cheaper model lower, which is the whole reason --model exists', () => {
    const request = { inputTokens: 40_000, outputTokens: 8_000 };
    const opus = estimateCost(usage('claude-opus-5', request));
    const haiku = estimateCost(usage('claude-haiku-4-5', request));

    assert.ok(opus !== undefined && haiku !== undefined);
    assert.equal(opus / haiku, 5);
  });

  it('charges a cached token far less than a fresh one', () => {
    const cost = estimateCost(usage('claude-haiku-4-5', { inputTokens: 0, cacheReadTokens: 1_000_000 }));

    assert.equal(cost, 0.1);
  });

  it('prices the dated snapshot the API answers from, not just the alias that was asked for', () => {
    // Measured on 2026-08-27: --model claude-haiku-4-5 was answered by claude-haiku-4-5-20251001,
    // and that resolved id is what the store records. Keyed by alias alone, every real generation
    // reported 'no price on file' - the cost report worked only for models nothing had ever run.
    const cost = estimateCost(usage('claude-haiku-4-5-20251001', { inputTokens: 1_000_000 }));

    assert.equal(cost, 1);
    assert.equal(hasPrice('claude-haiku-4-5-20251001'), true);
  });

  it('does not trim its way to a match, which would price an unknown model at a known rate', () => {
    // Neither of these is claude-haiku-4-5, and neither may borrow its price.
    assert.equal(hasPrice('claude-haiku-4-5-turbo'), false);
    assert.equal(hasPrice('claude-haiku-9-9-20991231'), false);
  });

  it('gives no cost for a model it has no price for, rather than a zero somebody would total up', () => {
    // Zero is the dangerous answer: it adds up, it prints, and it reads as "this run was free".
    assert.equal(estimateCost(usage('claude-from-next-year', { inputTokens: 500_000 })), undefined);
    assert.equal(hasPrice('claude-from-next-year'), false);
  });
});

function usage(model: string, counts: Partial<Omit<TokenUsage, 'model'>>): TokenUsage {
  return {
    model,
    inputTokens: 0,
    outputTokens: 0,
    cacheCreationTokens: 0,
    cacheReadTokens: 0,
    ...counts
  };
}
