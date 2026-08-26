import { readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createStubGenerator, type Generator } from './generator.ts';
import { createApiSender, createModelGenerator } from './modelGenerator.ts';
import { runSpecification, type SpecificationResult } from './loop.ts';
import { McpServerConnection } from './mcpClient.ts';
import { parseOptions, type Options } from './options.ts';
import { passedEverySpecification, report, type Repetition } from './report.ts';
import { repositoryRoot, resolveServerExecutable } from './serverLocation.ts';
import { loadSpecification, type Specification } from './specification.ts';
import { Telemetry, type RunId } from './telemetry.ts';
import { findContractBreaches } from './toolContract.ts';

/**
 * How long to wait for TIA Portal to start.
 *
 * @remarks
 * Three minutes against a known forty-five seconds, and much shorter than the ten this connection
 * allows a compile or a download, because a slow Connect is almost never slow: **TIA asks for
 * Openness confirmation again every time the server executable is rebuilt, and blocks until somebody
 * answers.** Measured three times. Waiting ten minutes for that protects nothing and delays the one
 * piece of news that matters, which is "look at the screen".
 */
const ConnectTimeoutMilliseconds = 180_000;

/**
 * Runs the whole specification set and reports what happened.
 *
 * @remarks
 * The report carries its sample size, because the roadmap says a bare percentage is not a
 * measurement. "3 of 4 passed, n=4" is a sentence somebody can argue with; "75%" is not.
 */
async function main(): Promise<number> {
  const options = parseOptions(process.argv.slice(2));
  const specifications = loadAll(options.specDirectory);

  if (specifications.length === 0) {
    console.error(`No specifications in ${options.specDirectory}.`);

    return 1;
  }

  const executable = resolveServerExecutable();
  const telemetry = Telemetry.open(options.databasePath);
  const connection = await McpServerConnection.open({
    executable,
    ...(options.policyPath === undefined ? {} : { policyPath: options.policyPath }),

    // Named, not defaulted. The server resolves these relative to its own working directory, which
    // is wherever the harness was started from - so the first runs scattered an audit trail and
    // fifteen backup manifests into harness/.tia-mcp/, outside the rules that ignore them at the
    // repository root, and git offered to commit the lot. Everything a run produces belongs in one
    // place that is ignored once.
    auditPath: join(options.harnessRoot, 'audit.jsonl'),
    backupRoot: join(options.harnessRoot, 'backups'),

    // On, and written out at the end of every run. The protocol carries almost nothing about
    // why a tool failed, so with this off a failing download is one sentence with no state
    // behind it - which is what five runs of 'Connect to module failed' cost.
    serverLogging: 'stderr'
  });

  const session: Session = { connection, telemetry, executable, specifications, options };

  try {
    await prepare(session);

    const repetitions = await runRepetitions(session);

    report(repetitions, { repetitions: options.repetitions });

    return repetitions.every(passedEverySpecification) ? 0 : 1;
  } catch (error) {
    console.error(`The run stopped: ${(error as Error).message}`);
    console.error(connection.serverDiagnostics());

    return 1;
  } finally {
    writeFileSync(join(options.harnessRoot, 'server.log'), connection.serverDiagnostics(), 'utf8');

    // Disconnect before closing the transport, so TIA Portal is shut down by the thing that started
    // it. Killing the server process with a portal still open is what leaves the two-gigabyte
    // orphan holding the licence.
    await connection.callTool('Disconnect').catch(() => undefined);

    // Both, always. The server is a child process and the store holds a file handle; either left
    // behind is the same class of failure this project has chased through three layers already.
    await connection.close();
    telemetry.close();
  }
}

/** Everything one invocation carries from end to end, so no function needs five parameters. */
type Session = {
  readonly connection: McpServerConnection;
  readonly telemetry: Telemetry;
  /** Recorded with every run, so a number is attributable to a build of the server. */
  readonly executable: string;
  readonly specifications: readonly Specification[];
  readonly options: Options;
};

/**
 * Everything that is done once, however many repetitions follow.
 *
 * @remarks
 * Connecting to TIA Portal takes about forty-five seconds and the network mode may only be set
 * while no instance exists, so both belong outside the repetition loop. Doing them per repetition
 * would add a minute to each and measure the harness's startup instead of the loop.
 */
