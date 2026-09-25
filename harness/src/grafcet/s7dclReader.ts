/**
 * Reads LAD blocks exported by TIA Portal V20 as SIMATIC SD documents (`.s7dcl`) into operations.
 *
 * @remarks
 * The document is text: a block holds networks, a network holds rungs, and a rung is a column of
 * elements that starts on a wire and may end on one. `RUNG wire#w1 ... END_RUNG` hangs off the
 * junction `w1`; `... END_RUNG wire#w1` feeds a parallel branch into it. A junction's value is
 * therefore the OR of every flow that reaches it, and a rung written *earlier* can read a junction
 * that a *later* rung feeds — so the flows are collected first and resolved afterwards.
 *
 * Measured on 2026-09-24 against TIA Portal V20: `ExportAsDocuments` writes this format even for a
 * block that does not compile, which is why an operand TIA could not resolve arrives here unquoted,
 * as in `Contact( I )`. That is reported, not repaired.
 *
 * Siemens publishes no grammar for the format. Anything this reader does not recognise is reported
 * as unsupported, never guessed at.
 */

import { and, factorCommon, negate, or, variable, type Condition, Always } from './grafcetModel.ts';
import { joinElements, readPreset } from './s7dclElements.ts';

/** Where an operation or a finding sits, as TIA Portal numbers it. */
export interface LadLocation {
  readonly block: string;
  readonly network: number;
}

/** One effect of a rung, with the condition under which it happens. */
export type LadOperation =
  | (LadLocation & { readonly kind: 'set' | 'reset' | 'assign'; readonly operand: string; readonly condition: Condition })
  | (LadLocation & { readonly kind: 'setRange' | 'resetRange'; readonly operand: string; readonly count: number; readonly condition: Condition })
  | (LadLocation & { readonly kind: 'timer'; readonly instance: string; readonly type: string; readonly presetSeconds: number | null; readonly condition: Condition })
  | (LadLocation & { readonly kind: 'call'; readonly target: string; readonly condition: Condition });

/** Something in the code a person should look at. */
export interface LadFinding extends LadLocation {
  readonly severity: 'error' | 'warning';
  readonly message: string;
}

/** A block as read: what it does and what looked wrong. */
export interface LadBlock {
  readonly name: string;
  readonly operations: readonly LadOperation[];
  readonly findings: readonly LadFinding[];
}

interface Flow {
  readonly base: string;
  readonly contacts: readonly Condition[];
}

type Pending = { readonly flow: Flow; readonly make: (condition: Condition) => LadOperation };

const LeadingByteOrderMark = new RegExp(`^${String.fromCharCode(0xfeff)}`);
const PowerRail = 'powerrail';
const TimerOutputPrefix = '@';
const HeaderPattern = /^(?:FUNCTION|FUNCTION_BLOCK|ORGANIZATION_BLOCK)\s+"([^"]+)"/;
const RungPattern = /^RUNG\s+wire#(\w+)$/;
const EndRungPattern = /^END_RUNG(?:\s+wire#(\w+))?$/;
const ContactPattern = /^(Contact|I_Contact)\(\s*(.*?)\s*\)$/;
const CoilPattern = /^(S_Coil|R_Coil|Coil)\(\s*(.*?)\s*\)$/;
const BitfieldPattern = /^(S_BitfieldCoil|R_BitfieldCoil)\(\s*operand\s*=>\s*(.*?)\s*,\s*n\s*:=\s*(\d+)\s*\)$/;
const TimerPattern = /^"([^"]+)"\.(TON|TOF|TP)\((.*)\)$/;
const CallPattern = /^"([^"]+)"\(\s*\)$/;
const JunctionPattern = /^wire#(\w+)$/;

/** Reads one `.s7dcl` document. */
export function readS7dcl(text: string): LadBlock {
  const lines = text.replace(LeadingByteOrderMark, '').split(/\r?\n/).map((line) => line.trim());
  const name = lines.map((line) => HeaderPattern.exec(line)?.[1]).find((found) => found !== undefined) ?? 'unnamed block';
  const operations: LadOperation[] = [];
  const findings: LadFinding[] = [];

  splitNetworks(lines).forEach((networkLines, index) => {
    const network = new NetworkReader({ block: name, network: index + 1 }, findings);
    operations.push(...network.read(networkLines));
  });

  return { name, operations, findings };
}

function splitNetworks(lines: readonly string[]): readonly (readonly string[])[] {
  const networks: string[][] = [];
  let current: string[] | null = null;

  for (const line of lines) {
    if (line === 'NETWORK') {
      current = [];
      networks.push(current);
      continue;
    }

    if (line === 'END_NETWORK') {
      current = null;
      continue;
    }

    current?.push(line);
  }

  return networks;
}

/** Reads the rungs of one network and resolves every junction once all of them are known. */
class NetworkReader {
  private readonly _location: LadLocation;
  private readonly _findings: LadFinding[];
  private readonly _contributions = new Map<string, Flow[]>();
  private readonly _pending: Pending[] = [];

