/**
 * Design checks on a chart, each named after the rule it enforces.
 *
 * @remarks
 * These are the flaws that make a chart behave differently from what its author drew, taken from
 * IEC 60848:2013 and from the literature that pins down where the standard is ambiguous
 * (Mroß et al., "Unambiguous Interpretation of IEC 60848 GRAFCET", 2023; Schnakenbeck et al.,
 * "Structural Analysis of GRAFCET Control Specifications", ETFA 2023; González-Rodríguez et al.,
 * "Clarifying and extending IEC-60848", 2025).
 *
 * Every check is structural: it reads the chart, never simulates it. A check that cannot be sure
 * says "may", and says what would make it happen, so a person decides.
 */

import { formatCondition } from './conditionText.ts';
import { allSteps, conjuncts, isUncertain, variablesOf, type Condition, type Grafcet, type Sequence, type Transition } from './grafcetModel.ts';

/** One finding on the chart. `rule` is stable, so a test or a caller can match on it. */
export interface ChartFinding {
  readonly rule: ChartRule;
  readonly severity: 'error' | 'warning';
  readonly sequence: string;
  readonly message: string;
}

/** The rules this module checks. */
export type ChartRule =
  | 'initial-step'
  | 'duplicate-step'
  | 'unreachable-step'
  | 'dead-end-step'
  | 'uncertain-reading'
  | 'non-exclusive-selection'
  | 'transient-evolution'
  | 'unknown-step-reference';

/** Runs every check. The order of the result follows the order of the chart. */
export function checkGrafcet(grafcet: Grafcet): readonly ChartFinding[] {
  return [
    ...findDuplicateSteps(grafcet),
    ...grafcet.sequences.flatMap((sequence) => [
      ...findMissingInitial(sequence),
      ...findUnreachable(sequence),
      ...findDeadEnds(sequence),
      ...findUncertain(sequence),
      ...findNonExclusiveSelections(sequence),
      ...findTransientEvolutions(sequence),
      ...findUnknownStepReferences(sequence, grafcet),
    ]),
  ];
}

function finding(rule: ChartRule, severity: 'error' | 'warning', sequence: Sequence, message: string): ChartFinding {
  return { rule, severity, sequence: sequence.name, message };
}

function describe(transition: Transition): string {
  return `${transition.from.join(',')} → ${transition.to.join(',')} (${formatCondition(transition.condition)})`;
}

function findDuplicateSteps(grafcet: Grafcet): readonly ChartFinding[] {
  const owners = new Map<number, string>();
  const findings: ChartFinding[] = [];

  for (const sequence of grafcet.sequences) {
    for (const step of sequence.steps) {
      const owner = owners.get(step.number);

      if (owner !== undefined) {
        findings.push(finding('duplicate-step', 'error', sequence, `step ${step.number} also belongs to ${owner}; a step number is unique in the whole chart`));
      }

      owners.set(step.number, owner ?? sequence.name);
    }
  }

  return findings;
}

function findMissingInitial(sequence: Sequence): readonly ChartFinding[] {
  return sequence.steps.some((step) => step.initial) ? [] : [finding('initial-step', 'error', sequence, 'no initial step: nothing is active after start-up, so the sequence can never run (IEC 60848 4.5.2)')];
}

function findUnreachable(sequence: Sequence): readonly ChartFinding[] {
  const reached = new Set(sequence.steps.filter((step) => step.initial).map((step) => step.number));
  let grew = true;

  while (grew) {
    const before = reached.size;
    sequence.transitions.filter((transition) => transition.from.every((from) => reached.has(from))).forEach((transition) => transition.to.forEach((to) => reached.add(to)));
    grew = reached.size > before;
  }

  return sequence.steps
    .filter((step) => !reached.has(step.number) && sequence.steps.some((candidate) => candidate.initial))
    .map((step) => finding('unreachable-step', 'warning', sequence, `step ${step.number} can never become active: no chain of transitions leads to it from an initial step`));
}

function findDeadEnds(sequence: Sequence): readonly ChartFinding[] {
  return sequence.steps
    .filter((step) => !sequence.transitions.some((transition) => transition.from.includes(step.number)))
    .map((step) => finding('dead-end-step', 'warning', sequence, `step ${step.number} has no outgoing transition: once active it stays active until the controller restarts`));
}

