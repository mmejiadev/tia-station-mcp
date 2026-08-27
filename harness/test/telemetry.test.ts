import assert from 'node:assert/strict';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { DatabaseSync } from 'node:sqlite';
import { describe, it } from 'node:test';
import { Telemetry } from '../src/telemetry.ts';

/**
 * What the harness records is what the phase delivers, so it is worth its own tests.
 *
 * All of them run against an in-memory store except the two that are about a file, so they need
 * neither TIA Portal nor a disk. The roadmap asks for numbers reported with their sample size
 * attached; these are the tests that the sample size is counted rather than assumed.
 */
describe('Telemetry', () => {
  it('counts a run by how its iterations ended', () => {
    const telemetry = Telemetry.open(':memory:');

    try {
      const runId = telemetry.startRun({
        specSet: 'specs/smoke',
        serverExecutable: 'TiaMcpServer.exe',
        iterationLimit: 5,
        generator: 'stub'
      });

      // One of each, so a summary that confused two categories could not pass.
      finishOne(telemetry, runId, 'first', 'passed', 0);
      finishOne(telemetry, runId, 'second', 'compiler-errors', 3);
      finishOne(telemetry, runId, 'third', 'behaviour-failed', 0);
      finishOne(telemetry, runId, 'fourth', 'refused', 0);

      // And one left unfinished, which is what an interrupted run leaves behind. It has to be
      // counted: a run that crashed halfway must not report fewer iterations than it ran.
      telemetry.startIteration(runId, 'fifth', 1);

      const summary = telemetry.summarise(runId);

      assert.equal(summary.iterations, 5);
      assert.equal(summary.counts['passed'], 1);
      assert.equal(summary.counts['compiler-errors'], 1);
      assert.equal(summary.counts['behaviour-failed'], 1);
      assert.equal(summary.counts['refused'], 1);
      assert.equal(summary.counts['unfinished'], 1);
    } finally {
      telemetry.close();
    }
  });

  it('reports an empty run as zero iterations rather than as nothing', () => {
    // A run that started and did nothing is a real state: it is what a run that crashed at once
    // looks like. It must read as zero, never as a missing number that becomes NaN in a report.
    const telemetry = Telemetry.open(':memory:');

    try {
      const runId = telemetry.startRun({
        specSet: 'specs/empty',
        serverExecutable: 'TiaMcpServer.exe',
        iterationLimit: 1,
        generator: 'stub'
      });

      const summary = telemetry.summarise(runId);

      assert.equal(summary.iterations, 0);
      assert.deepEqual(summary.counts, {});
    } finally {
      telemetry.close();
    }
  });

  it('times a phase that threw, and records that it threw', async () => {
    // The reason the timing is in a finally block. "The compile took ninety seconds and then
    // failed" is a different problem from "the compile failed at once", and only one of them is
    // visible if a failed phase records nothing.
    const telemetry = Telemetry.open(':memory:');

    try {
      const runId = telemetry.startRun({
        specSet: 'specs/smoke',
        serverExecutable: 'TiaMcpServer.exe',
        iterationLimit: 1,
        generator: 'stub'
      });
      const iterationId = telemetry.startIteration(runId, 'first', 1);

      await assert.rejects(
        () => telemetry.time(iterationId, 'compile', async () => {
          throw new Error('the compiler fell over');
        }),
        /fell over/
      );

      const phases = telemetry.phasesOf(iterationId);

      assert.equal(phases.length, 1);
      assert.equal(phases[0]?.phase, 'compile');
      assert.equal(phases[0]?.outcome, 'failed');
    } finally {
      telemetry.close();
    }
  });

  it('keeps the phases of an iteration in the order they ran', async () => {
    const telemetry = Telemetry.open(':memory:');

    try {
      const runId = telemetry.startRun({
        specSet: 'specs/smoke',
        serverExecutable: 'TiaMcpServer.exe',
        iterationLimit: 1,
        generator: 'stub'
      });
      const iterationId = telemetry.startIteration(runId, 'first', 1);

      await telemetry.time(iterationId, 'generate', async () => 'source');
      await telemetry.time(iterationId, 'write', async () => undefined);
      await telemetry.time(iterationId, 'compile', async () => undefined);

      assert.deepEqual(
        telemetry.phasesOf(iterationId).map((entry) => entry.phase),
        ['generate', 'write', 'compile']
      );
    } finally {
      telemetry.close();
    }
  });

  it('refuses to open a store written by a different schema version', () => {
    // The one failure mode that could produce a wrong number rather than an error: columns from an
    // older schema read as though they meant what they mean now.
    const directory = mkdtempSync(join(tmpdir(), 'tia-harness-'));
    const path = join(directory, 'nested', 'metrics.db');

    try {
      const first = Telemetry.open(path);
      first.close();

      // Reopening the same store is fine. That is the case this must not break.
      const second = Telemetry.open(path);
      second.close();

      corruptSchemaVersion(path);

      assert.throws(() => Telemetry.open(path), /schema version/);
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('records what a generation cost, per attempt rather than per run', () => {
    // A specification that passes on the third try paid for three generations. Summed on the way in,
    // the expensive one would be invisible.
    const telemetry = Telemetry.open(':memory:');

    try {
      const runId = telemetry.startRun(context());

      telemetry.recordUsage(telemetry.startIteration(runId, 'two-station', 1), usage(1000, 2000));
      telemetry.recordUsage(telemetry.startIteration(runId, 'two-station', 2), usage(3000, 4000));

      const recorded = telemetry.usageOfRun(runId);

      assert.equal(recorded.length, 2);
      assert.deepEqual(
        recorded.map((entry) => [entry.inputTokens, entry.outputTokens]),
        [
          [1000, 2000],
          [3000, 4000]
        ]
      );
    } finally {
      telemetry.close();
    }
  });

  it('reports no usage for a run that asked no model anything, rather than a zero', () => {
    // The stub costs nothing, and a run with no rows here is a run that spent nothing - which is a
    // different statement from a run whose cost was never measured.
    const telemetry = Telemetry.open(':memory:');

    try {
      const runId = telemetry.startRun(context());

      telemetry.startIteration(runId, 'two-station', 1);

      assert.deepEqual(telemetry.usageOfRun(runId), []);
    } finally {
      telemetry.close();
    }
  });

  it('carries a store forward from the schema that had no costs in it, keeping its runs', () => {
    // Version 2 only added a table. Refusing here would strand the thirty-nine runs already recorded
    // for no better reason than that they predate a column none of them uses.
    const directory = mkdtempSync(join(tmpdir(), 'tia-harness-'));
    const path = join(directory, 'metrics.db');

    try {
      const first = Telemetry.open(path);

      first.startRun(context());
      first.close();

      setSchemaVersion(path, 1);

      const migrated = Telemetry.open(path);

      try {
        assert.equal(migrated.runStatistics().length, 1);
      } finally {
        migrated.close();
      }
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });
});

function finishOne(
  telemetry: Telemetry,
  runId: ReturnType<Telemetry['startRun']>,
  specification: string,
  outcome: 'passed' | 'compiler-errors' | 'behaviour-failed' | 'refused',
  errorCount: number
): void {
  const iterationId = telemetry.startIteration(runId, specification, 1);

  telemetry.finishIteration(iterationId, outcome, errorCount);
}

/** A run context, since none of these tests is about what is in one. */
function context(): Parameters<Telemetry['startRun']>[0] {
  return {
    specSet: 'specs',
    serverExecutable: 'TiaMcpServer.exe',
    iterationLimit: 3,
    generator: 'a-model'
  };
}

/** Token counts distinct enough that a transposed input and output would fail the assertion. */
function usage(inputTokens: number, outputTokens: number): Parameters<Telemetry['recordUsage']>[1] {
  return { model: 'a-model', inputTokens, outputTokens, cacheCreationTokens: 0, cacheReadTokens: 0 };
}

/** Stamps a store with a version, standing in for a build that wrote it. */
function setSchemaVersion(path: string, version: number): void {
  const database = new DatabaseSync(path);

  database.prepare('UPDATE schema_version SET version = ?').run(version);
  database.close();
}

/** Writes a version this harness does not know, the way an older build would have left one. */
function corruptSchemaVersion(path: string): void {
  const database = new DatabaseSync(path);

  database.prepare('UPDATE schema_version SET version = ?').run(99);
  database.close();
}
