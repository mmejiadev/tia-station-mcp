/**
 * Writes a chart as set/reset LAD, as SIMATIC SD documents (`.s7dcl`) TIA Portal V20 can import.
 *
 * @remarks
 * The method taught for S7-1200, which has no S7-GRAPH (TIA Portal V20 S7-1200 manual, 11/2024:
 * LAD, FBD and SCL only):
 *
 * - one block per sequence, one network per transition: source steps and condition in series,
 *   S on every target step, R on every source step;
 * - an initialisation block run on the first scan: S on the initial steps and one R per other step.
 *   Deliberately not R_BF: a bit range only clears the intended steps if their addresses happen to
 *   be consecutive, and a class project on 2026-09-24 showed how quietly that goes wrong;
 * - an outputs block: one plain coil per output, OR-ing every step that drives it, so no output has
 *   two coils;
 * - a step's on-delay timer as a TON started by the step, whose Q drives a Bool named after it.
 *
 * The syntax is the one TIA Portal V20 itself wrote when exporting LAD on 2026-09-24. A chart with
 * an unconfirmed (`?`) reading, or with an edge, is refused: code is not generated from a guess, and
 * the edge syntax of the format has not been measured.
 */

import { isUncertain, type Action, type Condition, type Grafcet, type Sequence, type Step } from './grafcetModel.ts';

/** One document to write: `name.s7dcl`. */
export interface LadDocument {
  readonly name: string;
  readonly text: string;
}

/** Names of the two shared blocks. The sequence blocks are named after their sequences. */
export interface LadNames {
  readonly initialisation: string;
  readonly outputs: string;
  readonly firstScan: string;
}

/** The defaults: FirstScan is the system memory bit TIA Portal names by default. */
export const DefaultLadNames: LadNames = { initialisation: 'GRAFCET_INIT', outputs: 'GRAFCET_OUTPUTS', firstScan: 'FirstScan' };

/** TIA Portal writes its documents as UTF-8 with a byte order mark, and so does this. */
const ByteOrderMark = String.fromCharCode(0xfeff);

type Literal = { readonly name: string; readonly negated: boolean };

/** Generates the documents. Throws when the chart holds something that must not be generated. */
export function grafcetToLad(grafcet: Grafcet, names: LadNames = DefaultLadNames): readonly LadDocument[] {
  refuseUnsafe(grafcet);
  return [
    { name: names.initialisation, text: block(names.initialisation, grafcet.sequences.map((sequence) => initialisationNetwork(sequence, names.firstScan))) },
    ...grafcet.sequences.map((sequence) => ({ name: sequence.name, text: block(sequence.name, sequenceNetworks(sequence)) })),
    { name: names.outputs, text: block(names.outputs, outputNetworks(grafcet)) },
  ];
}

function refuseUnsafe(grafcet: Grafcet): void {
  for (const sequence of grafcet.sequences) {
    const uncertain = sequence.transitions.find((transition) => isUncertain(transition.condition));

    if (uncertain !== undefined) {
      throw new Error(`${sequence.name}: ${uncertain.from.join(',')} -> ${uncertain.to.join(',')} still has a '?' reading; confirm it before generating code`);
    }

    if (!sequence.steps.some((step) => step.initial)) {
      throw new Error(`${sequence.name}: no initial step, so the generated code could never start`);
    }

    if (sequence.name.includes('"')) {
      throw new Error(`${sequence.name}: a block is named after its sequence, and a block name cannot contain a double quote`);
    }
  }
}

function block(name: string, networks: readonly string[]): string {
  const header = '{\n    S7_Optimized := "TRUE";\n    S7_PreferredLanguage := "LAD";\n    S7_Version := "0.1"\n}\n';
  const body = networks.map((network) => `    { S7_Language := "LAD" }\n    NETWORK\n${network}    END_NETWORK\n`).join('');
  return `${ByteOrderMark}${header}FUNCTION "${name}" : Void\n${body}END_FUNCTION\n`.replace(/\n/g, '\r\n');
}

function rung(start: string, elements: readonly string[], end = ''): string {
  const lines = elements.map((element) => `            ${element}\n`).join('');
  return `        RUNG wire#${start}\n${lines}        END_RUNG${end === '' ? '' : ` wire#${end}`}\n`;
}

/**
 * Contacts in series driving one or more coils. TIA Portal refuses a junction that nothing else uses
 * ("Instruction 'Coil' : Pin 'in' connection is missing", measured 2026-09-24), so a single coil
 * follows the contacts directly and only several coils hang off `w1`.
 */
function withJunction(contacts: readonly string[], coils: readonly string[]): string {
  if (coils.length === 1) {
    return rung('powerrail', [...contacts, coils[0]!]);
  }

  return rung('powerrail', [...contacts, 'wire#w1', coils[0]!]) + coils.slice(1).map((coil) => rung('w1', [coil])).join('');
}

function contact(literal: Literal): string {
  return `${literal.negated ? 'I_Contact' : 'Contact'}( "${literal.name}" )`;
}

function step(number: number): Literal {
  return { name: `X${number}`, negated: false };
}