  public constructor(location: LadLocation, findings: LadFinding[]) {
    this._location = location;
    this._findings = findings;
  }

  public read(lines: readonly string[]): readonly LadOperation[] {
    let flow: Flow | null = null;

    for (const element of joinElements(lines)) {
      const rung = RungPattern.exec(element);
      const end = EndRungPattern.exec(element);

      if (rung !== null) {
        flow = { base: rung[1]!, contacts: [] };
        continue;
      }

      if (end !== null && flow !== null && end[1] !== undefined) {
        this.contribute(end[1], flow);
      }

      flow = end !== null || flow === null ? null : this.readElement(element, flow);
    }

    return this._pending.map((pending) => pending.make(factorCommon(this.resolve(pending.flow, new Set()))));
  }

  private readElement(element: string, flow: Flow): Flow {
    const contact = ContactPattern.exec(element);

    if (contact !== null) {
      const operand = variable(this.operandName(contact[2]!));
      return { base: flow.base, contacts: [...flow.contacts, contact[1] === 'I_Contact' ? negate(operand) : operand] };
    }

    const junction = JunctionPattern.exec(element);

    if (junction !== null) {
      this.contribute(junction[1]!, flow);
      return { base: junction[1]!, contacts: [] };
    }

    return this.readEffect(element, flow);
  }

  private readEffect(element: string, flow: Flow): Flow {
    const coil = CoilPattern.exec(element);

    if (coil !== null) {
      const kind = coil[1] === 'S_Coil' ? 'set' : coil[1] === 'R_Coil' ? 'reset' : 'assign';
      const operand = this.operandName(coil[2]!);
      this.defer(flow, (condition) => ({ ...this._location, kind, operand, condition }));
      return flow;
    }

    const bitfield = BitfieldPattern.exec(element);

    if (bitfield !== null) {
      const kind = bitfield[1] === 'S_BitfieldCoil' ? 'setRange' : 'resetRange';
      const operand = this.operandName(bitfield[2]!);
      const count = Number(bitfield[3]);
      this.defer(flow, (condition) => ({ ...this._location, kind, operand, count, condition }));
      return flow;
    }

    return this.readBox(element, flow);
  }

  private readBox(element: string, flow: Flow): Flow {
    const timer = TimerPattern.exec(element);

    if (timer !== null) {
      const instance = timer[1]!;
      const presetSeconds = readPreset(timer[3]!);
      this.defer(flow, (condition) => ({ ...this._location, kind: 'timer', instance, type: timer[2]!, presetSeconds, condition }));
      return this.afterTimer(timer[2]!, instance, flow);
    }

    const call = CallPattern.exec(element);

    if (call !== null) {
      const target = call[1]!;
      this.defer(flow, (condition) => ({ ...this._location, kind: 'call', target, condition }));
      return flow;
    }

    this.report('warning', `unsupported LAD element, left out of the analysis: ${element}`);
    return flow;
  }

  /**
   * The flow leaving a timer box. A TON's Q can only be true while its IN is, so what follows the
   * box reads "IN and Q" — which is what lets `X36 → TON → S X37 / R X36` be recognised as the
   * transition 36 → 37. A TOF or TP keeps Q after IN drops, so for those only Q is carried on.
   */
  private afterTimer(type: string, instance: string, input: Flow): Flow {
    const done = variable(`${instance}.Q`);

    if (type !== 'TON') {
      return { base: TimerOutputPrefix + instance + '.Q', contacts: [] };
    }

    const inputWire = `ton#${instance}`;
    this.contribute(inputWire, input);
    return { base: inputWire, contacts: [done] };
  }

  private operandName(text: string): string {
    const quoted = /^"([^"]*)"$/.exec(text);

    if (quoted !== null) {
      return quoted[1]!;
    }

    if (!text.startsWith('%')) {
      this.report('error', `operand ${text} is not a tag: TIA reads an unquoted name as an address. Type it as "${text}" or pick the tag from the list`);
    }

    return text;
  }

  private contribute(wire: string, flow: Flow): void {
    const list = this._contributions.get(wire) ?? [];
    list.push(flow);
    this._contributions.set(wire, list);
  }

  private defer(flow: Flow, make: (condition: Condition) => LadOperation): void {
    this._pending.push({ flow, make });
  }

  private resolve(flow: Flow, visiting: Set<string>): Condition {
    return and([this.wireValue(flow.base, visiting), ...flow.contacts]);
  }

  private wireValue(wire: string, visiting: Set<string>): Condition {
    if (wire === PowerRail) {
      return Always;
    }

    if (wire.startsWith(TimerOutputPrefix)) {
      return variable(wire.slice(TimerOutputPrefix.length));
    }

    if (visiting.has(wire)) {
      throw new Error(`${this._location.block} network ${this._location.network}: wire ${wire} feeds itself`);
    }

    const flows = this._contributions.get(wire) ?? [];
    const inner = new Set([...visiting, wire]);
    return or(flows.map((flow) => this.resolve(flow, inner)));
  }

  private report(severity: 'error' | 'warning', message: string): void {
    this._findings.push({ ...this._location, severity, message });
  }
}
