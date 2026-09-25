import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatCondition } from '../../src/grafcet/conditionText.ts';
import type { Grafcet } from '../../src/grafcet/grafcetModel.ts';
import { ladToGrafcet } from '../../src/grafcet/ladToGrafcet.ts';
import { readS7dcl } from '../../src/grafcet/s7dclReader.ts';
import { FaultyFeeder, Feeder, Init, Outfeed, Outputs, Transfer } from './ladFixtures.ts';

function chart(...documents: string[]): ReturnType<typeof ladToGrafcet> {
  return ladToGrafcet(documents.map(readS7dcl), 'test');
}

function transitions(grafcet: Grafcet, sequence: string): string[] {
  const found = grafcet.sequences.find((candidate) => candidate.name === sequence);

  return (found?.transitions ?? []).map((t) => `${t.from.join(',')} -> ${t.to.join(',')} : ${formatCondition(t.condition)}`);
}

describe('recovering a GRAFCET from set/reset LAD', () => {
  it('recovers each transition from its S and R coils', () => {
    const { grafcet, findings } = chart(Feeder, Init);

    assert.deepEqual(transitions(grafcet, 'FEEDER'), ['0 -> 1 : START', '1 -> 2 : PART', '2 -> 3 : PART.HOME', '3 -> 0 : /PART']);
    assert.deepEqual(findings, []);
  });

  it('takes initial steps from what the first scan sets, in every sequence it initialises', () => {
    const { grafcet } = chart(Feeder, Init);

    const initial = grafcet.sequences.flatMap((sequence) => sequence.steps.filter((step) => step.initial).map((step) => step.number));

    assert.deepEqual(initial, [0, 30, 50]);
  });

  it('keeps a step of another sequence in the condition, since that sequence does not reset it', () => {
    const { grafcet } = chart(Transfer, Outfeed);

    assert.deepEqual(transitions(grafcet, 'OUTFEED'), ['50 -> 51 : X33', '51 -> 50 : /EXIT']);
  });

  it('names an on-delay after its step and turns it into an action', () => {
    const { grafcet } = chart(Transfer);
    const transfer = grafcet.sequences.find((sequence) => sequence.name === 'TRANSFER')!;

    assert.equal(transitions(grafcet, 'TRANSFER')[1], '31 -> 32 : T31');
    assert.deepEqual(transfer.steps.find((step) => step.number === 31)?.actions, [{ kind: 'timer', name: 'T31', seconds: 30 }]);
  });

  it('turns an OR of steps on a plain coil into one action per step', () => {
    const { grafcet } = chart(Feeder, Outputs);
    const feeder = grafcet.sequences.find((sequence) => sequence.name === 'FEEDER')!;

    const driven = feeder.steps.filter((step) => step.actions.some((action) => action.kind === 'output' && action.name === 'MOTOR')).map((step) => step.number);

    assert.deepEqual(driven, [1, 3]);
  });

  it('reports a step driven by a plain coil, the fault that splits a sequence in two', () => {
    const { findings } = chart(FaultyFeeder);

    const plain = findings.filter((finding) => finding.message.includes('plain coil ( )'));

    assert.deepEqual(plain.map((finding) => finding.network), [3]);
  });

  it('reports a set with no step reset alongside it', () => {
    const { findings } = chart(FaultyFeeder);

    assert.ok(findings.some((finding) => finding.network === 3 && finding.message.includes('has no source step')));
  });

  it('reports the second plain coil on the same output', () => {
    const doubled = Outputs.replace('END_FUNCTION', '    NETWORK\n        RUNG wire#powerrail\n            Contact( "X2" )\n            Coil( "MOTOR" )\n        END_RUNG\n    END_NETWORK\nEND_FUNCTION');

    const { findings } = chart(doubled);

    assert.equal(findings.length, 1);
    assert.match(findings[0]!.message, /MOTOR has a second plain coil/);
  });
});
