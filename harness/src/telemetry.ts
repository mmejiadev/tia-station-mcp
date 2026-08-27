import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { verifySchemaVersion } from './schemaVersion.ts';

/**
 * The phases of one iteration of the loop.
 *
 * @remarks
 * The roadmap wrote this as generate, write, compile, read, fix. Two of those are not phases here.
 * Reading the errors is not a step: the compile response carries them. And fixing is not a step
 * either — it is the next iteration's generate, given the errors — so counting it separately would
 * report the same work twice.
 *
 * Download and verify are new, on the user's decision of 2026-08-21: the number the roadmap asks
 * for is what fraction of specifications passes on a simulated CPU, and only these two can answer
 * that. They also cost most of the minute an iteration takes.
 */
export type LoopPhase = 'generate' | 'write' | 'compile' | 'download' | 'verify';

/** How one iteration ended. */
export type IterationOutcome =
  /** It compiled, downloaded, ran, and the cell behaved as the specification says. */
  | 'passed'
  /** It did not compile. The errors are the input to the next iteration. */
  | 'compiler-errors'
  /** It compiled but could not be downloaded or would not reach RUN. */
  | 'download-failed'
  /** It ran and did not do what the specification says. */
  | 'behaviour-failed'
  /** The governance layer refused a write. Not a failure: the system working. */
  | 'refused'
  /** Something broke that the loop cannot act on. */
  | 'failed';

/**
 * The outcomes that mean the compiler accepted the SCL.
 *
 * @remarks
 * Listed rather than inferred from what is not a compiler error, and the difference matters: a
 * refused write and a broken transport never reached the compiler either, and counting them as
 * clean compilations would report a rate that rises as the harness breaks. Exported because the
 * dashboard API answers the same question about a specification and must answer it the same way.
 */
export const CleanCompilationOutcomes: readonly IterationOutcome[] = ['passed', 'download-failed', 'behaviour-failed'];

/** How a whole run of a specification ended. */
export type RunOutcome = 'passed' | 'exhausted' | 'failed';

/**
 * Identifiers are their own types, not bare numbers.
 *
 * @remarks
 * The same reason `PlanId` is its own type on the C# side: passing an iteration id where a run id
 * belongs is exactly the class of mistake that should be impossible, and the compiler makes it
 * impossible for nothing. The brand is a type, so it vanishes when Node strips the types.
 */
export type RunId = number & { readonly brand: 'RunId' };

/**
 * What one generation actually cost, as the API reported it.
 *
 * @remarks
 * Measured, not inferred. Phase 3 closed with a cost per generation estimated from token counts
 * nobody had counted, and `STATUS.md` says plainly that the first thing to do with a key is record
 * what it really costs and stop guessing. Every response carries these four numbers; this is where
 * they land.
 *
 * There is no cost field, here or in the store. A price is a fact about a day, and one written into
 * a row is wrong from the next price change onward with nothing to reveal it. Tokens are a fact
 * about the request, so the cost is computed where it is read - see `estimateCost`.
 */
export type TokenUsage = {
  /** Which model was asked, so a cost is attributable to one rather than to an average. */
  readonly model: string;
  readonly inputTokens: number;
  readonly outputTokens: number;
  /** Tokens written into the prompt cache, billed above the input rate. Zero until caching is used. */
  readonly cacheCreationTokens: number;
  /** Tokens served from the prompt cache, billed far below it. Zero until caching is used. */
  readonly cacheReadTokens: number;
};

/** Identifies one iteration within a run. */
export type IterationId = number & { readonly brand: 'IterationId' };

/** What is being measured, recorded once per run so a number can be traced back to it. */
export type RunContext = {
  /** Which specification set was run. */
  readonly specSet: string;
  /** The server the run went through, so a number is attributable to a build. */
  readonly serverExecutable: string;
  /** How many iterations one specification is allowed before the loop gives up. */
  readonly iterationLimit: number;
  /** What produced the SCL: a model name, or 'stub' when nothing was generated. */
  readonly generator: string;
};

/**
 * What a run added up to.
 *
 * @remarks
 * The counts are a map keyed by outcome rather than one field per outcome, and that is not a style
 * choice. The first version summed a fixed list of CASE expressions, so an outcome added later
 * would have been counted by nothing and silently missing from every report — the same silent
 * default the governance layer forbids. Grouping cannot lose a category it has never heard of.
 */
