import type { Generator } from './generator.ts';
import type { McpServerConnection } from './mcpClient.ts';
import type { Specification } from './specification.ts';
import type { IterationId, IterationOutcome, RunId, Telemetry } from './telemetry.ts';
import { verify } from './verification.ts';

/** What running one specification came to. */
export type SpecificationResult = {
  readonly specification: string;
  /** How the last iteration ended, which is how the specification ended. */
  readonly outcome: IterationOutcome;
  readonly attempts: number;
  readonly detail: string;
};

/** Everything one specification needs to be run. */
export type LoopOptions = {
  readonly connection: McpServerConnection;
  readonly telemetry: Telemetry;
  readonly runId: RunId;
  readonly specification: Specification;
  readonly generator: Generator;
  /** How many attempts before giving up. */
  readonly iterationLimit: number;
};

/**
 * Runs one specification until it passes or the attempts run out.
 *
 * @remarks
 * The whole chain, on the user's decision of 2026-08-21: generate, write, compile, download, run,
 * and check the cell behaves. Stopping at "it compiles" would have measured syntax — a coordinator
 * that hands a piece to the wrong station compiles perfectly.
 *
 * Which failures continue and which stop is the only real decision in here, and it is not
 * symmetric:
 *
 * - **Compiler errors continue.** They are the input to the next attempt; that is the loop.
 * - **Wrong behaviour continues.** The code was valid and did the wrong thing, which is exactly
 *   what a generator should be given another go at.
 * - **A refusal stops, immediately.** The governance layer said no, and it will say no again. A
 *   loop that retried a refusal would be a loop hammering a closed door, and the door is closed on
 *   purpose.
 * - **A download failure stops.** It is an environment fault — no controller, wrong address, a
 *   licence — and nothing a different source would fix. Retrying would spend the whole budget
 *   proving the same thing five times.
 */
export async function runSpecification(options: LoopOptions): Promise<SpecificationResult> {
  const { specification, telemetry, runId, iterationLimit } = options;

  let previousErrors: readonly string[] = [];
  let last: SpecificationResult = {
    specification: specification.name,
    outcome: 'failed',
    attempts: 0,
    detail: 'the loop never ran an attempt'
  };

  for (let attempt = 1; attempt <= iterationLimit; attempt += 1) {
    const iterationId = telemetry.startIteration(runId, specification.name, attempt);
    const iteration = await runOneAttempt(options, iterationId, attempt, previousErrors);

    telemetry.finishIteration(iterationId, iteration.outcome, iteration.errorCount);

    last = {
      specification: specification.name,
      outcome: iteration.outcome,
      attempts: attempt,
      detail: iteration.detail
    };

    if (iteration.outcome === 'passed' || stopsTheLoop(iteration.outcome)) {
      return last;
    }

    previousErrors = iteration.errors;
  }

  return last;
}

/** Whether an outcome is one that another attempt could not improve on. */
function stopsTheLoop(outcome: IterationOutcome): boolean {
  return outcome === 'refused' || outcome === 'download-failed' || outcome === 'failed';
}

type AttemptResult = {
  readonly outcome: IterationOutcome;
  readonly detail: string;
  readonly errors: readonly string[];
  readonly errorCount: number;
};

/** One pass of the whole chain. */
async function runOneAttempt(
  options: LoopOptions,
  iterationId: IterationId,
  attempt: number,
  previousErrors: readonly string[]
): Promise<AttemptResult> {
  const { connection, telemetry, specification, generator } = options;

  try {
    const source = await telemetry.time(iterationId, 'generate', () =>
      generator.generate({ specification, attempt, previousErrors })
    );

    const written = await telemetry.time(iterationId, 'write', () =>
      connection.callTool('WriteScl', { softwarePath: specification.softwarePath, sclCode: source })
    );

    if (written.isError) {
      return failure(`WriteScl failed: ${written.text}`);
    }

    if (!wasApplied(written.payload)) {
      // Not a failure of the harness and not something to retry: the policy did not name this
      // target, and it will not name it on the next attempt either.
      return { outcome: 'refused', detail: written.text, errors: [], errorCount: 0 };
    }

    const compiled = await telemetry.time(iterationId, 'compile', () => compile(options));

    if (compiled.errorCount > 0) {
      return {
        outcome: 'compiler-errors',
        // The messages, not just how many. A count tells a reader that something is wrong and
        // nothing about what, which is the failure this repository's error model exists to prevent.
        detail: describeErrors(compiled.errors, compiled.errorCount),
        errors: compiled.errors,
        errorCount: compiled.errorCount
      };
    }

    const downloaded = await telemetry.time(iterationId, 'download', () => download(options));

    if (downloaded !== undefined) {
      return { outcome: 'download-failed', detail: downloaded, errors: [], errorCount: 0 };
    }

    const verified = await telemetry.time(iterationId, 'verify', () => verify(connection, specification));

    if (!verified.passed) {
      return { outcome: 'behaviour-failed', detail: verified.detail, errors: [], errorCount: 0 };
    }

    return { outcome: 'passed', detail: '', errors: [], errorCount: 0 };
  } catch (error) {
    // Anything that got this far is not something the loop can act on: a transport that died, a
    // generator that threw. Reported as itself rather than folded into one of the outcomes a
    // generator could fix, because a wrong category here would send the next attempt after the
    // wrong problem.
    return failure((error as Error).message);
  }
}

