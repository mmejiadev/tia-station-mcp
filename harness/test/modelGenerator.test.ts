import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { UnusableGeneration, type GenerationRequest } from '../src/generator.ts';
import {
  buildPrompt,
  createModelGenerator,
  extractScl,
  requireApiKey,
  thinkingFor,
  type MessageSender,
  type SclRequest
} from '../src/modelGenerator.ts';
import type { Specification } from '../src/specification.ts';

/**
 * The generator is a prompt, an answer, and the rules for turning one into the other. All three are
 * tested here without a network: what the API does with a well-formed request is Anthropic's
 * problem, and what this harness sends and accepts is this repository's.
 */
describe('model generator', () => {
  it('carries the cost out of an answer it cannot use', () => {
    // The defect this pins cost real money and hid it: an answer with no SCL threw a plain error,
    // the usage went with it, and the store recorded the attempt as free. On 2026-08-28 that meant
    // 46 generations run and 31 costs recorded — understated by exactly the failures.
    const generator = createModelGenerator(answering('   \n  '), 'a-model');

    return assert.rejects(
      () => generator.generate(requestFor(1, [])),
      (failure: unknown) => {
        assert.ok(failure instanceof UnusableGeneration);
        assert.equal(failure.usage?.outputTokens, 3400);

        return true;
      }
    );
  });

  it('says why the answer was empty, so the next unexplained sample is not paid for twice', () => {
    const generator = createModelGenerator(answeringWith('', 'max_tokens', ['thinking']), 'a-model');

    return assert.rejects(
      () => generator.generate(requestFor(1, [])),
      /Stop reason: max_tokens\. Blocks: thinking\. Text: 0 character\(s\)\./
    );
  });
  it('asks for the cell specification and the tags the check will read', () => {
    const prompt = buildPrompt(requestFor(1, []));

    assert.match(prompt, /TwoStationDemo/, 'the cell specification itself is not in the prompt');
    assert.match(prompt, /DB_TwoStationDemo\.CompletedPieceId/, 'the observable tags are not in the prompt');
  });

  it('puts the compiler errors, unedited, into the next attempt', () => {
    // Unedited because the line numbers and block names are the part that makes a fix possible.
    const errors = ['Error: PLC_0/Program blocks/Main (OB1)/4 — Tag #Missing not defined.'];

    const prompt = buildPrompt(requestFor(2, errors));

    assert.match(prompt, /Attempt 2/);
    assert.ok(prompt.includes(errors[0] ?? ''), 'the compiler error was not passed on verbatim');
  });

  it('says nothing about errors on the first attempt', () => {
    const prompt = buildPrompt(requestFor(1, []));

    assert.doesNotMatch(prompt, /did not compile/);
  });

  it('takes the SCL out of a fenced answer', () => {
    // The system prompt asks for bare SCL; a model that fences it anyway has still answered.
    const answer = 'Here you go:\n```scl\nFUNCTION_BLOCK FB_Station\nEND_FUNCTION_BLOCK\n```\n';

    assert.equal(extractScl(answer), 'FUNCTION_BLOCK FB_Station\nEND_FUNCTION_BLOCK');
  });

  it('takes an unfenced answer as it is', () => {
    assert.equal(extractScl('FUNCTION_BLOCK FB_Station\nEND_FUNCTION_BLOCK\n'), 'FUNCTION_BLOCK FB_Station\nEND_FUNCTION_BLOCK');
  });

  it('refuses an empty answer rather than writing nothing into a project', () => {
    // Written through, it would arrive as "0 blocks generated", which reads like a server fault.
    assert.throws(() => extractScl('   \n  '), /no SCL/);
  });

  it('sends one request per generation and returns what came back', async () => {
    const sent: SclRequest[] = [];
    const generator = createModelGenerator(answering('```scl\nFUNCTION_BLOCK FB_Station\nEND_FUNCTION_BLOCK\n```', sent), 'a-model');

    const generated = await generator.generate(requestFor(1, []));

    assert.equal(sent.length, 1);
    assert.ok((sent[0]?.system ?? '').includes('TIA Portal V20'), 'the system prompt did not reach the sender');
    assert.match(generated.source, /FUNCTION_BLOCK FB_Station/);
  });

  it('carries back what the generation cost, so the run does not have to estimate it', async () => {
    // The reason the seam returns an answer rather than a string. Phase 3 closed quoting a cost per
    // generation inferred from token counts nobody had counted; these are the counted ones.
    const generator = createModelGenerator(answering('FUNCTION_BLOCK FB\nEND_FUNCTION_BLOCK'), 'a-model');

    const generated = await generator.generate(requestFor(1, []));

    assert.deepEqual(generated.usage, {
      model: 'a-model',
      inputTokens: 1200,
      outputTokens: 3400,
      cacheCreationTokens: 0,
      cacheReadTokens: 0
    });
  });

  it('reports the cost of an answer the compiler will reject, because it was paid for too', async () => {
    // A cost that counted only the attempts that compiled would understate the bill by exactly the
    // attempts worth looking at - the ones a second iteration had to be spent fixing.
    const generator = createModelGenerator(answering('THIS IS NOT SCL AT ALL'), 'a-model');

    const generated = await generator.generate(requestFor(1, []));

    assert.equal(generated.usage?.outputTokens, 3400);
  });

  it('is named after the model, because that is what a measurement is attributable to', () => {
    const generator = createModelGenerator(answering('FUNCTION_BLOCK FB\nEND_FUNCTION_BLOCK'), 'claude-opus-5');

    assert.equal(generator.name, 'claude-opus-5');
  });

  it('asks for adaptive thinking only where the API accepts it', () => {
    // The parameter is a 400 on anything older than the 4.6 generation, and --model made those
    // reachable. A run that died on it would do so a minute in, with TIA Portal already open, and
    // would read as though the model name were the problem.
    assert.deepEqual(thinkingFor('claude-opus-5'), { thinking: { type: 'adaptive' } });
    assert.deepEqual(thinkingFor('claude-haiku-4-5'), {});
  });

  it('sends no thinking parameter for a model it has never heard of', () => {
    // Omitting it is valid everywhere; guessing is a request that cannot succeed. A model released
    // after this file was written should generate, not fail.
    assert.deepEqual(thinkingFor('claude-from-next-year'), {});
  });

  it('lets a failure from the sender reach the loop', async () => {
    // The loop records it as an attempt that failed. Swallowing it here would report a run that
    // generated nothing as one that generated something wrong.
    const generator = createModelGenerator(async () => {
      throw new Error('the API said no');
    }, 'a-model');

    await assert.rejects(() => generator.generate(requestFor(1, [])), /the API said no/);
  });
});