export type RunSummary = {
  readonly runId: RunId;
  readonly iterations: number;
  readonly counts: Readonly<Record<string, number>>;
};

/**
 * What one run added up to, in the terms the workshop gate is written in.
 *
 * @remarks
 * Declared here rather than with the gate because it describes what this store holds. The gate is
 * one reader of it; the dashboard of phase 4 will be another.
 */
export type RunStatistics = {
  readonly runId: number;
  /** Undefined when the run never ended, which is a fact the gate is required to notice. */
  readonly outcome: string | undefined;
  readonly startedAt: number;
  /** How many specifications the run attempted. */
  readonly specifications: number;
  /** How many of them reached a clean compilation at least once. */
  readonly cleanCompilations: number;
};

/**
 * Where the harness records what happened, so the phase's deliverable is a measurement rather than
 * an impression.
 *
 * @remarks
 * SQLite through `node:sqlite`, which ships with Node. That is the whole reason it was chosen over
 * `better-sqlite3`: a native module has to be compiled, and a harness that needs a C++ toolchain
 * before it can record a number is a harness that does not get run on the machine that has TIA
 * Portal on it.
 *
 * Every timestamp is epoch milliseconds. Not a formatted string: these get subtracted, sorted and
 * compared across runs, and a local-time string does none of those correctly twice a year.
 */
export class Telemetry {
  private readonly database: DatabaseSync;

  private constructor(database: DatabaseSync) {
    this.database = database;
  }

  /**
   * Opens the store, creating it and its schema if they are not there.
   *
   * @param path A file path, or ':memory:' for a store that is thrown away.
   * @returns The store, ready to record.
   */
  static open(path: string): Telemetry {
    if (path !== ':memory:') {
      mkdirSync(dirname(path), { recursive: true });
    }

    const database = new DatabaseSync(path);

    // Off by default in SQLite, which means a foreign key would document a relationship without
    // enforcing it — and an orphan iteration is a measurement that belongs to no run.
    database.exec('PRAGMA foreign_keys = ON');

    if (path !== ':memory:') {
      // So the dashboard of phase 4 can read while a run is still writing. On :memory: it is
      // meaningless and SQLite would refuse it.
      database.exec('PRAGMA journal_mode = WAL');
    }

    const telemetry = new Telemetry(database);

    try {
      telemetry.createSchema();
    } catch (error) {
      // A store that refuses to open must not leave its handle behind. On Windows the file stays
      // locked, so the next thing to touch it fails with a permission error that says nothing about
      // the schema mismatch that actually happened — found by the test for that mismatch, which
      // could not delete its own temporary directory afterwards.
      telemetry.close();

      throw error;
    }

    return telemetry;
  }

  /** Records the start of a run and returns its identifier. */
  startRun(context: RunContext, startedAt: number = Date.now()): RunId {
    const inserted = this.database
      .prepare(
        `INSERT INTO runs (spec_set, server_executable, iteration_limit, generator, started_at)
         VALUES (?, ?, ?, ?, ?)`
      )
      .run(context.specSet, context.serverExecutable, context.iterationLimit, context.generator, startedAt);

    return Number(inserted.lastInsertRowid) as RunId;
  }

  /** Records how a run ended. */
  finishRun(runId: RunId, outcome: RunOutcome, endedAt: number = Date.now()): void {
    this.database.prepare('UPDATE runs SET outcome = ?, ended_at = ? WHERE id = ?').run(outcome, endedAt, runId);
  }

  /**
   * Records the start of one iteration.
   *
   * @param runId The run it belongs to.
   * @param specification Which specification is being attempted.
   * @param attempt Which attempt this is, counting from one.
   * @returns The iteration's identifier.
   */
  startIteration(runId: RunId, specification: string, attempt: number, startedAt: number = Date.now()): IterationId {
    const inserted = this.database
      .prepare(
        `INSERT INTO iterations (run_id, specification, attempt, started_at)
         VALUES (?, ?, ?, ?)`
      )
      .run(runId, specification, attempt, startedAt);

    return Number(inserted.lastInsertRowid) as IterationId;
  }

  /**
   * Records how an iteration ended.
   *
   * @param iterationId The iteration.
   * @param outcome How it ended.
   * @param errorCount How many compiler errors it produced; zero for any other outcome.
   */
  finishIteration(
    iterationId: IterationId,
    outcome: IterationOutcome,
    errorCount: number,
    endedAt: number = Date.now()
  ): void {
    this.database
      .prepare('UPDATE iterations SET outcome = ?, error_count = ?, ended_at = ? WHERE id = ?')
      .run(outcome, errorCount, endedAt, iterationId);
  }