function findUncertain(sequence: Sequence): readonly ChartFinding[] {
  return sequence.transitions
    .filter((transition) => isUncertain(transition.condition))
    .map((transition) => finding('uncertain-reading', 'warning', sequence, `${describe(transition)} was transcribed with '?': confirm it against the original before trusting the chart`));
}

/** A literal of a conjunction: a variable, or a negated variable. Anything else is not compared. */
function literals(condition: Condition): readonly { name: string; negated: boolean }[] {
  return conjuncts(condition).flatMap((term): { name: string; negated: boolean }[] => {
    if (term.kind === 'variable') {
      return [{ name: term.name, negated: false }];
    }

    return term.kind === 'not' && term.term.kind === 'variable' ? [{ name: term.term.name, negated: true }] : [];
  });
}

function contradict(left: Condition, right: Condition): boolean {
  const rightLiterals = literals(right);
  return literals(left).some((literal) => rightLiterals.some((other) => other.name === literal.name && other.negated !== literal.negated));
}

/**
 * The branches of a selection must be exclusive (IEC 60848 6.2.3): if two can clear together, both
 * fire and two steps of one sequence end up active. In a set/reset LAD program the network written
 * first wins instead, which is a different bug and just as silent.
 */
function findNonExclusiveSelections(sequence: Sequence): readonly ChartFinding[] {
  const findings: ChartFinding[] = [];

  for (const step of sequence.steps) {
    const leaving = sequence.transitions.filter((transition) => transition.from.length === 1 && transition.from[0] === step.number);

    leaving.forEach((first, index) => leaving.slice(index + 1)
      .filter((second) => !contradict(first.condition, second.condition))
      .forEach((second) => findings.push(finding('non-exclusive-selection', 'warning', sequence, describeOverlap(step.number, first.condition, second.condition)))));
  }

  return findings;
}

function describeOverlap(step: number, first: Condition, second: Condition): string {
  const pivot = literals(first)[0];
  const fix = pivot === undefined ? '' : ` One way: ${formatCondition(second)}.${pivot.negated ? '' : '/'}${pivot.name} gives the first branch priority.`;
  return `after step ${step}, ${formatCondition(first)} and ${formatCondition(second)} can both be true at once, so the chart does not say which way to go; in set/reset LAD the network written first wins.${fix}`;
}

/**
 * Transient evolution (IEC 60848 3.1.9, 4.9.3): a step activated by a condition that is still true,
 * and left by a condition that includes it, may be crossed in the same evolution without its
 * continuous actions ever being output. González-Rodríguez et al. (2025) single out exactly this
 * shape: the same level condition on two consecutive transitions.
 */
function findTransientEvolutions(sequence: Sequence): readonly ChartFinding[] {
  const findings: ChartFinding[] = [];

  for (const entering of sequence.transitions) {
    for (const leaving of sequence.transitions.filter((candidate) => candidate.from.some((from) => entering.to.includes(from)))) {
      const shared = literals(entering.condition).filter((literal) => literals(leaving.condition).some((other) => other.name === literal.name && other.negated === literal.negated));

      if (shared.length > 0 && !contradict(entering.condition, leaving.condition)) {
        const names = shared.map((literal) => (literal.negated ? '/' : '') + literal.name).join('.');
        findings.push(finding('transient-evolution', 'warning', sequence,
          `step ${leaving.from.join(',')} may be crossed without stopping: ${names} is still true when it activates, so if the rest of ${formatCondition(leaving.condition)} already holds, ${describe(leaving)} clears in the same evolution. Use an edge (↑) or a condition that cannot hold on arrival`));
      }
    }
  }

  return findings;
}

function findUnknownStepReferences(sequence: Sequence, grafcet: Grafcet): readonly ChartFinding[] {
  const known = new Set(allSteps(grafcet).map((step) => `X${step.number}`));
  return sequence.transitions.flatMap((transition) => variablesOf(transition.condition)
    .filter((name) => /^X\d+$/.test(name) && !known.has(name))
    .map((name) => finding('unknown-step-reference', 'warning', sequence, `${describe(transition)} reads ${name}, which is not a step of this chart`)));
}
