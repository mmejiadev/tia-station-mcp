import assert from 'node:assert/strict';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { MetricsReader } from '../src/metricsReader.ts';
import { Telemetry, type IterationOutcome, type RunId } from '../src/telemetry.ts';

/**
 * The reader is what the dashboard sees, so what it reports is what the phase's numbers become.
 *
 * These run against a store on disk rather than in memory, because reading a store a run wrote is
 * the whole behaviour: a reader that only ever saw a database it had created itself would not have
 * caught the missing-file case below. None of them needs TIA Portal.
 */
describe('MetricsReader', () => {
  it('refuses a store that is not there rather than reporting zero measurements', () => {
    const directory = temporaryDirectory();

    try {
      // The failure this prevents is the quiet one: a mistyped path reading as a run that measured
      // nothing, which looks exactly like a run that measured nothing.
      assert.throws(
        () => MetricsReader.open(join(directory, 'absent.db')),
        /There is no metrics store at/
      );
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('reports runs newest first and the gate its runs oldest last', () => {
    const directory = temporaryDirectory();
    const path = join(directory, 'metrics.db');

    try {
      write(path, (telemetry) => {
        finishRun(telemetry, telemetry.startRun(context(), 1000));
        finishRun(telemetry, telemetry.startRun(context(), 2000));
      });

      const reader = MetricsReader.open(path);

      try {
        // Two orders, on purpose: a list of runs is read newest first, and criterion 4 of the gate
        // slices the end of its list to get the most recent twenty. Handing the gate this list the
        // other way round would compare the two halves of its window backwards.
        assert.deepEqual(
          reader.runs().map((run) => run.startedAt),
          [2000, 1000]
        );
        assert.deepEqual(
          reader.runStatistics().map((run) => run.startedAt),
          [1000, 2000]
        );
      } finally {
        reader.close();
      }
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('counts a specification as compiled when it compiled, whatever happened afterwards', () => {
    const directory = temporaryDirectory();
    const path = join(directory, 'metrics.db');

    try {
      write(path, (telemetry) => {
        const runId = telemetry.startRun(context());

        // Compiled and then failed on the controller: a clean compilation that is not a pass.
        finishIteration(telemetry, runId, 'gripper', 1, 'behaviour-failed');
        // Never reached the compiler at all, and must not be counted as either.
        finishIteration(telemetry, runId, 'conveyor', 1, 'refused');
        finishRun(telemetry, runId);
      });

      const reader = MetricsReader.open(path);

      try {
        const statistics = reader.specificationStatistics();
        const gripper = statistics.find((entry) => entry.specification === 'gripper');
        const conveyor = statistics.find((entry) => entry.specification === 'conveyor');

        assert.equal(gripper?.cleanCompilations, 1);
        assert.equal(gripper?.passed, 0);
        assert.equal(conveyor?.cleanCompilations, 0);
        assert.equal(conveyor?.meanIterationsToCleanCompilation, undefined);
      } finally {
        reader.close();
      }
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('averages iterations to a clean compilation over the attempts that got one', () => {
    const directory = temporaryDirectory();
    const path = join(directory, 'metrics.db');

    try {
      write(path, (telemetry) => {
        const first = telemetry.startRun(context());

        finishIteration(telemetry, first, 'gripper', 1, 'compiler-errors');
        finishIteration(telemetry, first, 'gripper', 2, 'passed');
        finishRun(telemetry, first);

        const second = telemetry.startRun(context());

        finishIteration(telemetry, second, 'gripper', 1, 'compiler-errors');
        finishIteration(telemetry, second, 'gripper', 2, 'compiler-errors');
        finishRun(telemetry, second);
      });

      const reader = MetricsReader.open(path);

      try {
        const gripper = reader.specificationStatistics()[0];

        // Two attempts, one of which got there on its second iteration. The mean is 2 over that one,
        // not 1 over both: averaging in a run that never compiled would report the specification as
        // faster the worse it went.
        assert.equal(gripper?.attempts, 2);
        assert.equal(gripper?.meanIterationsToCleanCompilation, 2);
      } finally {
        reader.close();
      }
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('counts a phase that threw in the mean, and says how many threw', async () => {
    const directory = temporaryDirectory();
    const path = join(directory, 'metrics.db');

    try {
      const telemetry = Telemetry.open(path);

      try {
        const runId = telemetry.startRun(context());
        const iterationId = telemetry.startIteration(runId, 'gripper', 1);

        await telemetry.time(iterationId, 'compile', async () => undefined);
        await assert.rejects(
          telemetry.time(iterationId, 'compile', async () => {
            throw new Error('the compile died');
          })
        );
      } finally {
        telemetry.close();
      }

      const reader = MetricsReader.open(path);

      try {
        const compile = reader.phaseDurations().find((phase) => phase.phase === 'compile');

        // Both samples, not just the one that worked: "the compile took ninety seconds and then
        // failed" is the measurement worth having, and dropping it reports a loop that gets faster
        // the more of it breaks.
        assert.equal(compile?.samples, 2);
        assert.equal(compile?.failures, 1);
      } finally {
        reader.close();
      }
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });
});

function temporaryDirectory(): string {
  return mkdtempSync(join(tmpdir(), 'tia-metrics-reader-'));
}

function write(path: string, record: (telemetry: Telemetry) => void): void {
  const telemetry = Telemetry.open(path);

  try {
    record(telemetry);
  } finally {
    telemetry.close();
  }
}

function context() {
  return { specSet: 'specs/smoke', serverExecutable: 'TiaMcpServer.exe', iterationLimit: 3, generator: 'stub' };
}

function finishRun(telemetry: Telemetry, runId: RunId): void {
  telemetry.finishRun(runId, 'passed');
}

function finishIteration(
  telemetry: Telemetry,
  runId: RunId,
  specification: string,
  attempt: number,
  outcome: IterationOutcome
): void {
  telemetry.finishIteration(telemetry.startIteration(runId, specification, attempt), outcome, 0);
}