  /**
   * Records what one generation cost the account.
   *
   * @param iterationId The iteration whose generate phase this was.
   * @param usage What the API reported.
   * @remarks
   * A row per generation rather than a running total on the run, because the question that gets
   * asked of it is a per-attempt one: a specification that passes on the third try paid for three
   * generations, and a single total would hide which of the three was the expensive one.
   *
   * Only a model generator has anything to record. A stub run writes no rows here, which is honest:
   * it did not cost anything, rather than costing something nobody measured.
   */
  recordUsage(iterationId: IterationId, usage: TokenUsage, recordedAt: number = Date.now()): void {
    this.database
      .prepare(
        `INSERT INTO token_usage (iteration_id, model, input_tokens, output_tokens,
                                  cache_creation_tokens, cache_read_tokens, recorded_at)
         VALUES (?, ?, ?, ?, ?, ?, ?)`
      )
      .run(
        iterationId,
        usage.model,
        usage.inputTokens,
        usage.outputTokens,
        usage.cacheCreationTokens,
        usage.cacheReadTokens,
        recordedAt
      );
  }

  /**
   * Every generation a run paid for, one row per attempt, oldest first.
   *
   * @remarks
   * The rows, not a sum. Tokens of two different models added together produce a number that cannot
   * be priced at all, and comparing two models is exactly what `--model` exists for.
   */
  usageOfRun(runId: RunId): TokenUsage[] {
    return this.database
      .prepare(
        `SELECT u.model AS model, u.input_tokens AS inputTokens, u.output_tokens AS outputTokens,
                u.cache_creation_tokens AS cacheCreationTokens,
                u.cache_read_tokens AS cacheReadTokens
         FROM token_usage u JOIN iterations i ON i.id = u.iteration_id
         WHERE i.run_id = ? ORDER BY u.id`
      )
      .all(runId) as TokenUsage[];
  }

  /**
   * Times one phase and records it, whether it succeeded or threw.
   *
   * @param iterationId The iteration the phase belongs to.
   * @param phase Which phase.
   * @param work The phase itself.
   * @returns Whatever the phase returned.
   * @remarks
   * The timing is recorded in a finally block on purpose. A phase that threw is the one whose
   * duration is most worth having — "the compile took ninety seconds and then failed" is a
   * different problem from "the compile failed immediately" — and a version that recorded only on
   * success would lose exactly those.
   */
  async time<T>(iterationId: IterationId, phase: LoopPhase, work: () => Promise<T>): Promise<T> {
    const startedAt = Date.now();
    let failed = false;

    try {
      return await work();
    } catch (error) {
      failed = true;
      throw error;
    } finally {
      this.recordPhase(iterationId, phase, startedAt, Date.now(), failed ? 'failed' : 'ok');
    }
  }

  /** Adds up a run, for a report that has to carry its sample size. */
  summarise(runId: RunId): RunSummary {
    const rows = this.database
      .prepare(
        `SELECT COALESCE(outcome, 'unfinished') AS outcome, COUNT(*) AS total
         FROM iterations WHERE run_id = ? GROUP BY outcome`
      )
      .all(runId) as { outcome: string; total: number }[];

    const counts: Record<string, number> = {};
    let iterations = 0;

    for (const row of rows) {
      // 'unfinished' is a real category, not a gap: an iteration with no outcome is one that was
      // interrupted, and a run that crashed halfway must not report fewer iterations than it ran.
      counts[row.outcome] = row.total;
      iterations += row.total;
    }

    return { runId, iterations, counts };
  }

  /** How long each phase of an iteration took, in the order they ran. */
  phasesOf(iterationId: IterationId): { phase: LoopPhase; durationMilliseconds: number; outcome: string }[] {
    const rows = this.database
      .prepare(
        `SELECT phase, ended_at - started_at AS duration, outcome
         FROM phase_timings WHERE iteration_id = ? ORDER BY id`
      )
      .all(iterationId) as { phase: string; duration: number; outcome: string }[];

    return rows.map((row) => ({
      phase: row.phase as LoopPhase,
      durationMilliseconds: row.duration,
      outcome: row.outcome
    }));
  }

