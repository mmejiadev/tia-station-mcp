import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatCondition } from '../../src/grafcet/conditionText.ts';
import type { Grafcet } from '../../src/grafcet/grafcetModel.ts';
import { ladToGrafcet } from '../../src/grafcet/ladToGrafcet.ts';
import { readS7dcl } from '../../src/grafcet/s7dclReader.ts';
import { FaultyFeeder, fc, Feeder, Init, Outfeed, Outputs, Transfer } from './ladFixtures.ts';

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

  it('reports one edge memory bit used by contacts in two blocks, which see a single edge between them', () => {
    // What the class exercise of 2026-10-08 compiled without a word: P contacts sharing MARXA_FP1.
    const edge = (block: string, step: string, target: string): string => fc(block, [`        RUNG wire#powerrail\n            Contact( "${step}" )\n            P_Contact( operand := "MARXA", bit := "MARXA_FP1" )\n            wire#w1\n            S_Coil( "${target}" )\n        END_RUNG\n        RUNG wire#w1\n            R_Coil( "${step}" )\n        END_RUNG\n`]);

    const { findings } = chart(edge('S', 'X21', 'X22'), edge('MASTER', 'X10', 'X11'));

    assert.deepEqual(findings.map((finding) => [finding.severity, finding.block]), [['error', 'MASTER']]);
    assert.match(findings[0]!.message, /MARXA_FP1 is already used by an edge contact in S network 1/);
  });

  it('reports a coil on an edge memory bit, which corrupts the edge it keeps', () => {
    const document = fc('S', [
      '        RUNG wire#powerrail\n            Contact( "X0" )\n            P_Contact( operand := "A", bit := "A_FP1" )\n            wire#w1\n            S_Coil( "X1" )\n        END_RUNG\n        RUNG wire#w1\n            R_Coil( "X0" )\n        END_RUNG\n',
      '        RUNG wire#powerrail\n            Contact( "X1" )\n            Coil( "A_FP1" )\n        END_RUNG\n',
    ]);

    const { findings } = chart(document);

    assert.ok(findings.some((finding) => finding.severity === 'error' && /A_FP1 is an edge memory bit and is also written/.test(finding.message)));
  });
});
