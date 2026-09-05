import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { AuditEntry, AuditReadResult } from '../src/auditTrail.ts';
import type { AuditResponse } from '../src/dashboardApi.ts';
import { respondTo, type ApiSources, type DashboardStore } from '../src/dashboardApi.ts';
import type { GateVerdict } from '../src/gate.ts';
import type { RecordedRun } from '../src/metricsReader.ts';

/**
 * The API is what the dashboard believes, so every way it can mislead is worth a test.
 *
 * All of them run against a stand-in store and a trail built in memory: no database, no socket, no
 * TIA Portal. What is being checked here is the routing and the refusals, and those are exactly the
 * parts that would otherwise only be exercised by clicking around a browser.
 */
describe('the dashboard API', () => {
  it('serves the install guide as the document, not as a rendering of it', () => {
    const response = respondTo('/api/guide', new URLSearchParams(), sources());

    assert.equal(response.status, 200);
    assert.match((response.body as { markdown: string }).markdown, /Installing tia-station-mcp/);
  });

  it('answers what this machine meets, item by item', () => {
    const response = respondTo('/api/preconditions', new URLSearchParams(), sources());

    assert.equal(response.status, 200);
    assert.equal((response.body as { ready: boolean }).ready, true);
  });

  it('names the endpoints it has when asked for one it does not', () => {
    const answer = respondTo('/api/runz', new URLSearchParams(), sources());

    assert.equal(answer.status, 404);
    assert.deepEqual((answer.body as { endpoints: string[] }).endpoints.includes('/api/runs'), true);
  });

  it('refuses a filter it does not know instead of ignoring it', () => {
    // The failure this prevents: '?tol=CreateBlock' answering with the whole trail, which reads as an
    // audit that recorded no such restriction. An audit view may not do that.
    const answer = respondTo('/api/audit', new URLSearchParams({ tol: 'CreateBlock' }), sources());

    assert.equal(answer.status, 400);
    assert.match(String((answer.body as { error: string }).error), /tol/);
  });

  it('filters the audit trail by tool and reports how many entries there were', () => {
    const answer = respondTo('/api/audit', new URLSearchParams({ tool: 'CreateBlock' }), sources());
    const body = answer.body as AuditResponse;

    assert.equal(answer.status, 200);
    assert.equal(body.entries.length, 1);
    assert.equal(body.matched, 1);
    assert.equal(body.total, 2);
  });

  it('sends the most recent entries when the trail is longer than the limit', () => {
    const long = {
      entries: [entry({ planId: 'oldest' }), entry({ planId: 'middle' }), entry({ planId: 'newest' })],
      unreadableLines: []
    };

    const answer = respondTo('/api/audit', new URLSearchParams({ limit: '2' }), sources(long));
    const body = answer.body as AuditResponse;

    // The end of the list, not the start. The trail is written in order, so what somebody opening
    // this view is looking for is what happened last.
    assert.deepEqual(
      body.entries.map((shown) => shown.planId),
      ['middle', 'newest']
    );
    // And it says so: 2 shown of 3 matched. A truncated answer that reported itself as complete
    // would be an audit view claiming the trail ends where the page does.
    assert.equal(body.matched, 3);
    assert.equal(body.limit, 2);
  });

  it('refuses a limit that is not a count instead of quietly using the default', () => {
    // '?limit=all' silently becoming 200 would answer a request for everything with a fifth of it.
    assert.equal(respondTo('/api/audit', new URLSearchParams({ limit: 'all' }), sources()).status, 400);
    assert.equal(respondTo('/api/audit', new URLSearchParams({ limit: '0' }), sources()).status, 400);
  });

  it('tells a run that was never recorded from an identifier that is not one', () => {
    // Two different things to whoever is looking at it: a typed URL, and a run that is gone.
    assert.equal(respondTo('/api/runs/nine', new URLSearchParams(), sources()).status, 400);
    assert.equal(respondTo('/api/runs/99', new URLSearchParams(), sources()).status, 404);
  });

  it('tells an iteration that has finished no phase from one that is not an iteration', () => {
    // An empty list is a real answer: an iteration that has started and finished no phase yet has
    // nothing to show, and a screen watching a run in progress must not read that as an error.
    const empty = respondTo('/api/iterations/7/phases', new URLSearchParams(), sources());

    assert.equal(empty.status, 200);
    assert.deepEqual((empty.body as { phases: unknown[] }).phases, []);
    assert.equal(respondTo('/api/iterations/none/phases', new URLSearchParams(), sources()).status, 400);
  });

  it('answers unknown for the mode banner when nothing was recorded', () => {
    const answer = respondTo('/api/mode', new URLSearchParams(), sources({ entries: [], unreadableLines: [] }));

    // Never 'Study'. A banner that assumes the safe answer when it does not know is worse than none,
    // and what this API reads is files: the live session is what GetOperationMode is for.
    assert.equal((answer.body as { mode: string }).mode, 'unknown');
  });

  it('shows Workshop on the banner as soon as one operation was in it', () => {
    const trail = {
      entries: [entry({ mode: 'Study' }), entry({ mode: 'Workshop' })],
      unreadableLines: []
    };

    const answer = respondTo('/api/mode', new URLSearchParams(), sources(trail));

    assert.equal((answer.body as { mode: string }).mode, 'Workshop');
  });

  it('answers unknown when the trail records a mode it does not recognise', () => {
    const trail = { entries: [entry({ mode: 'Rehearsal' })], unreadableLines: [] };

    const answer = respondTo('/api/mode', new URLSearchParams(), sources(trail));

    assert.equal((answer.body as { mode: string }).mode, 'unknown');
  });

  it('reports every metric with the sample size it was computed from', () => {
    const answer = respondTo('/api/metrics', new URLSearchParams(), sources());
    const body = answer.body as { sampleSize: { runs: number; specificationAttempts: number } };

    // The roadmap forbids a bare percentage, and a chart drawn from this endpoint can only obey that
    // if the counts arrive with it.
    assert.equal(body.sampleSize.runs, 1);
    assert.equal(body.sampleSize.specificationAttempts, 3);
  });

  it('names the generator its numbers are about, even when they are a blend', () => {
    const answer = respondTo('/api/metrics', new URLSearchParams(), sources());
    const body = answer.body as { generator: string; generators: { generator: string }[] };

    // 'all' spelled out rather than left absent: a caller that forgets to look at this field is the
    // one the field exists to protect, and an empty one reads as "the usual generator".
    assert.equal(body.generator, 'all');
    assert.deepEqual(body.generators.map((entry) => entry.generator), ['stub']);
  });

  it('answers for one generator when asked for one', () => {
    const answer = respondTo('/api/metrics', new URLSearchParams('generator=stub'), sources());

    assert.equal(answer.status, 200);
    assert.equal((answer.body as { generator: string }).generator, 'stub');
  });

  it('refuses a generator no run used rather than quietly returning every run', () => {
    // The failure this prevents is the silent one: a mistyped generator answering with the blend
    // looks exactly like the answer that was wanted, and nothing on the page would say otherwise.
    const answer = respondTo('/api/metrics', new URLSearchParams('generator=gpt'), sources());

    assert.equal(answer.status, 400);
    assert.match((answer.body as { error: string }).error, /No run in this store used generator 'gpt'/);
  });
});

