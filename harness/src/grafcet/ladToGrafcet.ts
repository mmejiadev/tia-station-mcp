/**
 * Recovers the GRAFCET a LAD program implements with the set/reset method.
 *
 * @remarks
 * The method writes each transition as one network: the source steps and the transition-condition
 * in series, then S on every target step and R on every source step. Continuous actions are plain
 * coils whose contacts are the steps they belong to. Reading that pattern backwards gives the chart
 * the program actually runs, which is the chart worth comparing with the one on the whiteboard.
 *
 * A source step is recognised by being *both* a contact of the rung and reset by it. A step that is
 * only a contact is part of the transition-condition, as `X43` is when one sequence waits for
 * another. Code that fits the method is turned into the chart; code that does not is reported with
 * the network it sits in, because those departures are where the bugs are.
 */

import { and, conjuncts, negate, or, sameCondition, variable, variablesOf, type Action, type Condition, type Grafcet, type Sequence, type Step, type Transition } from './grafcetModel.ts';
import type { LadBlock, LadFinding, LadLocation, LadOperation } from './s7dclReader.ts';

/** How step variables and the first-scan bit are named in the program being read. */
export interface LadReadingOptions {
  readonly stepPattern: RegExp;
  readonly firstScanNames: readonly string[];
}

/** The default reading: steps are X0, X1, …; the first scan bit is the system memory FirstScan. */
export const DefaultReading: LadReadingOptions = { stepPattern: /^X(\d+)$/, firstScanNames: ['FirstScan'] };

/** The chart recovered from the program, and everything that did not fit the method. */
export interface LadChart {
  readonly grafcet: Grafcet;
  readonly findings: readonly LadFinding[];
}

type Operation<K extends LadOperation['kind']> = LadOperation & { readonly kind: K };

/** Recovers the chart from blocks read with {@link readS7dcl}. */
export function ladToGrafcet(blocks: readonly LadBlock[], title: string, options: LadReadingOptions = DefaultReading): LadChart {
  const operations = blocks.flatMap((block) => block.operations);
  const findings: LadFinding[] = blocks.flatMap((block) => block.findings);
  const stepOf = (name: string): number | (null) => readStep(name, options.stepPattern);
  const isFirstScan = (condition: Condition): boolean => variablesOf(condition).some((name) => options.firstScanNames.includes(name));

  findings.push(...findPlainCoilsOnSteps(operations, stepOf), ...findDoubleCoils(operations));
  const timers = readTimers(operations, stepOf, findings);
  const transitions = readTransitions(operations, stepOf, isFirstScan, findings).map((located) => ({ ...located, transition: renameTimers(located.transition, timers) }));
  const initial = new Set(operations.filter(isSetOnStep(stepOf)).filter((operation) => isFirstScan(operation.condition)).map((operation) => stepOf(operation.operand)!));
  const actions = readActions(operations, stepOf, timers, findings);
  const sequences = groupSequences(transitions, initial, actions);
  return { grafcet: { title, sequences }, findings };
}

function readStep(name: string, pattern: RegExp): number | null {
  const match = pattern.exec(name);
  return match === null ? null : Number(match[1]);
}

function isSetOnStep(stepOf: (name: string) => number | null): (operation: LadOperation) => operation is Operation<'set'> {
  return (operation): operation is Operation<'set'> => operation.kind === 'set' && stepOf(operation.operand) !== null;
}

function findPlainCoilsOnSteps(operations: readonly LadOperation[], stepOf: (name: string) => number | null): readonly LadFinding[] {
  return operations
    .filter((operation): operation is Operation<'assign'> => operation.kind === 'assign' && stepOf(operation.operand) !== null)
    .map((operation) => ({
      block: operation.block,
      network: operation.network,
      severity: 'error' as const,
      message: `step ${operation.operand} is driven by a plain coil ( ). A step must be kept with S and cleared with R, otherwise it drops the moment the transition-condition goes false`,
    }));
}

