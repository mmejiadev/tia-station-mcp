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
 * - a step's on-delay timer as a TON started by the step, whose Q drives a Bool named after it;
 * - an edge (`↑A`, `↓A`) as a P or N contact with a memory bit of its own: `A_FP1`, `A_FN1`, ...
 *
 * The syntax is the one TIA Portal V20 itself wrote when exporting LAD on 2026-09-24. The edge
 * contact, `P_Contact( operand := "A", bit := "A_FP1" )`, was measured by importing it into a copy
 * of a class project on 2026-10-08; the positional form is refused ("Pin 'bit' connection is
 * missing"). A chart with an unconfirmed (`?`) reading is refused: code is not generated from a guess.
 *
 * An edge contact is written exactly once per edge. Two P contacts on one memory bit see a single
 * edge between them — the first one updates the bit, so the second finds no change — and that is
 * what a class exercise produced on 2026-10-08 when `X10.↑MARXA.(X21 + X22 + X23)` was expanded into
 * three branches. Whatever every branch shares stands in series in front of them; an edge that would
 * still be copied makes the chart refused.
 */

import { allocateEdgeBits, edgeOperand, type EdgeBits } from './edgeMemoryBits.ts';
import { findEdgeCascadePairs } from './grafcetCheck.ts';
import { isUncertain, type Action, type Condition, type Grafcet, type Sequence, type Step } from './grafcetModel.ts';

/** One document to write: `name.s7dcl`. */
export interface LadDocument {
  readonly name: string;
  readonly text: string;
}

/** What was generated: the documents, and the edge memory bits they need as Bool tags. */
export interface LadProgram {
  readonly documents: readonly LadDocument[];
  readonly edgeMemoryBits: readonly string[];
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

type Literal =
  | { readonly kind: 'contact'; readonly name: string; readonly negated: boolean }
  | { readonly kind: 'edge'; readonly name: string; readonly rising: boolean; readonly bit: string };

/** Generates the documents. Throws when the chart holds something that must not be generated. */
export function grafcetToLad(grafcet: Grafcet, names: LadNames = DefaultLadNames): readonly LadDocument[] {
  return generateLad(grafcet, names).documents;
}

/** Generates the documents and names the edge memory bits they use, from one allocation. */
export function generateLad(grafcet: Grafcet, names: LadNames = DefaultLadNames): LadProgram {
  refuseUnsafe(grafcet);
  const bits = allocateEdgeBits(grafcet);
  const initialisation = [...grafcet.sequences.map((sequence) => initialisationNetwork(sequence, names.firstScan)), ...edgeInitialisationNetworks(bits, names.firstScan)];
  const documents = [
    { name: names.initialisation, text: block(names.initialisation, initialisation) },
    ...grafcet.sequences.map((sequence) => ({ name: sequence.name, text: block(sequence.name, sequenceNetworks(sequence, bits)) })),
    { name: names.outputs, text: block(names.outputs, outputNetworks(grafcet, bits)) },
  ];
  return { documents, edgeMemoryBits: [...bits.values()] };
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

    const cascade = findEdgeCascadePairs(sequence)[0];

    if (cascade !== undefined) {
      throw new Error(`${sequence.name}: ${cascade.entering.from.join(',')} -> ${cascade.entering.to.join(',')} and ${cascade.leaving.from.join(',')} -> ${cascade.leaving.to.join(',')} both wait for ${cascade.edge}, and in set/reset LAD one edge would clear both in the same scan; wait for a different signal in one of them`);
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
  if (literal.kind === 'edge') {
    return `${literal.rising ? 'P_Contact' : 'N_Contact'}( operand := "${literal.name}", bit := "${literal.bit}" )`;
  }

  return `${literal.negated ? 'I_Contact' : 'Contact'}( "${literal.name}" )`;
}

function step(number: number): Literal {
  return { kind: 'contact', name: `X${number}`, negated: false };
}

function initialisationNetwork(sequence: Sequence, firstScan: string): string {
  const initial = sequence.steps.filter((candidate) => candidate.initial);
  const others = sequence.steps.filter((candidate) => !candidate.initial);
  const coils = [...initial.map((candidate) => `S_Coil( "X${candidate.number}" )`), ...others.map((candidate) => `R_Coil( "X${candidate.number}" )`)];
  return withJunction([contact({ kind: 'contact', name: firstScan, negated: false })], coils);
}

/**
 * On the first scan every edge memory bit takes its operand's present state. A bit left at 0 would
 * see an input already true at start-up as a rising edge and clear a transition nobody triggered.
 * Two plain networks, set and reset, rather than a coil: a coil would rewrite the bit on every scan.
 */
function edgeInitialisationNetworks(bits: EdgeBits, firstScan: string): readonly string[] {
  const scan: Literal = { kind: 'contact', name: firstScan, negated: false };
  return [...bits.entries()].flatMap(([edge, bit]) => {
    const operand = edgeOperand(edge.term);
    return [
      rung('powerrail', [contact(scan), contact({ kind: 'contact', name: operand, negated: false }), `S_Coil( "${bit}" )`]),
      rung('powerrail', [contact(scan), contact({ kind: 'contact', name: operand, negated: true }), `R_Coil( "${bit}" )`]),
    ];
  });
}

function sequenceNetworks(sequence: Sequence, bits: EdgeBits): readonly string[] {
  const timers = sequence.steps.flatMap((candidate) => candidate.actions.filter((action): action is Extract<Action, { kind: 'timer' }> => action.kind === 'timer').map((action) => timerNetwork(candidate, action)));
  const transitions = sequence.transitions.map((transition) => {
    const flow = conditionRungs(transition.from.map(step), transition.condition, bits);
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
 * A disjunction becomes parallel branches that rejoin, the way TIA Portal exports them; whatever
 * every branch shares stands in series in front of them, written once.
 */
function conditionRungs(steps: readonly Literal[], condition: Condition, bits: EdgeBits): { readonly end: string; readonly text: (firstCoil: string) => string } {
  const { common, branches } = factorShared(disjunctiveTerms(condition, bits));
  refuseCopiedEdges(branches);
  const series = [...steps, ...common];

  if (branches.length === 1) {
    return { end: 'w1', text: (coil) => rung('powerrail', [...[...series, ...branches[0]!].map(contact), 'wire#w1', coil]) };
  }

  return {
    end: 'w2',
    text: (coil) => rung('powerrail', [...series.map(contact), 'wire#w1', ...branches[0]!.map(contact), 'wire#w2', coil])
      + branches.slice(1).map((branch) => rung('w1', branch.map(contact), 'w2')).join(''),
  };
}

/** Takes the literals every term holds out of the terms: `A.B + A.C` is `A` in front of `B + C`. */
function factorShared(terms: readonly (readonly Literal[])[]): { readonly common: readonly Literal[]; readonly branches: readonly (readonly Literal[])[] } {
  if (terms.length === 1) {
    return { common: [], branches: terms };
  }

  const common = terms[0]!.filter((literal) => terms.every((term) => term.some((other) => sameLiteral(other, literal))));
  const branches = terms.map((term) => term.filter((literal) => !common.some((shared) => sameLiteral(shared, literal))));

  // A branch left with nothing is always true, so the whole OR is: `A + A.B` is `A`. Writing it out
  // would leave an empty rung between two junctions, a shape TIA Portal has never been seen to take.
  return branches.some((branch) => branch.length === 0) ? { common, branches: [[]] } : { common, branches };
}

/**
 * An edge left in more than one branch would be written as several contacts on one memory bit. That
 * happens when the edge sits in an OR that is ANDed with another OR, `(↑A + B).(C + D)`, and there is
 * no series form to fall back on, so the chart is refused rather than generated wrong.
 */
function refuseCopiedEdges(branches: readonly (readonly Literal[])[]): void {
  const bits = branches.flatMap((branch) => branch.flatMap((literal) => literal.kind === 'edge' ? [literal.bit] : []));
  const copied = bits.find((bit, index) => bits.indexOf(bit) !== index);

  if (copied !== undefined) {
    throw new Error(`the edge on ${copied} would be copied into several parallel branches, and copies of one edge contact see a single edge between them; rewrite the condition so the edge is ANDed with all of it`);
  }
}

function sameLiteral(left: Literal, right: Literal): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

/** The condition as an OR of ANDs of literals — the only shape a rung of contacts can hold. */
function disjunctiveTerms(condition: Condition, bits: EdgeBits): readonly (readonly Literal[])[] {
  switch (condition.kind) {
    case 'true':
      return [[]];
    case 'variable':
      return [[{ kind: 'contact', name: condition.name, negated: false }]];
    case 'not':
      return [[negatedLiteral(condition.term)]];
    case 'or':
      return condition.terms.flatMap((term) => disjunctiveTerms(term, bits));
    case 'and':
      return condition.terms.map((term) => disjunctiveTerms(term, bits)).reduce<readonly (readonly Literal[])[]>((product, part) => product.flatMap((left) => part.map((right) => [...left, ...right])), [[]]);
    case 'rising':
    case 'falling':
      return [[edgeLiteral(condition, bits)]];
    default:
      throw new Error(`Unrecognised condition: ${JSON.stringify(condition satisfies never)}`);
  }
}

function edgeLiteral(edge: Extract<Condition, { kind: 'rising' | 'falling' }>, bits: EdgeBits): Literal {
  // Every edge in a transition was given a bit, so a missing one is an edge in an action condition.
  const bit = bits.get(edge);

  if (bit === undefined) {
    throw new Error('edges (↑ ↓) are generated in transitions only; an action is driven by its step, not by an edge');
  }

  return { kind: 'edge', name: edgeOperand(edge.term), rising: edge.kind === 'rising', bit };
}

function negatedLiteral(term: Condition): Literal {
  if (term.kind === 'rising' || term.kind === 'falling') {
    throw new Error('a negated edge (/↑A) has no contact; an edge is true for one scan, so write the condition without the negation');
  }

  if (term.kind !== 'variable') {
    throw new Error('only a single variable can be negated in a contact; rewrite /(A.B) as /A + /B');
  }

  return { kind: 'contact', name: term.name, negated: true };
}

function outputNetworks(grafcet: Grafcet, bits: EdgeBits): readonly string[] {
  const steps = grafcet.sequences.flatMap((sequence) => sequence.steps);
  const outputs = [...new Set(steps.flatMap((candidate) => candidate.actions.filter((action) => action.kind === 'output').map((action) => action.name)))];
  const continuous = outputs.map((output) => outputNetwork(steps, output, bits));
  const stored = steps.flatMap((candidate) => candidate.actions
    .filter((action): action is Extract<Action, { kind: 'set' | 'reset' }> => action.kind === 'set' || action.kind === 'reset')
    .map((action) => rung('powerrail', [contact(step(candidate.number)), `${action.kind === 'set' ? 'S_Coil' : 'R_Coil'}( "${action.name}" )`])));
  return [...continuous, ...stored];
}

function outputNetwork(steps: readonly Step[], output: string, bits: EdgeBits): string {
  const branches = steps.flatMap((candidate) => candidate.actions
    .filter((action): action is Extract<Action, { kind: 'output' }> => action.kind === 'output' && action.name === output)
    .flatMap((action) => disjunctiveTerms(action.condition ?? { kind: 'true' }, bits).map((term) => [step(candidate.number), ...term])));
  const coil = `Coil( "${output}" )`;

  if (branches.length === 1) {
    return rung('powerrail', [...branches[0]!.map(contact), coil]);
  }

  const first = rung('powerrail', [...branches[0]!.map(contact), 'wire#w1', coil]);
  return first + branches.slice(1).map((branch) => rung('powerrail', branch.map(contact), 'w1')).join('');
}
