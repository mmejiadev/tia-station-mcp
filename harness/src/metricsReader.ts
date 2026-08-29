import { existsSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import { verifySchemaVersion } from './schemaVersion.ts';
import { CleanCompilationOutcomes, type LoopPhase, type RunStatistics } from './telemetry.ts';

/** One run, as the dashboard's list of runs shows it. */
export type RecordedRun = {
  readonly runId: number;
  readonly specSet: string;
  readonly serverExecutable: string;
  readonly iterationLimit: number;
  readonly generator: string;
  readonly startedAt: number;
  /** Undefined while the run is still going, or for ever if it was interrupted. */
  readonly endedAt: number | undefined;
  /** Undefined for the same two reasons, which the dashboard must show as such. */
  readonly outcome: string | undefined;
  readonly iterations: number;
  readonly specifications: number;
  readonly cleanCompilations: number;
  /** How many of them also ran on a simulated CPU and behaved as specified. */
  readonly passed: number;
};

/** One iteration of one run, in the order it ran. */
export type RecordedIteration = {
  readonly iterationId: number;
  readonly specification: string;
  readonly attempt: number;
  readonly startedAt: number;
  readonly endedAt: number | undefined;
  readonly outcome: string | undefined;
  readonly errorCount: number | undefined;
};

/** One phase of one iteration, as it actually ran. */
export type IterationPhase = {
  readonly phase: LoopPhase;
  readonly startedAt: number;
  readonly durationMilliseconds: number;
  /** 'ok', or 'failed' when the phase threw. A failed phase is still a measurement. */
  readonly outcome: string;
};

/** How long one phase took, added up over everything it was measured on. */
export type PhaseDuration = {
  readonly phase: LoopPhase;
  /** How many times the phase was timed. Reported so no mean arrives without its sample size. */
  readonly samples: number;
  readonly totalMilliseconds: number;
  readonly meanMilliseconds: number;
  /** How many of those samples were phases that threw. */
  readonly failures: number;
};

/**
 * What every run of one specification adds up to.
 *
 * @remarks
 * Counted per attempted specification-run, not per run, so a set of six cases run ten times gives a
 * sample of ten for each case rather than of sixty for the set. That is the number the roadmap asks
 * for: mean iterations to a clean compilation *per specification*.
 */
export type SpecificationStatistics = {
  readonly specification: string;
  /** How many runs attempted it. This is the sample size, and it travels with every rate below. */
  readonly attempts: number;
  /** In how many of them it reached a clean compilation. */
  readonly cleanCompilations: number;
  /** In how many of them it also ran on a simulated CPU and behaved as specified. */
  readonly passed: number;
  /**
   * Mean iterations to the first clean compilation, over the attempts that got one.
   *
   * @remarks
   * Undefined rather than zero when none did. Zero is a number a chart would draw, and it would draw
   * the specification that never compiles as the fastest one on the page.
   */
  readonly meanIterationsToCleanCompilation: number | undefined;
};

/**
 * How a measurement is narrowed before it is computed.
 *
 * @remarks
 * `generator` exists because the README's central claim depends on it: *the loop was measured with a
 * deterministic generator first, on purpose, so that the loop's own failures could be separated from
 * a generator's*, and *mixing the two in one sample would produce a number about neither*. Until this
 * type existed, `specificationStatistics` read every iteration in the store and the first run with
 * `--generator model` would have blended the two silently — in the README's table, in the dashboard
 * and in the copilot's brief, all three from this one function.
 *
 * An absent field means "every one of them", never "the usual one". A blend is a legitimate thing to
 * ask for; a blend nobody asked for is the defect.
 */
export type MeasurementFilter = {
  readonly runId?: number;
  /** The generator as the run recorded it: `stub` or `model`. */
  readonly generator?: string;
};

/** How many runs one generator produced. Reported so a blended rate can never hide that it is one. */
export type GeneratorSample = {
  readonly generator: string;
  readonly runs: number;
};

/**
 * Reads what a run recorded, and writes nothing.
 *
 * @remarks
 * Its own connection to the same file the harness writes. That is what the WAL journal mode set in
 * `Telemetry.open` is for: the dashboard can read while a run is still going, which is the whole
 * reason a live view is worth having.
 *
 * The store is opened read-write even though nothing here writes, because a WAL reader needs to
 * touch the sidecar files. What keeps this class honest is that it prepares no statement that is
 * not a SELECT, not a flag on the connection.
 */
export class MetricsReader {
  private readonly database: DatabaseSync;

  private constructor(database: DatabaseSync) {
    this.database = database;
  }

  /**
   * Opens a store that a run has already written.
   *
   * @param path The metrics database.
   * @returns The reader.
   * @remarks
   * A missing file is refused rather than created. The caller is asking what was measured, and an
   * empty store would answer "nothing was measured" to a question that was really "where is the
   * file" — the same reason `readAuditTrail` refuses a missing trail instead of reporting no writes.
   */
  static open(path: string): MetricsReader {
    if (!existsSync(path)) {
      throw new Error(
        `There is no metrics store at ${path}. Point --database at one a run has written, rather ` +
          'than at a path that would quietly read as zero measurements.'
      );
    }

    const database = new DatabaseSync(path);

    try {
      verifySchemaVersion(database);
    } catch (error) {
      // Same reason as in Telemetry.open: on Windows a handle left behind keeps the file locked, and
      // the next thing to touch it fails with a permission error that says nothing about the schema.
      database.close();

      throw error;
    }

    return new MetricsReader(database);
  }

  /** Every run, newest first, which is the order a list of runs is read in. */
  runs(): RecordedRun[] {
    const placeholders = CleanCompilationOutcomes.map(() => '?').join(', ');
    const rows = this.database
      .prepare(
        `SELECT r.id AS runId, r.spec_set AS specSet, r.server_executable AS serverExecutable,
                r.iteration_limit AS iterationLimit, r.generator AS generator,
                r.started_at AS startedAt, r.ended_at AS endedAt, r.outcome AS outcome,
                COUNT(i.id) AS iterations,
                COUNT(DISTINCT i.specification) AS specifications,
                COUNT(DISTINCT CASE WHEN i.outcome IN (${placeholders})
                                    THEN i.specification END) AS cleanCompilations,
                COUNT(DISTINCT CASE WHEN i.outcome = 'passed' THEN i.specification END) AS passed
         FROM runs r LEFT JOIN iterations i ON i.run_id = r.id
         GROUP BY r.id ORDER BY r.id DESC`
      )
      .all(...CleanCompilationOutcomes) as RunRow[];

    return rows.map(toRecordedRun);
  }

  /** One run, or nothing when no run has that identifier. */
  run(runId: number): RecordedRun | undefined {
    return this.runs().find((run) => run.runId === runId);
  }

  /** Every iteration of one run, oldest first. */
  iterationsOf(runId: number): RecordedIteration[] {
    const rows = this.database
      .prepare(
        `SELECT id AS iterationId, specification, attempt, started_at AS startedAt,
                ended_at AS endedAt, outcome, error_count AS errorCount
         FROM iterations WHERE run_id = ? ORDER BY id`
      )
      .all(runId) as IterationRow[];

    return rows.map(toRecordedIteration);
  }

  /**
   * The phases of one iteration, in the order they ran.
   *
   * @remarks
   * Per iteration rather than per run, which is what a screen watching a run in progress needs: the
   * question there is not what the download costs on average, it is what this attempt has got through
   * so far.
   *
   * A phase appears here only once it has **ended** — the store records it in a finally block, which
   * is what makes a phase that threw get recorded at all. So the phase currently running is not in
   * this list, and nothing here should pretend otherwise.
   */
  phasesOfIteration(iterationId: number): IterationPhase[] {
    const rows = this.database
      .prepare(
        `SELECT phase, started_at AS startedAt, ended_at - started_at AS duration, outcome
         FROM phase_timings WHERE iteration_id = ? ORDER BY id`
      )
      .all(iterationId) as IterationPhaseRow[];

    return rows.map((row) => ({
      phase: row.phase as LoopPhase,
      startedAt: row.startedAt,
      durationMilliseconds: row.duration,
      outcome: row.outcome
    }));
  }

  /**
   * How long each phase took, over one run or over every run.
   *
   * @param filter Which run, which generator, or neither for everything.
   * @remarks
   * Failed phases are counted in the mean rather than dropped. "The compile took ninety seconds and
   * then failed" is the measurement most worth having, and a mean over successes only would report
   * the loop as faster the more of it broke. The failure count travels alongside so it can be read.
   */
  phaseDurations(filter: MeasurementFilter = {}): PhaseDuration[] {
    const rows = this.database
      .prepare(
        `SELECT p.phase AS phase, COUNT(*) AS samples,
                SUM(p.ended_at - p.started_at) AS total,
                SUM(CASE WHEN p.outcome = 'failed' THEN 1 ELSE 0 END) AS failures
         FROM phase_timings p
              JOIN iterations i ON i.id = p.iteration_id
              JOIN runs r ON r.id = i.run_id
         WHERE (? IS NULL OR i.run_id = ?) AND (? IS NULL OR r.generator = ?)
         GROUP BY p.phase ORDER BY p.phase`
      )
      .all(...repeated(filter.runId), ...repeated(filter.generator)) as PhaseRow[];

    return rows.map(toPhaseDuration);
  }

  /**
   * What each specification cost, across every run that attempted it.
   *
   * @param filter Which generator to count, or nothing for all of them.
   * @remarks
   * Read {@link MeasurementFilter} before calling this without a generator. Doing so is correct only
   * when the caller means "every generator in this store" and says so to whoever reads the number.
   */
  specificationStatistics(filter: MeasurementFilter = {}): SpecificationStatistics[] {
    const placeholders = CleanCompilationOutcomes.map(() => '?').join(', ');
    const rows = this.database
      .prepare(
        `SELECT i.specification AS specification, i.run_id AS runId,
                MIN(CASE WHEN i.outcome IN (${placeholders}) THEN i.attempt END) AS firstClean,
                MAX(CASE WHEN i.outcome = 'passed' THEN 1 ELSE 0 END) AS passed
         FROM iterations i JOIN runs r ON r.id = i.run_id
         WHERE (? IS NULL OR r.generator = ?)
         GROUP BY i.specification, i.run_id ORDER BY i.specification, i.run_id`
      )
      .all(...CleanCompilationOutcomes, ...repeated(filter.generator)) as SpecificationRow[];

    return summariseSpecifications(rows);
  }

  /**
   * Which generators this store holds, and how many runs each produced.
   *
   * @remarks
   * Exists so that no surface can present a blended rate without knowing it is one. A store with a
   * single generator returns one row and the question does not arise; a store with two returns two,
   * and a caller that ignores them is making a choice rather than missing a possibility.
   */
  generators(): GeneratorSample[] {
    const rows = this.database
      .prepare('SELECT generator, COUNT(*) AS runs FROM runs GROUP BY generator ORDER BY runs DESC, generator')
      .all() as unknown as GeneratorSample[];

    // Rebuilt rather than returned as they came: node:sqlite hands back null-prototype rows, and one
    // of those crossing into a caller compares unequal to the plain object it is identical to.
    return rows.map((row) => ({ generator: row.generator, runs: row.runs }));
  }

  /**
   * Every run in the order the workshop gate reads them, oldest last.
   *
   * @remarks
   * Derived from {@link runs} rather than queried again, so there is one definition of what a clean
   * compilation is on this side too. The reversal is not cosmetic: criterion 4 judges the last twenty
   * runs by slicing the end of this list, and handing it newest-first would compare the two halves of
   * the window backwards and call a falling rate a rising one.
   */
  runStatistics(): RunStatistics[] {
    return this.runs()
      .map((run) => ({
        runId: run.runId,
        outcome: run.outcome,
        startedAt: run.startedAt,
        specifications: run.specifications,
        cleanCompilations: run.cleanCompilations
      }))
      .reverse();
  }

  /** How many iterations were recorded and never given an outcome. */
  countUnfinishedIterations(): number {
    const row = this.database
      .prepare('SELECT COUNT(*) AS total FROM iterations WHERE outcome IS NULL')
      .get() as { total: number };

    return row.total;
  }

  /**
   * A short string that changes whenever the store does.
   *
   * @returns Something like `r39:39/i123:123/p486`. Its shape is not a contract; only its equality is.
   * @remarks
   * Both halves of it are needed. The highest identifier catches everything that was inserted — a
   * run, an iteration, a phase that has just been timed — and the counts of finished rows catch what
   * was *updated*, which is how an iteration ends: `outcome` is written into a row that already
   * existed, and no identifier moves when it happens. A token built from identifiers alone would
   * show a run advancing through its phases and then never notice it finishing.
   *
   * One query, three counts and three maxima, against tables that have their indexes. It is meant to
   * be asked once a second while somebody is watching, and to cost nothing when nothing is going on.
   */
  changeToken(): string {
    const row = this.database
      .prepare(
        `SELECT (SELECT COALESCE(MAX(id), 0) FROM runs) AS runs,
                (SELECT COUNT(*) FROM runs WHERE outcome IS NOT NULL) AS finishedRuns,
                (SELECT COALESCE(MAX(id), 0) FROM iterations) AS iterations,
                (SELECT COUNT(*) FROM iterations WHERE outcome IS NOT NULL) AS finishedIterations,
                (SELECT COALESCE(MAX(id), 0) FROM phase_timings) AS phases`
      )
      .get() as ChangeTokenRow;

    return `r${row.runs}:${row.finishedRuns}/i${row.iterations}:${row.finishedIterations}/p${row.phases}`;
  }

  /** Closes the store. */
  close(): void {
    this.database.close();
  }
}

type RunRow = {
  runId: number;
  specSet: string;
  serverExecutable: string;
  iterationLimit: number;
  generator: string;
  startedAt: number;
  endedAt: number | null;
  outcome: string | null;
  iterations: number;
  specifications: number;
  cleanCompilations: number;
  passed: number;
};

type IterationRow = {
  iterationId: number;
  specification: string;
  attempt: number;
  startedAt: number;
  endedAt: number | null;
  outcome: string | null;
  errorCount: number | null;
};

type PhaseRow = { phase: string; samples: number; total: number; failures: number };

type IterationPhaseRow = { phase: string; startedAt: number; duration: number; outcome: string };

type ChangeTokenRow = {
  runs: number;
  finishedRuns: number;
  iterations: number;
  finishedIterations: number;
  phases: number;
};

type SpecificationRow = { specification: string; runId: number; firstClean: number | null; passed: number };

/**
 * One optional filter value, as the pair of parameters `(? IS NULL OR column = ?)` needs.
 *
 * @remarks
 * Written once rather than at each call site, because the failure it prevents is silent: pass the
 * value once and every row matches, which reads as a filter that found everything rather than as a
 * filter that was never applied.
 */
function repeated(value: number | string | undefined): [number | string | null, number | string | null] {
  return [value ?? null, value ?? null];
}

function toRecordedRun(row: RunRow): RecordedRun {
  return {
    runId: row.runId,
    specSet: row.specSet,
    serverExecutable: row.serverExecutable,
    iterationLimit: row.iterationLimit,
    generator: row.generator,
    startedAt: row.startedAt,
    endedAt: row.endedAt ?? undefined,
    outcome: row.outcome ?? undefined,
    iterations: row.iterations,
    specifications: row.specifications,
    cleanCompilations: row.cleanCompilations,
    passed: row.passed
  };
}

function toRecordedIteration(row: IterationRow): RecordedIteration {
  return {
    iterationId: row.iterationId,
    specification: row.specification,
    attempt: row.attempt,
    startedAt: row.startedAt,
    endedAt: row.endedAt ?? undefined,
    outcome: row.outcome ?? undefined,
    errorCount: row.errorCount ?? undefined
  };
}

function toPhaseDuration(row: PhaseRow): PhaseDuration {
  return {
    phase: row.phase as LoopPhase,
    samples: row.samples,
    totalMilliseconds: row.total,
    meanMilliseconds: row.total / row.samples,
    failures: row.failures
  };
}

/** Folds one row per specification-run into one row per specification. */
function summariseSpecifications(rows: readonly SpecificationRow[]): SpecificationStatistics[] {
  const attemptsBySpecification = new Map<string, SpecificationRow[]>();

  for (const row of rows) {
    const recorded = attemptsBySpecification.get(row.specification) ?? [];

    recorded.push(row);
    attemptsBySpecification.set(row.specification, recorded);
  }

  return [...attemptsBySpecification].map(([specification, attempts]) => ({
    specification,
    attempts: attempts.length,
    cleanCompilations: attempts.filter((attempt) => attempt.firstClean !== null).length,
    passed: attempts.filter((attempt) => attempt.passed === 1).length,
    meanIterationsToCleanCompilation: meanFirstClean(attempts)
  }));
}

function meanFirstClean(attempts: readonly SpecificationRow[]): number | undefined {
  const clean = attempts
    .map((attempt) => attempt.firstClean)
    .filter((attempt): attempt is number => attempt !== null);

  if (clean.length === 0) {
    return undefined;
  }

  return clean.reduce((total, attempt) => total + attempt, 0) / clean.length;
}