function findDoubleCoils(operations: readonly LadOperation[]): readonly LadFinding[] {
  const assigns = operations.filter((operation): operation is Operation<'assign'> => operation.kind === 'assign');
  const seen = new Map<string, LadLocation>();
  const findings: LadFinding[] = [];

  for (const operation of assigns) {
    const first = seen.get(operation.operand);

    if (first !== undefined) {
      findings.push({ ...operation, severity: 'error', message: `${operation.operand} has a second plain coil (the first is in ${first.block} network ${first.network}); the last one written wins every scan. OR the steps into one coil` });
    }

    seen.set(operation.operand, first ?? operation);
  }

  return findings;
}

/** Maps a timer's done bit (`IEC_Timer_0_DB.Q`) to the name the chart uses for it (`T36`). */
function readTimers(operations: readonly LadOperation[], stepOf: (name: string) => number | null, findings: LadFinding[]): ReadonlyMap<string, { step: number; name: string; seconds: number }> {
  const timers = new Map<string, { step: number; name: string; seconds: number }>();

  for (const operation of operations.filter((candidate): candidate is Operation<'timer'> => candidate.kind === 'timer')) {
    const step = operation.condition.kind === 'variable' ? stepOf(operation.condition.name) : null;

    if (step === null || operation.presetSeconds === null || operation.type !== 'TON') {
      findings.push({ ...operation, severity: 'warning', message: `${operation.type} ${operation.instance} is not an on-delay started by a single step, so it is drawn as the variable ${operation.instance}.Q` });
      continue;
    }

    timers.set(`${operation.instance}.Q`, { step, name: `T${step}`, seconds: operation.presetSeconds });
  }

  return timers;
}

interface LocatedTransition {
  readonly transition: Transition;
  readonly location: LadLocation;
}

function readTransitions(operations: readonly LadOperation[], stepOf: (name: string) => number | null, isFirstScan: (condition: Condition) => boolean, findings: LadFinding[]): readonly LocatedTransition[] {
  const sets = operations.filter(isSetOnStep(stepOf)).filter((operation) => !isFirstScan(operation.condition));
  const groups = groupBy(sets, (operation) => `${operation.block}|${operation.network}|${JSON.stringify(operation.condition)}`);
  return [...groups.values()].flatMap((group) => readTransition(group, operations, stepOf, findings));
}

function readTransition(group: readonly Operation<'set'>[], operations: readonly LadOperation[], stepOf: (name: string) => number | null, findings: LadFinding[]): readonly LocatedTransition[] {
  const first = group[0]!;
  const resets = operations
    .filter((operation): operation is Operation<'reset'> => operation.kind === 'reset' && operation.block === first.block && operation.network === first.network)
    .filter((operation) => sameCondition(operation.condition, first.condition) && stepOf(operation.operand) !== null)
    .map((operation) => operation.operand);
  const contacts = conjuncts(first.condition);
  const sources = contacts.filter((term) => term.kind === 'variable' && resets.includes(term.name)).map((term) => (term as { name: string }).name);
  const targets = group.map((operation) => stepOf(operation.operand)!);

  if (sources.length === 0) {
    findings.push({ ...first, severity: 'error', message: `S ${group.map(operation => operation.operand).join(', ')} has no source step: no step in its contacts is reset by the same rung, so the previous step stays active` });
    return [];
  }

  reportStrayResets(first, resets, sources, findings);
  const condition = and(contacts.filter((term) => !(term.kind === 'variable' && sources.includes(term.name))));
  return [{ transition: { from: sources.map((source) => stepOf(source)!), to: targets, condition }, location: first }];
}

function reportStrayResets(location: LadLocation, resets: readonly string[], sources: readonly string[], findings: LadFinding[]): void {
  for (const stray of resets.filter((reset) => !sources.includes(reset))) {
    findings.push({ ...location, severity: 'warning', message: `R ${stray} is not a step this transition leaves: ${stray} is not one of its contacts` });
  }
}

function renameTimers(transition: Transition, timers: ReadonlyMap<string, { name: string }>): Transition {
  return { ...transition, condition: rename(transition.condition, (name) => timers.get(name)?.name ?? name) };
}

