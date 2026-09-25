/**
 * The GRAFCET model every other file in this folder reads or writes.
 *
 * @remarks
 * IEC 60848 separates the *structure* of a chart (steps, transitions, directed links) from its
 * *interpretation* (transition-conditions and actions), and so does this model. Everything is
 * immutable: a chart is either parsed from the text notation, derived from PLC code, or built by
 * hand in a test, and nothing edits it afterwards.
 *
 * Only what an educational or cell-level chart uses is modelled: steps, initial steps, sequences,
 * OR and AND divergence and convergence, continuous and conditional actions, stored actions, and
 * on-delay timers. Macro-steps, enclosing steps and forcing orders are left out on purpose; a chart
 * that needs them is reported as unsupported rather than silently flattened.
 */

/** A boolean expression over input, internal and step variables. */
export type Condition =
  | { readonly kind: 'variable'; readonly name: string; readonly uncertain: boolean }
  | { readonly kind: 'not'; readonly term: Condition; readonly uncertain: boolean }
  | { readonly kind: 'rising'; readonly term: Condition }
  | { readonly kind: 'falling'; readonly term: Condition }
  | { readonly kind: 'and'; readonly terms: readonly Condition[] }
  | { readonly kind: 'or'; readonly terms: readonly Condition[] }
  | { readonly kind: 'true' };

/**
 * What a step does while, or when, it is active.
 *
 * `output` is a continuous action (assignation on state, IEC 60848 4.8.2): the variable is true
 * exactly while the step is active and the optional condition holds. `set` and `reset` are stored
 * actions on activation (allocation on event, 4.8.3). `timer` is an on-delay started by the step,
 * whose done bit is the variable `name` that later transition-conditions read.
 */
export type Action =
  | { readonly kind: 'output'; readonly name: string; readonly condition: Condition | null }
  | { readonly kind: 'set'; readonly name: string }
  | { readonly kind: 'reset'; readonly name: string }
  | { readonly kind: 'timer'; readonly name: string; readonly seconds: number };

/** A step, numbered as the chart numbers it. */
export interface Step {
  readonly number: number;
  readonly initial: boolean;
  readonly actions: readonly Action[];
}

/**
 * A transition. More than one source is an AND convergence (synchronisation) and more than one
 * target is an AND divergence (activation of parallel sequences).
 */
export interface Transition {
  readonly from: readonly number[];
  readonly to: readonly number[];
  readonly condition: Condition;
}

/** A connected chart: the steps and transitions of one sequence, e.g. one conveyor. */
export interface Sequence {
  readonly name: string;
  readonly steps: readonly Step[];
  readonly transitions: readonly Transition[];
}

/** A global chart: every sequence of one controller. */
export interface Grafcet {
  readonly title: string;
  readonly sequences: readonly Sequence[];
}

/** The always-true condition, for a transition that fires as soon as its source is active. */
export const Always: Condition = { kind: 'true' };

/** A plain variable reference. */
export function variable(name: string, uncertain = false): Condition {
  return { kind: 'variable', name, uncertain };
}

/** Negation, collapsing a double negation so `/ /a` never survives into a drawing. */
export function negate(term: Condition, uncertain = false): Condition {
  if (term.kind === 'not' && !uncertain) {
    return term.term;
  }

  return { kind: 'not', term, uncertain };
}

/** Conjunction with `true` removed and nested conjunctions flattened. */
export function and(terms: readonly Condition[]): Condition {
  const flat = terms.flatMap((term) => term.kind === 'and' ? term.terms : [term]).filter((term) => term.kind !== 'true');
  return flat.length === 0 ? Always : flat.length === 1 ? flat[0]! : { kind: 'and', terms: flat };
}

/** Disjunction with nested disjunctions flattened. A `true` term makes the whole of it true. */
export function or(terms: readonly Condition[]): Condition {
  const flat = terms.flatMap((term) => term.kind === 'or' ? term.terms : [term]);

  if (flat.length === 0 || flat.some((term) => term.kind === 'true')) {
    return Always;
  }

  return flat.length === 1 ? flat[0]! : { kind: 'or', terms: flat };
}

/**
 * Pulls the terms every branch of a disjunction shares out in front: `X71.A + X71.B` becomes
 * `X71.(A + B)`. LAD computes a parallel branch as exactly that expanded form, and the source step
 * of a transition can only be recognised once it stands in front again.
 */
export function factorCommon(condition: Condition): Condition {
  if (condition.kind !== 'or') {
    return condition;
  }

  const branches = condition.terms.map(conjuncts);
  const common = branches[0]!.filter((term) => branches.every((branch) => branch.some((other) => sameCondition(other, term))));

  if (common.length === 0) {
    return condition;
  }

  const rests = branches.map((branch) => and(branch.filter((term) => !common.some((shared) => sameCondition(shared, term)))));
  return and([...common, or(rests)]);
}

/** The top-level AND terms of a condition: the condition itself when it is not a conjunction. */
export function conjuncts(condition: Condition): readonly Condition[] {
  if (condition.kind === 'and') {
    return condition.terms;
  }

  return condition.kind === 'true' ? [] : [condition];
}

/** Every variable name a condition reads, in order of first appearance. */
export function variablesOf(condition: Condition): readonly string[] {
  const names: string[] = [];
  visit(condition, (name) => {
    if (!names.includes(name)) {
      names.push(name);
    }
  });
  return names;
}

function visit(condition: Condition, onVariable: (name: string) => void): void {
  switch (condition.kind) {
    case 'variable':
      onVariable(condition.name);
      return;
    case 'not':
    case 'rising':
    case 'falling':
      visit(condition.term, onVariable);
      return;
    case 'and':
    case 'or':
      condition.terms.forEach((term) => visit(term, onVariable));
      return;
    case 'true':
      return;
    default:
      throw new Error(`Unrecognised condition: ${JSON.stringify(condition satisfies never)}`);
  }
}

/** True when any part of the condition was transcribed with a `?`, i.e. still needs confirming. */
export function isUncertain(condition: Condition): boolean {
  switch (condition.kind) {
    case 'variable':
      return condition.uncertain;
    case 'not':
      return condition.uncertain || isUncertain(condition.term);
    case 'rising':
    case 'falling':
      return isUncertain(condition.term);
    case 'and':
    case 'or':
      return condition.terms.some(isUncertain);
    case 'true':
      return false;
    default:
      throw new Error(`Unrecognised condition: ${JSON.stringify(condition satisfies never)}`);
  }
}

/** Structural equality, so two transitions written in different networks can be recognised as one. */
export function sameCondition(left: Condition, right: Condition): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

/** Every step of every sequence, for lookups that do not care which sequence owns it. */
export function allSteps(grafcet: Grafcet): readonly Step[] {
  return grafcet.sequences.flatMap((sequence) => sequence.steps);
}
