import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { GenerationRequest } from '../src/generator.ts';
import { buildPrompt, createModelGenerator, extractScl, type SclRequest } from '../src/modelGenerator.ts';
import type { Specification } from '../src/specification.ts';

/**
 * The generator is a prompt, an answer, and the rules for turning one into the other. All three are
 * tested here without a network: what the API does with a well-formed request is Anthropic's
 * problem, and what this harness sends and accepts is this repository's.
 */
describe('model generator', () => {
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
    const generator = createModelGenerator(async (request) => {
      sent.push(request);

      return '```scl\nFUNCTION_BLOCK FB_Station\nEND_FUNCTION_BLOCK\n```';
    }, 'a-model');

    const source = await generator.generate(requestFor(1, []));

    assert.equal(sent.length, 1);
    assert.ok((sent[0]?.system ?? '').includes('TIA Portal V20'), 'the system prompt did not reach the sender');
    assert.match(source, /FUNCTION_BLOCK FB_Station/);
  });

  it('is named after the model, because that is what a measurement is attributable to', () => {
    const generator = createModelGenerator(async () => 'FUNCTION_BLOCK FB\nEND_FUNCTION_BLOCK', 'claude-opus-5');

    assert.equal(generator.name, 'claude-opus-5');
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