/**
 * A sender that answers with a fixed text and a fixed bill.
 *
 * @param text What the model is to have said.
 * @param sent Where to record the requests, when a test wants to look at them.
 * @remarks
 * The token counts are invented, and they are meant to be: what this asserts is that whatever the
 * API reported reaches the store unchanged, which is a different question from whether the API
 * counts correctly. Two distinct values so that a transposition of input and output would fail.
 */
function answering(text: string, sent?: SclRequest[]): MessageSender {
  return async (request: SclRequest) => {
    sent?.push(request);

    return {
      text,
      usage: { model: 'a-model', inputTokens: 1200, outputTokens: 3400, cacheCreationTokens: 0, cacheReadTokens: 0 }
    };
  };
}

/** The same double, plus the two diagnostics a real answer carries. */
function answeringWith(text: string, stopReason: string, blocks: readonly string[]): MessageSender {
  return async () => ({
    text,
    usage: { model: 'a-model', inputTokens: 1200, outputTokens: 3400, cacheCreationTokens: 0, cacheReadTokens: 0 },
    stopReason,
    blocks
  });
}

function requestFor(attempt: number, previousErrors: readonly string[]): GenerationRequest {
  return { specification: specification(), attempt, previousErrors };
}

function specification(): Specification {
  return {
    name: 'two-station-runs',
    goal: 'A two-station cell that admits a numbered piece and reports it completed.',
    cellPath: 'spec/cells/two-station-demo.json',
    softwarePath: 'PLC_0',
    breakFirstAttempt: false,
    controller: { name: 'C', address: '192.168.0.1', subnetMask: '255.255.255.0', cpuType: 'CPU1511' },
    acceptance: [
      { action: 'write', tag: 'DB_TwoStationDemo.Enable', value: 'true' },
      { action: 'waitFor', tag: 'DB_TwoStationDemo.CompletedPieceId', equals: '17', timeoutMilliseconds: 20000 }
    ]
  };
}

/**
 * The key is the one thing this generator needs that no test can supply.
 *
 * @remarks
 * These are about *when* the absence is noticed, not about the API. A run asked for `--generator
 * model` reads the key before it opens TIA Portal, because forty-five seconds of startup followed by
 * "there is no key" is the shape of a mistake that gets made twice.
 */
describe('the API key a model run needs', () => {
  it('refuses when it is not set, and names what to set', () => {
    withKey(undefined, () => {
      assert.throws(requireApiKey, /ANTHROPIC_API_KEY is not set/);
    });
  });

  it('refuses an empty one rather than failing later as an authentication error', () => {
    // `ANTHROPIC_API_KEY=` in a profile sets the variable to nothing. A client built on that fails
    // much later, blaming the key rather than saying there isn't one — and this is exactly the case
    // that looks, from the outside, as though the key *is* set.
    withKey('   ', () => {
      assert.throws(requireApiKey, /ANTHROPIC_API_KEY is not set/);
    });
  });

  it('is satisfied by a key that is there', () => {
    withKey('sk-ant-not-a-real-key', () => {
      assert.doesNotThrow(requireApiKey);
    });
  });
});

/**
 * Runs one check with the environment variable set to a given value, and puts it back afterwards.
 *
 * @remarks
 * Restored in a finally block, and that matters more than it looks: these tests share a process with
 * every other test in the suite, and one that left the variable behind would decide the outcome of a
 * later test somewhere else in the file tree.
 */
function withKey(value: string | undefined, check: () => void): void {
  const original = process.env['ANTHROPIC_API_KEY'];

  if (value === undefined) {
    delete process.env['ANTHROPIC_API_KEY'];
  } else {
    process.env['ANTHROPIC_API_KEY'] = value;
  }

  try {
    check();
  } finally {
    if (original === undefined) {
      delete process.env['ANTHROPIC_API_KEY'];
    } else {
      process.env['ANTHROPIC_API_KEY'] = original;
    }
  }
}