async function prepare(session: Session): Promise<void> {
  const { connection } = session;

  // Before anything is started, because it costs a second and the alternative costs a run: every
  // argument name the harness sends is checked against the server's own schemas. Two of them were
  // wrong the first time and both surfaced as "An error occurred", a minute and a half in.
  const breaches = await findContractBreaches(connection);

  if (breaches.length > 0) {
    throw new Error(`The harness and the server disagree: ${breaches.join(' | ')}`);
  }

  // Then connect: every project tool refuses until the server holds a portal, with "Connect to TIA
  // Portal before retrieving a project". A minute of startup, once per invocation.
  const connected = await connection.callTool('Connect', {}, ConnectTimeoutMilliseconds);

  if (connected.isError) {
    throw new Error(
      `Connect failed: ${connected.text}. If this timed out, TIA Portal is probably waiting for ` +
        'Openness confirmation on screen, which it asks for again whenever the server executable is rebuilt.'
    );
  }

  await useTcpIpNetworkMode(connection);
}

/**
 * Puts the PLCSIM runtime in TCP/IP mode, and checks the mode rather than the answer.
 *
 * @remarks
 * Before any instance exists, which is the whole constraint on this call. The mode it reports is
 * checked and not just the fact that it answered: over Softbus a controller is reachable only by
 * PLCSIM itself, and the download then fails with "Connect to module failed" several minutes later.
 */
async function useTcpIpNetworkMode(connection: McpServerConnection): Promise<void> {
  const networked = await connection.callTool('UseTcpIpNetworkMode');

  requireApplied(networked, 'UseTcpIpNetworkMode');

  const mode = (networked.payload as { meta?: { networkMode?: unknown } } | undefined)?.meta?.networkMode;

  if (typeof mode !== 'string' || !mode.startsWith('TCPIP')) {
    throw new Error(`The PLCSIM runtime is in ${String(mode)} mode, not TCP/IP. A download cannot reach a controller.`);
  }
}

/**
 * Runs the specification set as many times as was asked for.
 *
 * @remarks
 * Each repetition is a separate run in the store and starts from a project nothing has written to,
 * because that is what makes repetitions comparable with each other: a second pass over a project
 * the first one already filled with blocks is measuring something else entirely.
 */
async function runRepetitions(session: Session): Promise<Repetition[]> {
  const generator = createGenerator(session);
  const repetitions: Repetition[] = [];

  for (let index = 1; index <= session.options.repetitions; index += 1) {
    if (session.options.repetitions > 1) {
      console.error(`=== repetition ${index} of ${session.options.repetitions}`);
    }

    repetitions.push(await runOneRepetition(session, generator, index));
  }

  return repetitions;
}

/**
 * Builds the generator the run was asked for.
 *
 * @remarks
 * The API key is read here, at the start, rather than at the first generation: a batch of ten
 * repetitions that dies forty minutes in because a key was never set is a batch nobody gets back.
 */
function createGenerator(session: Session): Generator {
  if (session.options.generator === 'model') {
    return createModelGenerator(createApiSender());
  }

  return createStubGenerator(session.connection, repositoryRoot());
}

/** One pass over the set, recorded as its own run. */
async function runOneRepetition(
  session: Session,
  generator: Generator,
  index: number
): Promise<Repetition> {
  const { telemetry, options } = session;

  const runId = telemetry.startRun({
    specSet: options.specDirectory,
    serverExecutable: session.executable,
    iterationLimit: options.iterationLimit,
    generator: generator.name
  });

  try {
    const results = await runTheSet(session, generator, runId);

    telemetry.finishRun(runId, results.every((result) => result.outcome === 'passed') ? 'passed' : 'failed');

    return { index, results };
  } catch (error) {
    // Marked before rethrowing, so a repetition killed by a broken transport is a failed run in the
    // store rather than one that never ended. The gate counts complete runs, and an unfinished one
    // it could not see would make the count wrong in the direction that opens a workshop door.
    telemetry.finishRun(runId, 'failed');

    throw error;
  }
}

/** Opens a project nothing has written to, then runs every specification against it. */
async function runTheSet(
  session: Session,
  generator: Generator,
  runId: RunId
): Promise<SpecificationResult[]> {
  await closeAnyOpenProject(session.connection);
  await openTheProject(session.connection, session.options);

  const results: SpecificationResult[] = [];

  for (const specification of session.specifications) {
    results.push(await runOne(session, runId, specification, generator));
  }

  return results;
}

/**
 * Closes whatever is open, and treats "nothing was open" as success.
 *
 * @remarks
 * The first repetition has nothing to close and every later one does, and asking which is which is
 * how an off-by-one becomes a run that dies on "Another project is already open" forty minutes into
 * an unattended batch. Refusing to close a project that is not there is not an error worth having.
 */