const recordedRun: RecordedRun = {
  runId: 7,
  specSet: 'specs',
  serverExecutable: 'TiaMcpServer.exe',
  iterationLimit: 3,
  generator: 'stub',
  startedAt: 1000,
  endedAt: 2000,
  outcome: 'passed',
  iterations: 4,
  specifications: 2,
  cleanCompilations: 2,
  passed: 1
};

/** A store that answers from constants, so the routing is the only thing under test. */
const store: DashboardStore = {
  runs: () => [recordedRun],
  run: (runId) => (runId === recordedRun.runId ? recordedRun : undefined),
  iterationsOf: () => [],
  phaseDurations: () => [],
  phasesOfIteration: () => [],
  generators: () => [{ generator: 'stub', runs: 1 }],
  specificationStatistics: () => [
    {
      specification: 'gripper',
      attempts: 3,
      cleanCompilations: 2,
      passed: 1,
      meanIterationsToCleanCompilation: 1.5
    }
  ]
};

const shutGate: GateVerdict = { open: false, criteria: [] };

function sources(trail?: AuditReadResult): ApiSources {
  return {
    reader: store,
    readAudit: () => trail ?? defaultTrail(),
    evaluateGate: () => shutGate,
    readGuide: () => ({ markdown: '# Installing tia-station-mcp', available: true, reason: '' }),
    checkPreconditions: () => ({ available: true, ready: true, checks: [], reason: '' })
  };
}

function defaultTrail(): AuditReadResult {
  return {
    entries: [entry({ tool: 'CreateBlock' }), entry({ tool: 'CompileSoftware' })],
    unreadableLines: []
  };
}

function entry(overrides: Partial<AuditEntry>): AuditEntry {
  return {
    timestamp: '2026-08-26T10:00:00Z',
    planId: 'a-plan',
    mode: 'Study',
    tool: 'CreateBlock',
    target: 'Cell/FB_Station',
    backupPath: '',
    origin: 'harness',
    outcome: 'Applied',
    detail: '',
    ...overrides
  };
}
