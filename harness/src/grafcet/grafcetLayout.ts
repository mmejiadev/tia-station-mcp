/**
 * Decides where each step and transition of a sequence goes, before anything is drawn.
 *
 * @remarks
 * The rules are the ones a person follows at the whiteboard, made deterministic:
 *
 * - The main sequence runs down the first lane. A step's first outgoing transition continues its
 *   lane; every further one (a selection branch, IEC 60848 6.2.3) opens a lane to the right.
 * - Rows are the longest forward path from an initial step, so a convergence always sits below
 *   every branch that reaches it.
 * - A transition back to an earlier step is a *return*. When it is the only way out of its step and
 *   goes back to the first lane, it is drawn as the classic loop on the left with an upward arrow
 *   (6.2.2). Otherwise it becomes a jump arrow naming the target, which the standard treats as an
 *   equivalent notation and which never crosses another line.
 *
 * Macro-steps, enclosures and forcing orders are not laid out; the model does not hold them.
 */

import type { Sequence, Transition } from './grafcetModel.ts';

/** Where a step sits: lane (column) and row, both counted from zero. */
export interface Cell {
  readonly lane: number;
  readonly row: number;
}

/** How a transition is drawn. */
export type RouteKind = 'forward' | 'loop' | 'jump';

/** A transition with its drawing decision. `barLane` is the lane its bar sits in. */
export interface RoutedTransition {
  readonly transition: Transition;
  readonly kind: RouteKind;
  readonly barLane: number;
  readonly row: number;
  readonly loopIndex: number;
}

/** The layout of one sequence. */
export interface SequenceLayout {
  readonly cells: ReadonlyMap<number, Cell>;
  readonly routes: readonly RoutedTransition[];
  readonly lanes: number;
  readonly rows: number;
  readonly loops: number;
}

/** Lays out one sequence. */
export function layOutSequence(sequence: Sequence): SequenceLayout {
  const outgoing = (step: number): readonly Transition[] => sequence.transitions.filter((transition) => transition.from.includes(step));
  const backEdges = findBackEdges(sequence, outgoing);
  const rows = assignRows(sequence, backEdges);
  const lanes = assignLanes(sequence, outgoing, backEdges);
  const routes = routeTransitions(sequence, { rows, lanes, backEdges, outgoing });
  const cells = new Map(sequence.steps.map((step) => [step.number, { lane: lanes.get(step.number) ?? 0, row: rows.get(step.number) ?? 0 }]));
  const laneCount = Math.max(1, ...[...cells.values()].map((cell) => cell.lane + 1), ...routes.map((route) => route.barLane + 1));
  const rowCount = Math.max(1, ...[...cells.values()].map((cell) => cell.row + 1));
  return { cells, routes, lanes: laneCount, rows: rowCount, loops: routes.filter((route) => route.kind === 'loop').length };
}

function roots(sequence: Sequence): readonly number[] {
  const initial = sequence.steps.filter((step) => step.initial).map((step) => step.number);
  const targets = new Set(sequence.transitions.flatMap((transition) => transition.to));
  const orphans = sequence.steps.filter((step) => !step.initial && !targets.has(step.number)).map((step) => step.number);
  const fallback = initial.length === 0 && orphans.length === 0 ? sequence.steps.slice(0, 1).map((step) => step.number) : [];
  return [...initial, ...orphans, ...fallback];
}

/** Transitions that close a cycle, found by depth-first search from the roots. */
function findBackEdges(sequence: Sequence, outgoing: (step: number) => readonly Transition[]): ReadonlySet<Transition> {
  const back = new Set<Transition>();
  const done = new Set<number>();
  const onPath = new Set<number>();

  const visit = (step: number): void => {
    onPath.add(step);

    for (const transition of outgoing(step)) {
      if (transition.to.some((target) => onPath.has(target))) {
        back.add(transition);
        continue;
      }

      transition.to.filter((target) => !done.has(target)).forEach(visit);
    }

    onPath.delete(step);
    done.add(step);
  };

  [...roots(sequence), ...sequence.steps.map((step) => step.number)].filter((step) => !done.has(step)).forEach(visit);
  return back;
}

function assignRows(sequence: Sequence, backEdges: ReadonlySet<Transition>): ReadonlyMap<number, number> {
  const rows = new Map(sequence.steps.map((step) => [step.number, 0]));
  const forward = sequence.transitions.filter((transition) => !backEdges.has(transition));

  for (let pass = 0; pass < sequence.steps.length; pass++) {
    for (const transition of forward) {
      const below = Math.max(...transition.from.map((from) => rows.get(from) ?? 0)) + 1;
      transition.to.filter((target) => (rows.get(target) ?? 0) < below).forEach((target) => rows.set(target, below));
    }
  }

  return rows;
}

function assignLanes(sequence: Sequence, outgoing: (step: number) => readonly Transition[], backEdges: ReadonlySet<Transition>): ReadonlyMap<number, number> {
  const lanes = new Map<number, number>();
  let nextLane = 0;

  const place = (step: number, lane: number): void => {
    lanes.set(step, lane);
    let first = true;

    for (const transition of outgoing(step).filter((candidate) => !backEdges.has(candidate))) {
      transition.to.filter((target) => !lanes.has(target)).forEach((target, index) => {
        const own = first && index === 0 ? lane : ++nextLane;
        place(target, own);
      });
      first = false;
    }
  };

  for (const root of roots(sequence).filter((candidate) => !lanes.has(candidate))) {
    place(root, lanes.size === 0 ? 0 : ++nextLane);
  }

  sequence.steps.filter((step) => !lanes.has(step.number)).forEach((step) => place(step.number, ++nextLane));
  return lanes;
}

/** What the earlier passes worked out about a sequence, handed to the routing pass as one. */
interface PlacedGraph {
  readonly rows: ReadonlyMap<number, number>;
  readonly lanes: ReadonlyMap<number, number>;
  readonly backEdges: ReadonlySet<Transition>;
  readonly outgoing: (step: number) => readonly Transition[];
}

function routeTransitions(sequence: Sequence, graph: PlacedGraph): readonly RoutedTransition[] {
  const { rows, lanes, backEdges, outgoing } = graph;
  const usedStubs = new Set<string>();
  const maxLane = Math.max(0, ...lanes.values());
  let loops = 0;

  return sequence.transitions.map((transition) => {
    const source = transition.from[0]!;
    const row = Math.max(...transition.from.map((from) => rows.get(from) ?? 0));
    const sourceLane = lanes.get(source) ?? 0;

    if (!backEdges.has(transition)) {
      const targetLane = lanes.get(transition.to[0]!) ?? sourceLane;
      const barLane = outgoing(source).indexOf(transition) === 0 || transition.from.length > 1 ? sourceLane : targetLane;
      return { transition, kind: 'forward' as const, barLane, row, loopIndex: -1 };
    }

    const soleExit = outgoing(source).length === 1;
    const toFirstLane = transition.to.every((target) => (lanes.get(target) ?? 0) === 0);

    if (soleExit && toFirstLane) {
      return { transition, kind: 'loop' as const, barLane: sourceLane, row, loopIndex: loops++ };
    }

    const barLane = soleExit ? sourceLane : freeStubLane(maxLane + 1, row, usedStubs);
    return { transition, kind: 'jump' as const, barLane, row, loopIndex: -1 };
  });
}

function freeStubLane(first: number, row: number, used: Set<string>): number {
  let lane = first;

  while (used.has(`${lane}:${row}`)) {
    lane++;
  }

  used.add(`${lane}:${row}`);
  return lane;
}