  /**
   * Every run, oldest first, with what it attempted and how much of it compiled.
   *
   * @remarks
   * Which iteration outcomes mean "compiled" comes from {@link CleanCompilationOutcomes} and goes
   * into the query as parameters, so the one place that decides what counts as a clean compilation
   * is a list a reader can find rather than a literal buried in SQL.
   */
  runStatistics(): RunStatistics[] {
    const placeholders = CleanCompilationOutcomes.map(() => '?').join(', ');
    const rows = this.database
      .prepare(
        `SELECT r.id AS runId, r.outcome AS outcome, r.started_at AS startedAt,
                COUNT(DISTINCT i.specification) AS specifications,
                COUNT(DISTINCT CASE WHEN i.outcome IN (${placeholders})
                                    THEN i.specification END) AS cleanCompilations
         FROM runs r LEFT JOIN iterations i ON i.run_id = r.id
         GROUP BY r.id ORDER BY r.id`
      )
      .all(...CleanCompilationOutcomes) as {
      runId: number;
      outcome: string | null;
      startedAt: number;
      specifications: number;
      cleanCompilations: number;
    }[];

    return rows.map((row) => ({
      runId: row.runId,
      outcome: row.outcome ?? undefined,
      startedAt: row.startedAt,
      specifications: row.specifications,
      cleanCompilations: row.cleanCompilations
    }));
  }

  /** How many iterations were recorded and never given an outcome. */
  countUnfinishedIterations(): number {
    const row = this.database
      .prepare('SELECT COUNT(*) AS total FROM iterations WHERE outcome IS NULL')
      .get() as { total: number };

    return row.total;
  }

  /** Closes the store. */
  close(): void {
    this.database.close();
  }

  private recordPhase(
    iterationId: IterationId,
    phase: LoopPhase,
    startedAt: number,
    endedAt: number,
    outcome: 'ok' | 'failed'
  ): void {
    this.database
      .prepare(
        `INSERT INTO phase_timings (iteration_id, phase, started_at, ended_at, outcome)
         VALUES (?, ?, ?, ?, ?)`
      )
      .run(iterationId, phase, startedAt, endedAt, outcome);
  }

  /**
   * Creates the schema, or refuses to open a store written by a different version of it.
   *
   * @remarks
   * The tables are created if they are absent, and the version is then checked by the one function
   * that both writers and readers of this store go through. See {@link verifySchemaVersion}.
   */
  private createSchema(): void {
    this.database.exec(`
      CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL);

      CREATE TABLE IF NOT EXISTS runs (
        id                INTEGER PRIMARY KEY,
        spec_set          TEXT    NOT NULL,
        server_executable TEXT    NOT NULL,
        iteration_limit   INTEGER NOT NULL,
        generator         TEXT    NOT NULL,
        started_at        INTEGER NOT NULL,
        ended_at          INTEGER,
        outcome           TEXT
      );

      CREATE TABLE IF NOT EXISTS iterations (
        id            INTEGER PRIMARY KEY,
        run_id        INTEGER NOT NULL REFERENCES runs (id),
        specification TEXT    NOT NULL,
        attempt       INTEGER NOT NULL,
        started_at    INTEGER NOT NULL,
        ended_at      INTEGER,
        outcome       TEXT,
        error_count   INTEGER
      );

      CREATE TABLE IF NOT EXISTS phase_timings (
        id           INTEGER PRIMARY KEY,
        iteration_id INTEGER NOT NULL REFERENCES iterations (id),
        phase        TEXT    NOT NULL,
        started_at   INTEGER NOT NULL,
        ended_at     INTEGER NOT NULL,
        outcome      TEXT    NOT NULL
      );

      CREATE INDEX IF NOT EXISTS iterations_by_run ON iterations (run_id);
      CREATE TABLE IF NOT EXISTS token_usage (
        id                    INTEGER PRIMARY KEY,
        iteration_id          INTEGER NOT NULL REFERENCES iterations (id),
        model                 TEXT    NOT NULL,
        input_tokens          INTEGER NOT NULL,
        output_tokens         INTEGER NOT NULL,
        cache_creation_tokens INTEGER NOT NULL,
        cache_read_tokens     INTEGER NOT NULL,
        recorded_at           INTEGER NOT NULL
      );

      CREATE INDEX IF NOT EXISTS phase_timings_by_iteration ON phase_timings (iteration_id);
      CREATE INDEX IF NOT EXISTS token_usage_by_iteration ON token_usage (iteration_id);
    `);

    verifySchemaVersion(this.database);
  }
}
