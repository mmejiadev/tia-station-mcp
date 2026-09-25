import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { checkGrafcet, type ChartRule } from '../../src/grafcet/grafcetCheck.ts';
import { parseGrafcet } from '../../src/grafcet/grafcetText.ts';

/**
 * One test per rule, each naming it. A rule that only happens to be covered by a test about
 * something else is a rule that can be deleted without anything turning red.
 */
function rules(text: string): ChartRule[] {
  const parsed = parseGrafcet(text);
  assert.ok(parsed.ok, parsed.ok ? '' : parsed.errors.join('; '));

  return checkGrafcet(parsed.grafcet).map((finding) => finding.rule);
}

const Clean = 'sequence S\nstep 0 initial\nstep 1 : MOTOR\n0 -> 1 : START\n1 -> 0 : /START\n';

describe('GRAFCET design checks', () => {
  it('passes a clean cycle without findings', () => {
    assert.deepEqual(rules(Clean), []);
  });

  it('initial-step: a sequence with no initial step can never run', () => {
    assert.ok(rules('sequence S\n0 -> 1 : A\n1 -> 0 : /A\n').includes('initial-step'));
  });

  it('duplicate-step: a step number is unique across the whole chart', () => {
    assert.ok(rules(`${Clean}sequence T\nstep 1 initial\n1 -> 2 : B\n2 -> 1 : /B\n`).includes('duplicate-step'));
  });

  it('unreachable-step: nothing leads to a step from an initial step', () => {
    assert.ok(rules('sequence S\nstep 0 initial\n0 -> 1 : A\n1 -> 0 : /A\n5 -> 0 : B\n').includes('unreachable-step'));
  });

  it('dead-end-step: a step with no way out', () => {
    assert.ok(rules('sequence S\nstep 0 initial\n0 -> 1 : A\n').includes('dead-end-step'));
  });

  it('uncertain-reading: a ? stays a finding until someone confirms it', () => {
    assert.ok(rules('sequence S\nstep 0 initial\n0 -> 1 : ?/A\n1 -> 0 : A\n').includes('uncertain-reading'));
  });

  it('non-exclusive-selection: two branches that can clear together', () => {
    assert.ok(rules('sequence S\nstep 0 initial\n0 -> 1 : A\n0 -> 2 : B\n1 -> 0 : 1\n2 -> 0 : 1\n').includes('non-exclusive-selection'));
  });

  it('non-exclusive-selection: branches split on one variable are exclusive', () => {
    const found = rules('sequence S\nstep 0 initial\n0 -> 1 : GO.OK\n0 -> 2 : GO./OK\n1 -> 0 : /GO\n2 -> 0 : /GO\n');

    assert.ok(!found.includes('non-exclusive-selection'));
  });

  it('transient-evolution: the same level condition on two consecutive transitions', () => {
    // The shape found in a real class project: 1 -> 2 on CE, then 2 -> 3 on CE.PE.
    assert.ok(rules('sequence S\nstep 0 initial\n0 -> 1 : GO\n1 -> 2 : CE\n2 -> 3 : CE.PE\n3 -> 0 : /CE\n').includes('transient-evolution'));
  });

  it('transient-evolution: not raised when the second transition needs the opposite level', () => {
    const found = rules('sequence S\nstep 0 initial\n0 -> 1 : A.B\n1 -> 0 : /A.B\n');

    assert.ok(!found.includes('transient-evolution'));
  });

  it('unknown-step-reference: a condition that reads a step nobody drew', () => {
    assert.ok(rules('sequence S\nstep 0 initial\n0 -> 1 : X99\n1 -> 0 : /A\n').includes('unknown-step-reference'));
  });
});
