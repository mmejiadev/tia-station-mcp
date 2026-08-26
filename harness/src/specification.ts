import { readFileSync } from 'node:fs';

/** Drives one input of the running cell. */
export type WriteStep = {
  readonly action: 'write';
  readonly tag: string;
  /** The value as text, parsed by the server as the tag's declared type. */
  readonly value: string;
};

/** Waits for the cell to reach a state, or gives up. */
export type WaitStep = {
  readonly action: 'waitFor';
  readonly tag: string;
  readonly equals: string;
  readonly timeoutMilliseconds: number;
};

/** Asserts something about the cell right now. */
export type ExpectStep = {
  readonly action: 'expect';
  readonly tag: string;
  /** Exactly one of these is set. */
  readonly equals?: string;
  readonly notEquals?: string;
};

/**
 * Asserts that something does **not** happen, for long enough to mean it.
 *
 * @remarks
 * `expect` reads one instant, which is the wrong shape for a negative claim: "no piece completed"
 * checked one millisecond after the cell was started passes whether or not the cell is about to
 * move one. The cases that need this are the ones about a cell staying put - no Enable, manual
 * mode - and those are exactly the claims worth making precisely.
 */
export type HoldStep = {
  readonly action: 'hold';
  readonly tag: string;
  /** The value the tag must not reach while the window lasts. */
  readonly notEquals: string;
  readonly durationMilliseconds: number;
};

/** One step of an acceptance check. */
export type AcceptanceStep = WriteStep | WaitStep | ExpectStep | HoldStep;

/** Which virtual controller the specification runs on. */
export type ControllerSpecification = {
  readonly name: string;
  /** Must match the CPU's address in the project, or a download cannot find it. */
  readonly address: string;
  readonly subnetMask: string;
  readonly cpuType: string;
};

/** One case the harness runs end to end. */
export type Specification = {
  readonly name: string;
  /** What is being asked for, in words. The stub ignores it; a model will not. */
  readonly goal: string;
  /** Path to the cell specification in spec/cells/, relative to the repository root. */
  readonly cellPath: string;
  /** Path to the CPU in the project, e.g. 'PLC_0'. */
  readonly softwarePath: string;
  readonly controller: ControllerSpecification;
  /**
   * Whether the stub generator should produce code that does not compile on the first attempt.
   *
   * @remarks
   * A property of the specification rather than of the generator, because it is the specification
   * that says what this case is for: one of them exists to prove the loop reaches a clean compile
   * in one pass, and one exists to prove it recovers from a broken one. A model-backed generator
   * ignores it.
   */
  readonly breakFirstAttempt: boolean;
  /** What must be true of the running cell for this case to have passed. */
  readonly acceptance: readonly AcceptanceStep[];
};

const KnownActions = ['write', 'waitFor', 'expect', 'hold'] as const;

/**
 * Reads a specification from a file.
 *
 * @param path The file to read.
 * @returns The specification, validated.
 * @throws If the file is not a specification this harness understands.
 */
export function loadSpecification(path: string): Specification {
  let parsed: unknown;

  try {
    parsed = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(`${path} is not valid JSON: ${(error as Error).message}`);
  }

  return validateSpecification(parsed, path);
}

/**
 * Checks that a parsed object is a specification, and says what is wrong when it is not.
 *
 * @remarks
 * Everything is checked up front and the whole thing is refused if anything is missing. The
 * alternative — discovering at the verify phase that a step names no tag — would spend a compile
 * and a download before finding out, and this loop's iterations cost a minute each.
 *
 * The acceptance language is three actions and no expressions, on purpose and for the same reason
 * the SCL template language is two constructs: a language would need its own parser, its own error
 * messages and its own tests, and somebody debugging a cell that will not pass would then have two
 * of them to hold in their head.
 */
