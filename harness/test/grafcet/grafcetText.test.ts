import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatGrafcet, parseGrafcet } from '../../src/grafcet/grafcetText.ts';
import type { Grafcet } from '../../src/grafcet/grafcetModel.ts';

const Cell = `title Test cell
sequence Feeder
step 0 initial
step 1 : MOTOR
step 2 : timer T2 5s, LAMP if /DOOR, set ALARM
0 -> 1 : START./STOP
1 -> 2 : PART
2 -> 0 : T2
1 -> 0 : /START   # a second way out of step 1
`;

function read(text: string): Grafcet {
  const result = parseGrafcet(text);
  assert.ok(result.ok, result.ok ? '' : result.errors.join('; '));

  return result.grafcet;
}

describe('chart text notation', () => {
  it('reads steps, actions and transitions', () => {
    const grafcet = read(Cell);
    const feeder = grafcet.sequences[0]!;

    assert.equal(grafcet.title, 'Test cell');
    assert.deepEqual(feeder.steps.map((step) => step.number), [0, 1, 2]);
    assert.equal(feeder.steps[0]!.initial, true);
    assert.deepEqual(feeder.steps[2]!.actions.map((action) => action.kind), ['timer', 'output', 'set']);
    assert.equal(feeder.transitions.length, 4);
  });

  it('writes a chart that reads back identically', () => {
    const once = read(Cell);

    const twice = read(formatGrafcet(once));

    assert.deepEqual(twice, once);
  });

  it('creates an undeclared step that a transition names, with no actions', () => {
    const grafcet = read('sequence S\nstep 0 initial\n0 -> 7 : A\n7 -> 0 : B\n');

    const step = grafcet.sequences[0]!.steps.find((candidate) => candidate.number === 7);

    assert.equal(step?.initial, false);
    assert.equal(step?.actions.length, 0);
  });

  it('never guesses an initial step', () => {
    // Which step is initial is exactly what a transcription must state, so none is invented.
    const grafcet = read('sequence S\n0 -> 1 : A\n1 -> 0 : B\n');

    assert.equal(grafcet.sequences[0]!.steps.some((step) => step.initial), false);
  });

  it('reports every bad line with its number', () => {
    const result = parseGrafcet('sequence S\nstep x\n0 -> 1 :\nstep 2 : timer T2 soon\n');

    assert.equal(result.ok, false);
    assert.deepEqual(result.ok ? [] : result.errors.map((error) => error.split(':')[0]), ['line 2', 'line 3', 'line 4']);
  });

  it('refuses a step declared twice, even with no actions', () => {
    const result = parseGrafcet(['sequence S', 'step 0 initial', 'step 0'].join(String.fromCharCode(10)));

    assert.equal(result.ok, false);
  });

  it('refuses a step before any sequence', () => {
    const result = parseGrafcet('step 0 initial\n');

    assert.equal(result.ok, false);
  });

  it('reads AND divergence and convergence', () => {
    const grafcet = read('sequence S\nstep 0 initial\n0 -> 1,2 : GO\n1,2 -> 3 : 1\n3 -> 0 : 1\n');

    const [split, join] = grafcet.sequences[0]!.transitions;

    assert.deepEqual(split!.to, [1, 2]);
    assert.deepEqual(join!.from, [1, 2]);
  });
});