async function closeAnyOpenProject(connection: McpServerConnection): Promise<void> {
  await connection.callTool('CloseProject').catch(() => undefined);
}

/**
 * Opens the project, retrieving it from an archive first when that is what was given.
 *
 * @remarks
 * Retrieving into a fresh directory per run is not tidiness: a project the previous run wrote
 * blocks into already holds them, so a "first attempt" would be measured against a program that
 * was already there. RetrieveProject refuses an existing target, which is what makes that
 * impossible rather than merely discouraged.
 */
async function openTheProject(connection: McpServerConnection, options: Options): Promise<void> {
  if (options.archivePath !== undefined) {
    const target = join(options.workingRoot, `run-${Date.now()}`);
    const retrieved = await connection.callTool('RetrieveProject', {
      archivePath: options.archivePath,
      targetDirectory: target
    });

    if (retrieved.isError) {
      throw new Error(`RetrieveProject failed: ${retrieved.text}`);
    }

    // RetrieveProject opens what it retrieved, so there is nothing more to do.
    console.error(`Retrieved a fresh project into ${target}`);

    return;
  }

  requireApplied(await connection.callTool('OpenProject', { path: options.projectPath }), 'OpenProject');
}

async function runOne(
  session: Session,
  runId: RunId,
  specification: Specification,
  generator: Generator
): Promise<SpecificationResult> {
  const { connection, telemetry, options } = session;
  const { controller } = specification;

  console.error(`--- ${specification.name}`);

  await createTheController(connection, controller);

  try {
    return await runSpecification({
      connection,
      telemetry,
      runId,
      specification,
      generator,
      iterationLimit: options.iterationLimit,
      resetSession: () => resetSession(session, controller)
    });
  } finally {
    // Reported rather than thrown: losing the result of the specification behind a cleanup error
    // would waste the minute it took to produce. But a controller left registered holds the
    // address the next specification needs, so silence is not an option either.
    const removed = await connection.callTool('DeleteSimulationInstance', { instanceName: controller.name });

    if (removed.isError) {
      console.error(`Warning: '${controller.name}' was not removed: ${removed.text}`);
    }
  }
}

/** Creates the virtual controller one specification runs on. */
async function createTheController(
  connection: McpServerConnection,
  controller: Specification['controller']
): Promise<void> {
  requireApplied(
    await connection.callTool('CreateSimulationInstance', {
      instanceName: controller.name,
      ipAddress: controller.address,
      subnetMask: controller.subnetMask,
      // Not optional in practice. Without it the controller is an unspecified one, the hardware
      // download still succeeds, and then the text libraries fail with 'InvalidAID' — which is how
      // the first run that got this far died.
      cpuType: controller.cpuType
    }),
    'CreateSimulationInstance'
  );
}

/**
 * Reopens the project and the controller, so the next attempt can download at all.
 *
 * @remarks
 * Both halves are needed and neither is enough: a controller recreated inside the same open
 * project fails in the text libraries, and a project reopened around a controller that already
 * holds a program fails to connect to it. Measured, not reasoned - see `LoopOptions.resetSession`.
 */
async function resetSession(session: Session, controller: Specification['controller']): Promise<void> {
  const { connection } = session;

  console.error('  the attempt downloaded, so the next one starts from a fresh project and controller');

  const removed = await connection.callTool('DeleteSimulationInstance', { instanceName: controller.name });

  if (removed.isError) {
    throw new Error(`The controller could not be removed before retrying: ${removed.text}`);
  }

  await closeAnyOpenProject(connection);
  await openTheProject(connection, session.options);
  await createTheController(connection, controller);
}

/** Throws unless a tool actually did what was asked, refusals included. */
function requireApplied(result: { isError: boolean; text: string; payload: unknown }, tool: string): void {
  if (result.isError) {
    throw new Error(`${tool} failed: ${result.text}`);
  }

  const meta = (result.payload as { meta?: { success?: unknown } } | undefined)?.meta;

  if (meta?.success !== true) {
    throw new Error(`${tool} did not apply: ${result.text}`);
  }
}

function loadAll(directory: string): Specification[] {
  return readdirSync(directory)
    .filter((name) => name.endsWith('.json'))
    .sort()
    .map((name) => loadSpecification(join(directory, name)));
}

process.exitCode = await main();
