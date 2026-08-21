import { readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { createStubGenerator } from './generator.ts';
import { runSpecification, type SpecificationResult } from './loop.ts';
import { McpServerConnection } from './mcpClient.ts';
import { repositoryRoot, resolveServerExecutable } from './serverLocation.ts';
import { loadSpecification, type Specification } from './specification.ts';
import { Telemetry } from './telemetry.ts';
import { findContractBreaches } from './toolContract.ts';

/** What the CLI was asked to do. */
type Options = {
  /** A project to open, or an archive to retrieve one from. Exactly one of the two. */
  readonly projectPath: string | undefined;
  readonly archivePath: string | undefined;
  /** Where everything a run produces goes: projects, backups, the audit trail, the measurements. */
  readonly harnessRoot: string;
  readonly workingRoot: string;
  readonly specDirectory: string;
  readonly databasePath: string;
  readonly policyPath: string | undefined;
  readonly iterationLimit: number;
};

const DefaultIterationLimit = 3;

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
    backupRoot: join(options.harnessRoot, 'backups')
  });

  const runId = telemetry.startRun({
    specSet: options.specDirectory,
    serverExecutable: executable,
    iterationLimit: options.iterationLimit,
    generator: 'stub'
  });

  try {
    const results = await runEverything(connection, telemetry, runId, specifications, options);
    const passed = results.filter((result) => result.outcome === 'passed').length;

    telemetry.finishRun(runId, passed === results.length ? 'passed' : 'failed');

    report(results, telemetry.summarise(runId).iterations);

    return passed === results.length ? 0 : 1;
  } catch (error) {
    telemetry.finishRun(runId, 'failed');
    console.error(`The run stopped: ${(error as Error).message}`);
    console.error(connection.serverDiagnostics());

    return 1;
  } finally {
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

/**
 * Opens the project once, then runs each specification on its own controller.
 *
 * @remarks
 * The project is opened once and the controller is created per specification. A controller is tied
 * to an address that has to match the CPU in the project, so two of them alive at once would be two
 * machines claiming one address — and the download would reach whichever answered first.
 */
async function runEverything(
  connection: McpServerConnection,
  telemetry: Telemetry,
  runId: ReturnType<Telemetry['startRun']>,
  specifications: readonly Specification[],
  options: Options
): Promise<SpecificationResult[]> {
  // Before anything is started, because it costs a second and the alternative costs a run: every
  // argument name the harness sends is checked against the server's own schemas. Two of them were
  // wrong the first time and both surfaced as "An error occurred", a minute and a half in.
  const breaches = await findContractBreaches(connection);

  if (breaches.length > 0) {
    throw new Error(`The harness and the server disagree: ${breaches.join(' | ')}`);
  }

  // Then connect: every project tool refuses until the server holds a portal, with "Connect to TIA
  // Portal before retrieving a project". A minute of startup, once per run.
  const connected = await connection.callTool('Connect', {}, ConnectTimeoutMilliseconds);

  if (connected.isError) {
    throw new Error(
      `Connect failed: ${connected.text}. If this timed out, TIA Portal is probably waiting for ` +
        'Openness confirmation on screen, which it asks for again whenever the server executable is rebuilt.'
    );
  }

  await openTheProject(connection, options);

  // Before any instance exists, which is the whole constraint on this call.
  // Before any instance exists, which is the whole constraint on this call. The mode it reports is
  // checked and not just the fact that it answered: over Softbus a controller is reachable only by
  // PLCSIM itself, and the download then fails with "Connect to module failed" several minutes later.
  const networked = await connection.callTool('UseTcpIpNetworkMode');

  requireApplied(networked, 'UseTcpIpNetworkMode');

  const mode = (networked.payload as { meta?: { networkMode?: unknown } } | undefined)?.meta?.networkMode;

  if (typeof mode !== 'string' || !mode.startsWith('TCPIP')) {
    throw new Error(`The PLCSIM runtime is in ${String(mode)} mode, not TCP/IP. A download cannot reach a controller.`);
  }

  const generator = createStubGenerator(connection, repositoryRoot());
  const results: SpecificationResult[] = [];

  for (const specification of specifications) {
    results.push(await runOne(connection, telemetry, runId, specification, generator, options));
  }

  return results;
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
  connection: McpServerConnection,
  telemetry: Telemetry,
  runId: ReturnType<Telemetry['startRun']>,
  specification: Specification,
  generator: ReturnType<typeof createStubGenerator>,
  options: Options
): Promise<SpecificationResult> {
  const { controller } = specification;

  console.error(`--- ${specification.name}`);

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

  try {
    return await runSpecification({
      connection,
      telemetry,
      runId,
      specification,
      generator,
      iterationLimit: options.iterationLimit
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

/** Prints the result, with the sample size attached. */
function report(results: readonly SpecificationResult[], iterations: number): void {
  console.log('');

  for (const result of results) {
    const detail = result.detail.length > 0 ? ` — ${result.detail}` : '';

    console.log(`${result.outcome === 'passed' ? 'PASS' : 'FAIL'}  ${result.specification}  ` +
      `(${result.attempts} attempt(s), ${result.outcome})${detail}`);
  }

  const passed = results.filter((result) => result.outcome === 'passed').length;

  console.log('');
  console.log(`${passed} of ${results.length} specification(s) passed on a simulated CPU, ` +
    `n=${results.length}, ${iterations} iteration(s) in total.`);
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

/**
 * Reads the command line.
 *
 * @remarks
 * Neither `--project` nor `--archive` has a default and neither can have one: a TIA project is a
 * machine's file, and the whole repository refuses hardcoded paths. `--archive` is the one to reach
 * for, since it gives every run a project nothing has written to yet.
 */
function parseOptions(args: readonly string[]): Options {
  const values = new Map<string, string>();

  for (let index = 0; index < args.length; index += 2) {
    const flag = args[index];
    const value = args[index + 1];

    if (flag === undefined || value === undefined || !flag.startsWith('--')) {
      throw new Error(`Bad arguments near '${flag ?? ''}'. Every flag takes a value.`);
    }

    values.set(flag, value);
  }

  const projectPath = values.get('--project');
  const archivePath = values.get('--archive');

  if ((projectPath === undefined) === (archivePath === undefined)) {
    throw new Error(
      'Usage: node src/run.ts (--archive <path to a .zap20> | --project <path to a .ap20>) ' +
        '[--specs <dir>] [--database <file>] [--policy <file>] [--limit <n>] [--out <dir>]. ' +
        'Give exactly one of --archive and --project: retrieving gives each run a project nothing ' +
        'has written to yet, which is what makes a first attempt a first attempt.'
    );
  }

  // Every path is made absolute here, once. Openness refuses a relative one outright — "The
  // argument 'sourcePath' cannot be a relative path" is how the first real run died — and the
  // server is a separate process anyway, so a path relative to this one's working directory would
  // mean something else on the other side even where it was accepted.
  const harnessRoot = resolve(values.get('--out') ?? join(repositoryRoot(), '.tia-mcp', 'harness'));

  return {
    projectPath: absoluteOrUndefined(projectPath),
    archivePath: absoluteOrUndefined(archivePath),
    harnessRoot,
    workingRoot: resolve(values.get('--work') ?? join(harnessRoot, 'projects')),
    specDirectory: resolve(values.get('--specs') ?? join(repositoryRoot(), 'harness', 'specs')),
    databasePath: resolve(values.get('--database') ?? join(harnessRoot, 'metrics.db')),
    policyPath: absoluteOrUndefined(values.get('--policy')),
    iterationLimit: Number(values.get('--limit') ?? DefaultIterationLimit)
  };
}

function absoluteOrUndefined(path: string | undefined): string | undefined {
  return path === undefined ? undefined : resolve(path);
}

process.exitCode = await main();
