/**
 * The memory bits the generated edge contacts use: one per edge, `A_FP1`, `A_FP2`, `A_FN1`, ...
 *
 * @remarks
 * A P or N contact keeps the operand's previous state in its memory bit and rewrites it every time
 * the contact is executed. Two contacts sharing a bit therefore see a single edge between them, and a
 * bit that is also one of the chart's own signals gets that signal overwritten every scan. Both are
 * silent in TIA Portal, so every bit is new: a name the chart already uses is skipped.
 */

import { allSteps, variablesOf, type Condition, type Grafcet } from './grafcetModel.ts';

/** The memory bit each edge of the chart was given, keyed by the edge itself. */
export type EdgeBits = ReadonlyMap<Extract<Condition, { kind: 'rising' | 'falling' }>, string>;

/** Gives every edge in every transition a bit of its own, never one of the chart's names. */
export function allocateEdgeBits(grafcet: Grafcet): EdgeBits {
  const taken = namesInUse(grafcet);
  const bits = new Map<Extract<Condition, { kind: 'rising' | 'falling' }>, string>();
  const visit = (condition: Condition): void => {
    if (condition.kind === 'and' || condition.kind === 'or') {
      condition.terms.forEach(visit);
      return;
    }

    if (condition.kind === 'not') {
      visit(condition.term);
      return;
    }

    if (condition.kind === 'rising' || condition.kind === 'falling') {
      bits.set(condition, nextFree(`${edgeOperand(condition.term)}_${condition.kind === 'rising' ? 'FP' : 'FN'}`, taken));
    }
  };
  grafcet.sequences.forEach((sequence) => sequence.transitions.forEach((transition) => visit(transition.condition)));
  return bits;
}

/** The variable an edge reads. An edge contact reads exactly one. */
export function edgeOperand(term: Condition): string {
  if (term.kind !== 'variable') {
    throw new Error('an edge contact reads a single variable; rewrite ↑(A.B) with an edge on each variable it depends on');
  }

  return term.name;
}

function nextFree(prefix: string, taken: Set<string>): string {
  let number = 1;

  while (taken.has(`${prefix}${number}`)) {
    number += 1;
  }

  const name = `${prefix}${number}`;
  taken.add(name);
  return name;
}

function namesInUse(grafcet: Grafcet): Set<string> {
  const steps = allSteps(grafcet);
  const conditions = [
    ...grafcet.sequences.flatMap((sequence) => sequence.transitions.map((transition) => transition.condition)),
    ...steps.flatMap((step) => step.actions.flatMap((action) => action.kind === 'output' && action.condition !== null ? [action.condition] : [])),
  ];
  return new Set([
    ...steps.map((step) => `X${step.number}`),
    ...steps.flatMap((step) => step.actions.map((action) => action.name)),
    ...conditions.flatMap(variablesOf),
  ]);
}
