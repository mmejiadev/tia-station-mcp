import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { AuditReadResult } from '../src/auditTrail.ts';
import { buildBrief, type BriefSources } from '../src/copilotBrief.ts';
import type { DashboardStore } from '../src/dashboardApi.ts';
import type { GateVerdict } from '../src/gate.ts';
import type { GeneratorSample, RecordedRun, SpecificationStatistics } from '../src/metricsReader.ts';

/**
 * The brief is the copilot's whole world, so what is missing from it matters as much as what is in
 * it. These tests are about the facts that must survive the summarising: the sample sizes, the
 * modes, and the difference between "no outcome" and a made-up one.
 */
describe('the copilot brief', () => {
  it('states how many runs there are, not just how many it describes', () => {
    // Twelve runs, ten described. Without the total, a copilot reading this would answer "ten" to
    // "how many runs are there" and be confidently wrong about the one number it cannot check.
    const brief = buildBrief(sources({ runs: Array.from({ length: 12 }, (_, index) => run(index + 1)) }));

    assert.match(brief.text, /12 run\(s\) recorded/);
    assert.match(brief.text, /The 10 most recent/);
  });

  it('says a run without an outcome is unfinished rather than giving it one', () => {
    const brief = buildBrief(sources({ runs: [run(1, { outcome: undefined })] }));

    assert.match(brief.text, /no outcome recorded \(still going, or interrupted\)/);
  });

  it('carries every sample size beside the rate it belongs to', () => {
    // A number without its sample size is not a measurement in this project, and a copilot cannot
    // quote a sample size that was never sent to it.
    const brief = buildBrief(
      sources({
        specifications: [
          {
            specification: 'two-station-runs',
            attempts: 7,
            cleanCompilations: 5,
            passed: 4,
            meanIterationsToCleanCompilation: 1.4
          }
        ]
      })
    );

    assert.match(brief.text, /attempted 7 time\(s\)/);
    assert.match(brief.text, /1\.40 mean iterations/);
  });

  it('warns that its rates are a blend when the store holds more than one generator', () => {
    // The copilot only ever sees this text, so it cannot discover afterwards that 80% came from two
    // generators. If the brief does not say so, the answer on screen attributes it to one of them.
    const brief = buildBrief(sources({ generators: [{ generator: 'stub', runs: 50 }, { generator: 'model', runs: 10 }] }));

    assert.match(brief.text, /stub: 50 run\(s\)/);
    assert.match(brief.text, /model: 10 run\(s\)/);
    assert.match(brief.text, /ALL of these generators together/);
  });

  it('does not warn about a blend when there is only one generator to blend', () => {
    const brief = buildBrief(sources({ generators: [{ generator: 'stub', runs: 50 }] }));

    assert.match(brief.text, /Every rate below is over that one generator/);
  });

  it('says a specification that never compiled has no mean, rather than a mean of zero', () => {
    const brief = buildBrief(
      sources({
        specifications: [
          {
            specification: 'four-station-runs',
            attempts: 3,
            cleanCompilations: 0,
            passed: 0,
            meanIterationsToCleanCompilation: undefined
          }
        ]
      })
    );

    assert.match(brief.text, /no clean compilation, so no mean/);
    assert.doesNotMatch(brief.text, /0\.00 mean iterations/);
  });

  it('names every mode the trail recorded, because Workshop appearing there is the fact that matters most', () => {
    const brief = buildBrief(
      sources({
        audit: {
          entries: [auditEntry('Study'), auditEntry('Workshop')],
          unreadableLines: []
        }
      })
    );

    assert.match(brief.text, /Modes recorded: Study, Workshop/);
  });

  it('says when part of the trail could not be read, so its counts are never passed off as complete', () => {
    const brief = buildBrief(
      sources({ audit: { entries: [auditEntry('Study')], unreadableLines: [4, 9] } })
    );

    assert.match(brief.text, /2 line\(s\) could not be read/);
  });

  it('carries the gate verdict as the gate decided it', () => {
    const brief = buildBrief(sources({}));

    assert.match(brief.text, /Verdict: CLOSED/);
    assert.match(brief.text, /NOT MET - 50 complete loop runs/);
  });

  it('says plainly that an empty store is empty', () => {
    // The honest answer to "what has this been doing" on a fresh machine. An empty brief that simply
    // omitted the sections would leave the copilot to fill the silence.
    const brief = buildBrief(sources({ runs: [], specifications: [] }));

    assert.match(brief.text, /Nothing has been recorded in this store yet/);
    assert.match(brief.text, /No specification has been attempted/);
  });
});

type Overrides = {
  runs?: RecordedRun[];
  specifications?: SpecificationStatistics[];
  generators?: GeneratorSample[];
  audit?: AuditReadResult;
};

function sources(overrides: Overrides): BriefSources {
  const reader: DashboardStore = {
    runs: () => overrides.runs ?? [run(1)],
    run: () => undefined,
    iterationsOf: () => [],
    phaseDurations: () => [
      { phase: 'compile', samples: 121, totalMilliseconds: 193_600, meanMilliseconds: 1600, failures: 0 }
    ],
    phasesOfIteration: () => [],
    specificationStatistics: () => overrides.specifications ?? [],
    generators: () => overrides.generators ?? [{ generator: 'stub', runs: 1 }]
  };

  return {
    reader,
    readAudit: () => overrides.audit ?? { entries: [], unreadableLines: [] },
    evaluateGate: () => verdict()
  };
}

function run(runId: number, overrides: Partial<RecordedRun> = {}): RecordedRun {
  return {
    runId,
    specSet: 'specs',
    serverExecutable: 'TiaMcpServer.exe',
    iterationLimit: 3,
    generator: 'claude-haiku-4-5',
    startedAt: 1_787_863_742_997,
    endedAt: 1_787_863_800_000,
    outcome: 'completed',
    iterations: 1,
    specifications: 1,
    cleanCompilations: 0,
    passed: 0,
    ...overrides
  };
}

function auditEntry(mode: string): AuditReadResult['entries'][number] {
  return {
    timestamp: '2026-08-27T20:48:01.495Z',
    mode,
    tool: 'UseTcpIpNetworkMode',
    outcome: 'Refused',
    target: 'project',
    planId: '33N-CMV'
  } as AuditReadResult['entries'][number];
}

function verdict(): GateVerdict {
  return {
    open: false,
    criteria: [
      {
        number: 1,
        name: '50 complete loop runs in Study Mode',
        met: false,
        evidence: '39 complete run(s) of 50 required'
      }
    ]
  } as GateVerdict;
}