function initialisationNetwork(sequence: Sequence, firstScan: string): string {
  const initial = sequence.steps.filter((candidate) => candidate.initial);
  const others = sequence.steps.filter((candidate) => !candidate.initial);
  const coils = [...initial.map((candidate) => `S_Coil( "X${candidate.number}" )`), ...others.map((candidate) => `R_Coil( "X${candidate.number}" )`)];
  return withJunction([contact({ name: firstScan, negated: false })], coils);
}

function sequenceNetworks(sequence: Sequence): readonly string[] {
  const timers = sequence.steps.flatMap((candidate) => candidate.actions.filter((action): action is Extract<Action, { kind: 'timer' }> => action.kind === 'timer').map((action) => timerNetwork(candidate, action)));
  const transitions = sequence.transitions.map((transition) => {
    const flow = conditionRungs(transition.from.map(step), transition.condition);
    const sets = transition.to.map((target) => `S_Coil( "X${target}" )`);
    const resets = transition.from.map((source) => rung(flow.end, [`R_Coil( "X${source}" )`]));
    return flow.text(sets[0]!) + sets.slice(1).map((set) => rung(flow.end, [set])).join('') + resets.join('');
  });
  return [...timers, ...transitions];
}

function timerNetwork(owner: Step, action: Extract<Action, { kind: 'timer' }>): string {
  return rung('powerrail', [
    contact(step(owner.number)),
    `"${action.name}_DB".TON(`,
    `    pt := T#${action.seconds}S, `,
    '    et =>  ',
    ')',
    `Coil( "${action.name}" )`,
  ]);
}

/**
 * The rungs that compute "steps AND condition", ending on a junction the coils hang from.
 * A disjunction becomes parallel branches that rejoin, the way TIA Portal exports them.
 */
function conditionRungs(steps: readonly Literal[], condition: Condition): { readonly end: string; readonly text: (firstCoil: string) => string } {
  const terms = disjunctiveTerms(condition);

  if (terms.length === 1) {
    return { end: 'w1', text: (coil) => rung('powerrail', [...[...steps, ...terms[0]!].map(contact), 'wire#w1', coil]) };
  }

  return {
    end: 'w2',
    text: (coil) => rung('powerrail', [...steps.map(contact), 'wire#w1', ...terms[0]!.map(contact), 'wire#w2', coil])
      + terms.slice(1).map((term) => rung('w1', term.map(contact), 'w2')).join(''),
  };
}

/** The condition as an OR of ANDs of literals — the only shape a rung of contacts can hold. */
function disjunctiveTerms(condition: Condition): readonly (readonly Literal[])[] {
  switch (condition.kind) {
    case 'true':
      return [[]];
    case 'variable':
      return [[{ name: condition.name, negated: false }]];
    case 'not':
      return [[negatedLiteral(condition.term)]];
    case 'or':
      return condition.terms.flatMap(disjunctiveTerms);
    case 'and':
      return condition.terms.map(disjunctiveTerms).reduce<readonly (readonly Literal[])[]>((product, part) => product.flatMap((left) => part.map((right) => [...left, ...right])), [[]]);
    case 'rising':
    case 'falling':
      throw new Error('edges (↑ ↓) are not generated: the edge syntax of the document format has not been measured; use P_TRIG by hand');
    default:
      throw new Error(`Unrecognised condition: ${JSON.stringify(condition satisfies never)}`);
  }
}

function negatedLiteral(term: Condition): Literal {
  if (term.kind !== 'variable') {
    throw new Error('only a single variable can be negated in a contact; rewrite /(A.B) as /A + /B');
  }

  return { name: term.name, negated: true };
}

function outputNetworks(grafcet: Grafcet): readonly string[] {
  const steps = grafcet.sequences.flatMap((sequence) => sequence.steps);
  const outputs = [...new Set(steps.flatMap((candidate) => candidate.actions.filter((action) => action.kind === 'output').map((action) => action.name)))];
  const continuous = outputs.map((output) => outputNetwork(steps, output));
  const stored = steps.flatMap((candidate) => candidate.actions
    .filter((action): action is Extract<Action, { kind: 'set' | 'reset' }> => action.kind === 'set' || action.kind === 'reset')
    .map((action) => rung('powerrail', [contact(step(candidate.number)), `${action.kind === 'set' ? 'S_Coil' : 'R_Coil'}( "${action.name}" )`])));
  return [...continuous, ...stored];
}

function outputNetwork(steps: readonly Step[], output: string): string {
  const branches = steps.flatMap((candidate) => candidate.actions
    .filter((action): action is Extract<Action, { kind: 'output' }> => action.kind === 'output' && action.name === output)
    .flatMap((action) => disjunctiveTerms(action.condition ?? { kind: 'true' }).map((term) => [step(candidate.number), ...term])));
  const coil = `Coil( "${output}" )`;

  if (branches.length === 1) {
    return rung('powerrail', [...branches[0]!.map(contact), coil]);
  }

  const first = rung('powerrail', [...branches[0]!.map(contact), 'wire#w1', coil]);
  return first + branches.slice(1).map((branch) => rung('powerrail', branch.map(contact), 'w1')).join('');
}