export function validateSpecification(candidate: unknown, source: string): Specification {
  const raw = requireObject(candidate, source);

  const specification: Specification = {
    name: requireString(raw, 'name', source),
    goal: requireString(raw, 'goal', source),
    cellPath: requireString(raw, 'cellPath', source),
    softwarePath: requireString(raw, 'softwarePath', source),
    controller: validateController(raw['controller'], source),
    breakFirstAttempt: raw['breakFirstAttempt'] === true,
    acceptance: validateAcceptance(raw['acceptance'], source)
  };

  if (specification.acceptance.length === 0) {
    throw new Error(
      `${source} has no acceptance steps. A case that checks nothing would pass by compiling, and ` +
        'compiling is not the claim this harness exists to make.'
    );
  }

  return specification;
}

function validateController(candidate: unknown, source: string): ControllerSpecification {
  const raw = requireObject(candidate, `${source}: controller`);

  return {
    name: requireString(raw, 'name', `${source}: controller`),
    address: requireString(raw, 'address', `${source}: controller`),
    subnetMask: requireString(raw, 'subnetMask', `${source}: controller`),
    cpuType: requireString(raw, 'cpuType', `${source}: controller`)
  };
}

function validateAcceptance(candidate: unknown, source: string): AcceptanceStep[] {
  if (!Array.isArray(candidate)) {
    throw new Error(`${source}: acceptance must be an array of steps.`);
  }

  return candidate.map((step, index) => validateStep(step, `${source}: acceptance[${index}]`));
}

/**
 * Validates one step.
 *
 * @remarks
 * An unrecognised action is refused rather than skipped, and that is the same rule the governance
 * layer states as "anything not foreseen refuses". A step silently ignored would make the case pass
 * while checking less than it says it does, which is worse than not running at all.
 */
function validateStep(candidate: unknown, source: string): AcceptanceStep {
  const raw = requireObject(candidate, source);
  const action = requireString(raw, 'action', source);

  if (action === 'write') {
    return { action, tag: requireString(raw, 'tag', source), value: requireString(raw, 'value', source) };
  }

  if (action === 'waitFor') {
    return {
      action,
      tag: requireString(raw, 'tag', source),
      equals: requireString(raw, 'equals', source),
      timeoutMilliseconds: requirePositiveNumber(raw, 'timeoutMilliseconds', source)
    };
  }

  if (action === 'expect') {
    return validateExpect(raw, source);
  }

  if (action === 'hold') {
    return {
      action,
      tag: requireString(raw, 'tag', source),
      notEquals: requireString(raw, 'notEquals', source),
      durationMilliseconds: requirePositiveNumber(raw, 'durationMilliseconds', source)
    };
  }

  throw new Error(`${source}: '${action}' is not an action. Use one of: ${KnownActions.join(', ')}.`);
}

function validateExpect(raw: Record<string, unknown>, source: string): ExpectStep {
  const equals = raw['equals'];
  const notEquals = raw['notEquals'];
  const tag = requireString(raw, 'tag', source);

  // Exactly one, because both would be a contradiction nobody meant and neither would assert
  // nothing while looking like an assertion.
  if (typeof equals === 'string' && notEquals === undefined) {
    return { action: 'expect', tag, equals };
  }

  if (typeof notEquals === 'string' && equals === undefined) {
    return { action: 'expect', tag, notEquals };
  }

  throw new Error(`${source}: an expect step needs exactly one of 'equals' or 'notEquals'.`);
}

function requireObject(candidate: unknown, source: string): Record<string, unknown> {
  if (typeof candidate !== 'object' || candidate === null || Array.isArray(candidate)) {
    throw new Error(`${source} must be an object.`);
  }

  return candidate as Record<string, unknown>;
}

function requireString(raw: Record<string, unknown>, field: string, source: string): string {
  const value = raw[field];

  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`${source}: '${field}' is required and must be a non-empty string.`);
  }

  return value;
}

function requirePositiveNumber(raw: Record<string, unknown>, field: string, source: string): number {
  const value = raw[field];

  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
    throw new Error(`${source}: '${field}' is required and must be a positive number.`);
  }

  return value;
}
