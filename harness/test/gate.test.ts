import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { AuditEntry } from '../src/auditTrail.ts';
import { evaluateGate, type GateEvidence } from '../src/gate.ts';
import type { RunStatistics } from '../src/telemetry.ts';

/**
 * The gate decides whether Workshop Mode may be enabled, which is the one decision in this
 * repository that ends with a person near moving machinery.
 *
 * Every test here names one way the door must stay shut. A criterion nobody asserts is a criterion
 * that quietly stops holding, and this is the last place that may happen.
 */
describe('workshop gate', () => {
  it('opens only when all five criteria are met', () => {
    const verdict = evaluateGate(evidenceThatMeetsEverything());

    assert.equal(verdict.open, true, verdict.criteria.filter((c) => !c.met).map((c) => c.evidence).join(' | '));
    assert.equal(verdict.criteria.length, 5);
  });

  it('stays shut below fifty complete runs', () => {
    const evidence = { ...evidenceThatMeetsEverything(), runs: passingRuns(49) };

    const verdict = evaluateGate(evidence);

    assert.equal(verdict.open, false);
    assert.equal(criterion(verdict, 1).met, false);
  });

  it('does not count an unfinished run towards the fifty', () => {
    // A run with no outcome is one that was interrupted. Counting it would make the gate open on
    // evidence that includes a run nobody knows the end of.
    const runs = [...passingRuns(49), { ...passingRun(50), outcome: undefined }];

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), runs });

    assert.equal(criterion(verdict, 1).met, false);
    assert.match(criterion(verdict, 1).evidence, /49 complete/);
  });

  it('refuses evidence gathered outside Study Mode', () => {
    const audit = { entries: [entry({ mode: 'Workshop' })], unreadableLines: [] };

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), audit });

    assert.equal(criterion(verdict, 1).met, false);
    assert.match(criterion(verdict, 1).evidence, /Workshop/);
  });

  it('treats an unrecognised audit outcome as a silent failure', () => {
    // The point of criterion 2: an outcome this harness cannot classify is unknown, not new.
    const audit = { entries: [entry({ outcome: 'Probably fine' })], unreadableLines: [] };

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), audit });

    assert.equal(criterion(verdict, 2).met, false);
  });

  it('treats an unreadable audit line as a silent failure', () => {
    const audit = { entries: [entry({})], unreadableLines: [17] };

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), audit });

    assert.equal(criterion(verdict, 2).met, false);
    assert.match(criterion(verdict, 2).evidence, /1 unreadable/);
  });

  it('treats an iteration with no outcome as a silent failure', () => {
    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), unfinishedIterations: 1 });

    assert.equal(criterion(verdict, 2).met, false);
  });

  it('refuses when a recorded backup is not on disk', () => {
    const audit = { entries: [entry({ backupPath: 'C:\\backups\\gone' })], unreadableLines: [] };

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), audit, backupExists: () => false });

    assert.equal(criterion(verdict, 3).met, false);
    assert.match(criterion(verdict, 3).evidence, /gone/);
  });

  it('does not ask a refused change for a backup it never took', () => {
    // A refusal exports nothing, so requiring a backup for one would make the criterion unmeetable
    // by the governance layer working exactly as designed.
    const audit = {
      entries: [entry({ outcome: 'Refused', backupPath: 'C:\\backups\\never-written' })],
      unreadableLines: []
    };

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), audit, backupExists: () => false });

    assert.equal(criterion(verdict, 3).met, true);
  });

  it('refuses when the clean-compilation rate falls beyond the tolerance', () => {
    // Ten runs at 100%, then ten at 50%: a fall of fifty points against a tolerated ten.
    const runs = [...passingRuns(40), ...decliningRuns(41, 10)];

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), runs });

    assert.equal(criterion(verdict, 4).met, false);
  });

  it('does not call an improving rate unstable', () => {
    const runs = [...passingRuns(40), ...decliningRuns(41, 10), ...passingRuns(10, 51)];

    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), runs });

    assert.equal(criterion(verdict, 4).met, true);
  });

  it('cannot be opened by data alone, without the in-person review', () => {
    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), review: undefined });

    assert.equal(verdict.open, false);
    assert.equal(criterion(verdict, 5).met, false);
  });

  it('reports every criterion even when the first one fails', () => {
    // A "no" has to say what is missing, not stop at the first thing that is.
    const verdict = evaluateGate({ ...evidenceThatMeetsEverything(), runs: [] });

    assert.equal(verdict.criteria.length, 5);
    assert.equal(verdict.criteria.filter((one) => one.met).length, 3);
  });
});

/** Evidence in which all five criteria hold, for tests that spoil exactly one of them. */
function evidenceThatMeetsEverything(): GateEvidence {
  return {
    runs: passingRuns(50),
    unfinishedIterations: 0,
    audit: { entries: [entry({})], unreadableLines: [] },
    backupExists: () => true,
    review: { date: '2026-09-01', reviewer: 'the supervising teacher' }
  };
}

function criterion(verdict: { criteria: readonly { number: number; met: boolean; evidence: string }[] }, number: number) {
  const found = verdict.criteria.find((one) => one.number === number);

  assert.ok(found !== undefined, `no criterion ${number} in the verdict`);

  return found;
}

function passingRuns(count: number, from = 1): RunStatistics[] {
  return Array.from({ length: count }, (_unused, index) => passingRun(from + index));
}

function passingRun(runId: number): RunStatistics {
  return { runId, outcome: 'passed', startedAt: runId, specifications: 2, cleanCompilations: 2 };
}

function decliningRuns(from: number, count: number): RunStatistics[] {
  return Array.from({ length: count }, (_unused, index) => ({
    runId: from + index,
    outcome: 'failed',
    startedAt: from + index,
    specifications: 2,
    cleanCompilations: 1
  }));
}

/** One audit entry, with only the fields a test cares about overridden. */
function entry(overrides: Partial<AuditEntry>): AuditEntry {
  return {
    timestamp: '2026-08-26T19:00:00+02:00',
    planId: 'ABC-123',
    mode: 'Study',
    tool: 'WriteScl',
    target: 'PLC_0',
    backupPath: '',
    origin: 'agent',
    outcome: 'Applied',
    detail: '',
    ...overrides
  };
}