function rename(condition: Condition, map: (name: string) => string): Condition {
  switch (condition.kind) {
    case 'variable':
      return variable(map(condition.name), condition.uncertain);
    case 'not':
      return negate(rename(condition.term, map), condition.uncertain);
    case 'rising':
    case 'falling':
      return { kind: condition.kind, term: rename(condition.term, map) };
    case 'and':
      return and(condition.terms.map((term) => rename(term, map)));
    case 'or':
      return or(condition.terms.map((term) => rename(term, map)));
    case 'true':
      return condition;
    default:
      throw new Error(`Unrecognised condition: ${JSON.stringify(condition satisfies never)}`);
  }
}

function readActions(operations: readonly LadOperation[], stepOf: (name: string) => number | null, timers: ReadonlyMap<string, { step: number; name: string; seconds: number }>, findings: LadFinding[]): ReadonlyMap<number, Action[]> {
  const actions = new Map<number, Action[]>();
  const add = (step: number, action: Action): void => { actions.set(step, [...(actions.get(step) ?? []), action]); };

  for (const timer of timers.values()) {
    add(timer.step, { kind: 'timer', name: timer.name, seconds: timer.seconds });
  }

  for (const operation of operations) {
    readAction(operation, stepOf, findings).forEach(([step, action]) => add(step, action));
  }

  return actions;
}

function readAction(operation: LadOperation, stepOf: (name: string) => number | null, findings: LadFinding[]): readonly [number, Action][] {
  if ((operation.kind !== 'assign' && operation.kind !== 'set' && operation.kind !== 'reset') || stepOf(operation.operand) !== null) {
    return [];
  }

  const terms = operation.condition.kind === 'or' ? operation.condition.terms : [operation.condition];
  const found: [number, Action][] = [];

  for (const term of terms) {
    const steps = conjuncts(term).filter((part) => part.kind === 'variable' && stepOf(part.name) !== null) as { name: string }[];

    if (steps.length !== 1) {
      findings.push({ ...operation, severity: 'warning', message: `${operation.operand} is written under a condition that does not name exactly one step, so it is not drawn as an action` });
      continue;
    }

    const rest = and(conjuncts(term).filter((part) => !(part.kind === 'variable' && part.name === steps[0]!.name)));
    const action: Action = operation.kind === 'assign' ? { kind: 'output', name: operation.operand, condition: rest.kind === 'true' ? null : rest } : { kind: operation.kind, name: operation.operand };
    found.push([stepOf(steps[0]!.name)!, action]);
  }

  return found;
}

function groupSequences(located: readonly LocatedTransition[], initial: ReadonlySet<number>, actions: ReadonlyMap<number, Action[]>): readonly Sequence[] {
  const numbers = new Set([...located.flatMap((item) => [...item.transition.from, ...item.transition.to]), ...initial, ...actions.keys()]);
  const component = unionFind([...numbers], located.map((item) => [...item.transition.from, ...item.transition.to]));
  const members = groupBy([...numbers].sort((left, right) => left - right), (number) => component(number));

  return [...members.values()].map((steps) => {
    const own = located.filter((item) => steps.includes(item.transition.from[0]!));
    const name = mostCommon(own.map((item) => item.location.block)) ?? `Steps ${steps.join(', ')}`;
    const built: Step[] = steps.map((number) => ({ number, initial: initial.has(number), actions: actions.get(number) ?? [] }));
    return { name, steps: built, transitions: own.map((item) => item.transition) };
  });
}

function unionFind(items: readonly number[], links: readonly (readonly number[])[]): (item: number) => number {
  const parent = new Map(items.map((item) => [item, item]));
  const find = (item: number): number => {
    const up = parent.get(item) ?? item;
    return up === item ? item : find(up);
  };

  for (const link of links) {
    link.slice(1).forEach((item) => parent.set(find(item), find(link[0]!)));
  }

  return find;
}

function groupBy<T, K>(items: readonly T[], key: (item: T) => K): Map<K, T[]> {
  const groups = new Map<K, T[]>();
  items.forEach((item) => groups.set(key(item), [...(groups.get(key(item)) ?? []), item]));
  return groups;
}

function mostCommon(values: readonly string[]): string | null {
  const counts = groupBy(values, (value) => value);
  return [...counts.entries()].sort((left, right) => right[1].length - left[1].length)[0]?.[0] ?? null;
}
