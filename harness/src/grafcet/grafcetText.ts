/**
 * The text notation a chart is transcribed into before it is checked, drawn or turned into LAD.
 *
 * @remarks
 * One statement per line; `#` starts a comment.
 *
 * ```text
 * title Weighing cell
 * sequence Conveyor E
 * step 0 initial
 * step 1 : MCE
 * step 36 : timer T36 30s
 * step 5 : LAMP if /DOOR, set ALARM
 * 0 -> 1 : I
 * 1 -> 0 : /I
 * 37 -> 42 : PS.?/BASCULA
 * 5,6 -> 7 : 1
 * ```
 *
 * A transition may list several sources (AND convergence) or several targets (AND divergence).
 * A step that a transition names but no `step` line declares is created with no actions, because a
 * whiteboard often leaves an actionless step undeclared; an initial step must always be declared,
 * since guessing which step is initial is exactly the mistake the notation exists to prevent.
 */

import { formatCondition, parseCondition } from './conditionText.ts';
import type { Action, Grafcet, Sequence, Step, Transition } from './grafcetModel.ts';

/**
 * Writes a chart in the text notation. Reading the result back gives the same chart, which is what
 * makes the notation safe to hand to a person for correction and read back afterwards.
 */
export function formatGrafcet(grafcet: Grafcet): string {
  const lines = [`title ${grafcet.title}`];

  for (const sequence of grafcet.sequences) {
    lines.push('', `sequence ${sequence.name}`);
    lines.push(...sequence.steps.map((step) => `step ${step.number}${step.initial ? ' initial' : ''}${step.actions.length > 0 ? ' : ' + step.actions.map(formatAction).join(', ') : ''}`));
    lines.push(...sequence.transitions.map((transition) => `${transition.from.join(',')} -> ${transition.to.join(',')} : ${formatCondition(transition.condition)}`));
  }

  return lines.join('\n') + '\n';
}

function formatAction(action: Action): string {
  switch (action.kind) {
    case 'output':
      return action.condition === null ? action.name : `${action.name} if ${formatCondition(action.condition)}`;
    case 'set':
    case 'reset':
      return `${action.kind} ${action.name}`;
    case 'timer':
      return `timer ${action.name} ${action.seconds}s`;
    default:
      throw new Error(`Unrecognised action: ${JSON.stringify(action satisfies never)}`);
  }
}

/** The outcome of reading a chart: the chart, or every error found, each naming its line. */
export type GrafcetParse =
  | { readonly ok: true; readonly grafcet: Grafcet }
  | { readonly ok: false; readonly errors: readonly string[] };

interface SequenceDraft {
  readonly name: string;
  readonly steps: Map<number, Step>;
  readonly transitions: Transition[];
}

const StepPattern = /^step\s+(\d+)(\s+initial)?\s*(?::\s*(.*))?$/;
const TransitionPattern = /^([\d\s,]+)->([\d\s,]+):(.*)$/;
const TimerPattern = /^timer\s+([A-Za-z_]\w*)\s+(\d+(?:\.\d+)?)\s*s$/;
const StoredPattern = /^(set|reset)\s+([A-Za-z_]\w*)$/;
const ConditionalPattern = /^([A-Za-z_]\w*)\s+if\s+(.+)$/;
const OutputPattern = /^[A-Za-z_]\w*$/;

/** Reads a chart written in the text notation. */
export function parseGrafcet(text: string): GrafcetParse {
  const drafts: SequenceDraft[] = [];
  const errors: string[] = [];
  let title = 'GRAFCET';

  text.split(/\r?\n/).forEach((raw, index) => {
    const line = raw.replace(/#.*$/, '').trim();
    const failure = line === '' ? null : readStatement(line, drafts, (value) => { title = value; });

    if (failure !== null) {
      errors.push(`line ${index + 1}: ${failure}`);
    }
  });

  return errors.length > 0 ? { ok: false, errors } : finish(title, drafts);
}

function readStatement(line: string, drafts: SequenceDraft[], setTitle: (value: string) => void): string | null {
  if (line.startsWith('title ')) {
    setTitle(line.slice('title '.length).trim());
    return null;
  }

  if (line.startsWith('sequence ')) {
    drafts.push({ name: line.slice('sequence '.length).trim(), steps: new Map(), transitions: [] });
    return null;
  }

  const current = drafts.at(-1);

  if (current === undefined) {
    return "a 'sequence' line must come before any step or transition";
  }

  return line.startsWith('step') ? readStep(line, current) : readTransition(line, current);
}

function readStep(line: string, draft: SequenceDraft): string | null {
  const match = StepPattern.exec(line);

  if (match === null) {
    return "a step is written 'step <number> [initial] [: action, action]'";
  }

  const number = Number(match[1]);
  const actions = readActions(match[3] ?? '');

  if (typeof actions === 'string') {
    return `step ${number}: ${actions}`;
  }

  // Steps a transition names are only created in finish(), so anything already here was declared.
  if (draft.steps.has(number)) {
    return `step ${number} is declared twice`;
  }

  draft.steps.set(number, { number, initial: match[2] !== undefined, actions });
  return null;
}

function readActions(text: string): readonly Action[] | string {
  const actions: Action[] = [];

  for (const item of text.split(',').map((part) => part.trim()).filter((part) => part !== '')) {
    const action = readAction(item);

    if (typeof action === 'string') {
      return action;
    }

    actions.push(action);
  }

  return actions;
}

function readAction(item: string): Action | string {
  const timer = TimerPattern.exec(item);

  if (timer !== null) {
    return { kind: 'timer', name: timer[1]!, seconds: Number(timer[2]) };
  }

  const stored = StoredPattern.exec(item);

  if (stored !== null) {
    return { kind: stored[1] === 'set' ? 'set' : 'reset', name: stored[2]! };
  }

  return readOutput(item);
}

function readOutput(item: string): Action | string {
  const conditional = ConditionalPattern.exec(item);

  if (conditional === null) {
    return OutputPattern.test(item) ? { kind: 'output', name: item, condition: null } : `cannot read the action '${item}'`;
  }

  const condition = parseCondition(conditional[2]!);
  return condition.ok ? { kind: 'output', name: conditional[1]!, condition: condition.condition } : `${item}: ${condition.error}`;
}

function readTransition(line: string, draft: SequenceDraft): string | null {
  const match = TransitionPattern.exec(line);

  if (match === null) {
    return `cannot read '${line}': a transition is written '<from> -> <to> : <condition>'`;
  }

  const condition = parseCondition(match[3]!.trim());

  if (!condition.ok) {
    return `transition ${match[1]!.trim()} -> ${match[2]!.trim()}: ${condition.error}`;
  }

  draft.transitions.push({ from: readNumbers(match[1]!), to: readNumbers(match[2]!), condition: condition.condition });
  return null;
}

function readNumbers(text: string): readonly number[] {
  return text.split(',').map((part) => part.trim()).filter((part) => part !== '').map(Number);
}

function finish(title: string, drafts: readonly SequenceDraft[]): GrafcetParse {
  const sequences: Sequence[] = drafts.map((draft) => {
    for (const number of draft.transitions.flatMap((transition) => [...transition.from, ...transition.to])) {
      if (!draft.steps.has(number)) {
        draft.steps.set(number, { number, initial: false, actions: [] });
      }
    }

    const steps = [...draft.steps.values()].sort((left, right) => left.number - right.number);
    return { name: draft.name, steps, transitions: draft.transitions };
  });

  return { ok: true, grafcet: { title, sequences } };
}