/** The first few compiler messages, and how many were left out. */
function describeErrors(errors: readonly string[], errorCount: number): string {
  const shown = errors.slice(0, 3).join(' | ');
  const hidden = errorCount - Math.min(errors.length, 3);

  if (shown.length === 0) {
    return `${errorCount} compiler error(s), none of which carried a message`;
  }

  return hidden > 0 ? `${shown} (and ${hidden} more)` : shown;
}

function failure(detail: string): AttemptResult {
  return { outcome: 'failed', detail, errors: [], errorCount: 0 };
}

type CompileResult = { readonly errorCount: number; readonly errors: readonly string[] };

/**
 * Compiles the program, with simulation support on and the hardware compiled after it.
 *
 * @remarks
 * The order is the one the server's own remarks were written in blood about, and getting it wrong
 * costs a download that blames the target instead of the project:
 *
 * 1. **Simulation support first.** The setting governs compilation, so blocks built without it stay
 *    unsimulatable however many times they are downloaded.
 * 2. **Then the software**, so the blocks are built with it on.
 * 3. **Then the hardware**, because turning the setting on invalidates the compiled hardware
 *    configuration, and downloading a stale one fails with "Loading of hardware configuration
 *    failed", which names neither the cause nor the fix.
 */
async function compile(options: LoopOptions): Promise<CompileResult> {
  const { connection, specification } = options;

  const enabled = await connection.callTool('EnableSimulationSupport');

  if (enabled.isError || !wasApplied(enabled.payload)) {
    return { errorCount: 1, errors: [`EnableSimulationSupport did not apply: ${enabled.text}`] };
  }

  const software = await connection.callTool('CompileSoftware', { softwarePath: specification.softwarePath });

  if (software.isError) {
    return { errorCount: 1, errors: [`CompileSoftware failed: ${software.text}`] };
  }

  const softwareErrors = readCompilerErrors(software.payload);

  if (softwareErrors.length > 0) {
    return { errorCount: softwareErrors.length, errors: softwareErrors };
  }

  const hardware = await connection.callTool('CompileHardware', { deviceItemPath: specification.softwarePath });

  if (hardware.isError) {
    return { errorCount: 1, errors: [`CompileHardware failed: ${hardware.text}`] };
  }

  return { errorCount: 0, errors: readCompilerErrors(hardware.payload) };
}

/**
 * Downloads to the virtual controller and puts it in RUN.
 *
 * @returns Nothing when it worked, or one line saying what stopped it.
 */
async function download(options: LoopOptions): Promise<string | undefined> {
  const { connection, specification } = options;

  const downloaded = await connection.callTool('DownloadToSimulation', {
    softwarePath: specification.softwarePath
  });

  if (downloaded.isError || !wasApplied(downloaded.payload)) {
    return `the download did not succeed: ${downloaded.text}`;
  }

  const started = await connection.callTool('StartSimulationInstance', {
    instanceName: specification.controller.name
  });

  if (started.isError || !wasApplied(started.payload)) {
    return `the controller did not start: ${started.text}`;
  }

  return undefined;
}

/** Whether a response says the work actually happened. */
function wasApplied(payload: unknown): boolean {
  return (payload as { meta?: { success?: unknown } } | undefined)?.meta?.success === true;
}

/** The compiler messages of a compile response, which are the answer rather than an error. */
function readCompilerErrors(payload: unknown): string[] {
  const messages = (payload as { messages?: unknown } | undefined)?.messages;

  if (!Array.isArray(messages)) {
    return [];
  }

  return messages.filter((message): message is string => typeof message === 'string');
}
