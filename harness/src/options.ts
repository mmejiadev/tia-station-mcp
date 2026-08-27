import { join, resolve } from 'node:path';
import { DefaultModel } from './modelGenerator.ts';
import { repositoryRoot } from './serverLocation.ts';

/**
 * How many attempts one specification gets before the loop gives up.
 *
 * @remarks
 * Three, and the number is a judgement: enough for a generator to be given the compiler's errors
 * and try again, few enough that a specification which cannot be written does not eat a batch.
 */
export const DefaultIterationLimit = 3;

/** One repetition, so a plain run stays a plain run and a measurement asks for more. */
const DefaultRepetitions = 1;

/**
 * Which generator produces the SCL.
 *
 * @remarks
 * 'stub' expands the repository's own patterns and proves nothing about generation - it is how the
 * loop itself is measured. 'model' is the one the phase exists for. Naming them in one type keeps a
 * typo on the command line from silently selecting the wrong one.
 */
export type GeneratorChoice = 'stub' | 'model';

/** What the CLI was asked to do. */
export type Options = {
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
  /**
   * How many times to run the whole specification set.
   *
   * @remarks
   * One run of a set answers 'did it pass', which is not what this phase is for. A rate needs
   * repetitions, and the roadmap is explicit that a number without its sample size is not a
   * measurement. Each repetition is its own run in the store, so the workshop gate's count of
   * complete runs means what it says.
   */
  readonly repetitions: number;
  /** Which generator writes the SCL: the repository's own patterns, or a model. */
  readonly generator: GeneratorChoice;
  /**
   * Which model writes the SCL, when one does.
   *
   * @remarks
   * A flag rather than a constant, because the two things a run is for want different answers. A
   * published measurement should name the model somebody chose; getting the loop to run at all
   * takes a great many attempts with nothing to learn from an expensive one, and the cheapest
   * model costs about a fifth of the default per generation.
   *
   * It is recorded with the run either way, so a number never has to be traced back to a flag
   * somebody remembers passing.
   */
  readonly model: string;
};

/**
 * Reads a command line written as `--flag value` pairs.
 *
 * @param args The arguments, without the executable and the script.
 * @param usage What to tell the caller when they are malformed.
 * @returns Every flag and its value.
 * @remarks
 * Shared by the three entry points rather than written three times. A flag with no value is refused
 * here instead of read as an empty string: `--database` at the end of a line would otherwise open a
 * store at the current directory and report measurements from a file nobody meant.
 */
export function parseFlags(args: readonly string[], usage: string): Map<string, string> {
  const values = new Map<string, string>();

  for (let index = 0; index < args.length; index += 2) {
    const flag = args[index];
    const value = args[index + 1];

    if (flag === undefined || value === undefined || !flag.startsWith('--')) {
      throw new Error(`Bad arguments near '${flag ?? ''}'. ${usage}`);
    }

    values.set(flag, value);
  }

  return values;
}

/**
 * Reads the command line.
 *
 * @remarks
 * Neither `--project` nor `--archive` has a default and neither can have one: a TIA project is a
 * machine's file, and the whole repository refuses hardcoded paths. `--archive` is the one to reach
 * for, since it gives every run a project nothing has written to yet.
 */
export function parseOptions(args: readonly string[]): Options {
  const values = parseFlags(args, 'Every flag takes a value.');

  const projectPath = values.get('--project');
  const archivePath = values.get('--archive');

  if ((projectPath === undefined) === (archivePath === undefined)) {
    throw new Error(
      'Usage: node src/run.ts (--archive <path to a .zap20> | --project <path to a .ap20>) ' +
        '[--specs <dir>] [--database <file>] [--policy <file>] [--limit <n>] [--repeat <n>] ' +
        '[--generator stub|model] [--model <id>] [--out <dir>]. ' +
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
    iterationLimit: requirePositive(values.get('--limit'), DefaultIterationLimit, '--limit'),
    repetitions: requirePositive(values.get('--repeat'), DefaultRepetitions, '--repeat'),
    generator: requireGeneratorChoice(values.get('--generator')),
    model: requireModel(values.get('--model'), values.get('--generator'))
  };
}

/**
 * Reads a count, refusing anything that is not one.
 *
 * @remarks
 * `Number('three')` is NaN, and a loop bounded by NaN runs zero times and reports success for
 * having done nothing. Refusing names the mistake while the run has cost nothing.
 */
function requirePositive(value: string | undefined, fallback: number, flag: string): number {
  if (value === undefined) {
    return fallback;
  }

  const parsed = Number(value);

  if (!Number.isInteger(parsed) || parsed < 1) {
    throw new Error(`${flag} takes a whole number of at least 1, not '${value}'.`);
  }

  return parsed;
}

/**
 * Reads which generator was asked for.
 *
 * @remarks
 * Anything that is not one of the two is refused rather than treated as the default. A run that
 * was meant to measure a model and quietly expanded the repository's own patterns would produce
 * numbers that look like the ones being asked for and are about something else.
 */
function requireGeneratorChoice(value: string | undefined): GeneratorChoice {
  if (value === undefined || value === 'stub') {
    return 'stub';
  }

  if (value === 'model') {
    return 'model';
  }

  throw new Error(`--generator takes 'stub' or 'model', not '${value}'.`);
}

/**
 * Reads which model was asked for, and refuses a request that names one nothing will ask.
 *
 * @remarks
 * `--model` alongside the stub generator is refused rather than ignored. The flag would otherwise
 * do nothing while looking like it had done something, and the run it produced would be a run
 * somebody believes measured a model - the same mistake `--generator` is written to refuse, one
 * flag further along.
 *
 * The name itself is not checked against a list. Models are released far more often than this file
 * is edited, and a whitelist here would refuse a new one for no better reason than that it is new.
 * The API rejects a name it does not know, in a sentence that says so.
 */
function requireModel(value: string | undefined, generator: string | undefined): string {
  if (value === undefined) {
    return DefaultModel;
  }

  if (generator !== 'model') {
    throw new Error(
      `--model ${value} was given, but the generator is the stub, which asks no model anything. ` +
        'Pass --generator model as well, or drop --model.'
    );
  }

  if (value.trim().length === 0) {
    throw new Error('--model takes a model identifier, not an empty string.');
  }

  return value;
}

function absoluteOrUndefined(path: string | undefined): string | undefined {
  return path === undefined ? undefined : resolve(path);
}
