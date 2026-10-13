import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatCondition } from '../../src/grafcet/conditionText.ts';
import { generateLad, grafcetToLad } from '../../src/grafcet/grafcetToLad.ts';
import { parseGrafcet } from '../../src/grafcet/grafcetText.ts';
import type { Grafcet } from '../../src/grafcet/grafcetModel.ts';
import { ladToGrafcet } from '../../src/grafcet/ladToGrafcet.ts';
import { readS7dcl } from '../../src/grafcet/s7dclReader.ts';

const Cell = `title Cell
sequence FEED
step 70 initial
step 71 : MOTOR
step 72 : timer T72 2s, LAMP if DOOR
70 -> 71 : START./STOP
71 -> 72 : SENSOR + BYPASS
72 -> 70 : T72
sequence LIFT
step 80 initial
step 81 : UP
80 -> 81 : X72
81 -> 80 : TOP
`;

function read(text: string): Grafcet {
  const parsed = parseGrafcet(text);
  assert.ok(parsed.ok, parsed.ok ? '' : parsed.errors.join('; '));

  return parsed.grafcet;
}

function summary(grafcet: Grafcet): string[] {
  return grafcet.sequences.flatMap((sequence) => sequence.transitions.map((t) => `${t.from.join(',')} -> ${t.to.join(',')} : ${formatCondition(t.condition)}`)).sort();
}

describe('GRAFCET to set/reset LAD', () => {
  it('generates code that reads back as the same chart', () => {
    // The strongest check available without a PLC: the reader, which was built against documents
    // TIA Portal wrote, recovers from the generated code exactly the transitions it was given.
    const grafcet = read(Cell);

    const blocks = grafcetToLad(grafcet).map((document) => readS7dcl(document.text));
    const back = ladToGrafcet(blocks, 'Cell');

    assert.deepEqual(summary(back.grafcet), summary(grafcet));
    assert.deepEqual(back.findings, []);
  });

  it('clears every non-initial step one by one, never with a bit range', () => {
    const init = grafcetToLad(read(Cell)).find((document) => document.name === 'GRAFCET_INIT')!;

    assert.ok(!init.text.includes('BitfieldCoil'));
    assert.equal((init.text.match(/R_Coil/g) ?? []).length, 3);
  });

  it('gives each output exactly one coil', () => {
    const outputs = grafcetToLad(read(Cell)).find((document) => document.name === 'GRAFCET_OUTPUTS')!;

    assert.equal((outputs.text.match(/Coil\( "MOTOR" \)/g) ?? []).length, 1);
  });

  it('writes documents the way TIA Portal does: byte order mark and CRLF', () => {
    const document = grafcetToLad(read(Cell))[0]!;

    assert.equal(document.text.charCodeAt(0), 0xfeff);
    assert.ok(!/[^\r]\n/.test(document.text));
  });

  it('never writes a junction that nothing else uses, which TIA Portal refuses to import', () => {
    // Measured on 2026-09-24: a lone wire#w1 before a coil fails with "Instruction 'Coil' : Pin 'in'
    // connection is missing". Every junction must be shared by another rung.
    for (const document of grafcetToLad(read(Cell))) {
      for (const network of document.text.split('NETWORK').slice(1)) {
        const lines = network.split(String.fromCharCode(10)).map((line) => line.trim());
        const junctions = lines.filter((line) => line.startsWith('wire#')).map((line) => line.slice('wire#'.length));

        for (const junction of junctions) {
          const reused = lines.some((line) => line === `RUNG wire#${junction}` || line === `END_RUNG wire#${junction}`);
          assert.ok(reused, `${document.name}: wire#${junction} is used once`);
        }
      }
    }
  });

  it('refuses to generate from an unconfirmed reading', () => {
    const unsure = read('sequence S\nstep 0 initial\n0 -> 1 : ?/A\n1 -> 0 : A\n');

    assert.throws(() => grafcetToLad(unsure), /confirm it before generating code/);
  });

  it('refuses a sequence that could never start', () => {
    const headless = read('sequence S\n0 -> 1 : A\n1 -> 0 : /A\n');

    assert.throws(() => grafcetToLad(headless), /no initial step/);
  });

  it('refuses a sequence name that would break the block name', () => {
    const quoted = read(['sequence A "B"', 'step 0 initial', '0 -> 1 : A', '1 -> 0 : /A'].join(String.fromCharCode(10)));

    assert.throws(() => grafcetToLad(quoted), /double quote/);
  });

  it('writes an edge ANDed with an OR once, in series, never once per branch', () => {
    // The class exercise of 2026-10-08: X10.↑MARXA.(X21 + X22 + X23) came out as three branches each
    // holding a P contact on one memory bit, and three such contacts see a single edge between them.
    const chart = read('sequence S\nstep 10 initial\nstep 11\n10 -> 11 : ↑MARXA.(X21 + X22 + X23)\n11 -> 10 : /MARXA\n');

    const sequence = grafcetToLad(chart).find((document) => document.name === 'S')!;

    assert.equal((sequence.text.match(/P_Contact\( operand := "MARXA", bit := "MARXA_FP1" \)/g) ?? []).length, 1);
  });

  it('reads generated edges back as the same chart', () => {
    const chart = read('sequence S\nstep 10 initial\nstep 11\n10 -> 11 : ↑MARXA.(X21 + X22 + X23)\n11 -> 10 : ↓MARXA\n');

    const blocks = grafcetToLad(chart).map((document) => readS7dcl(document.text));
    const back = ladToGrafcet(blocks, 'S');

    assert.deepEqual(summary(back.grafcet), summary(chart));
    assert.deepEqual(back.findings, []);
  });

  it('gives every edge a memory bit of its own and lists them as tags to create', () => {
    const chart = read('sequence S\nstep 0 initial\nstep 1\nstep 2\nstep 3\n0 -> 1 : ↑A\n1 -> 2 : B\n2 -> 3 : ↑A\n3 -> 0 : ↓A\n');

    assert.deepEqual(generateLad(chart).edgeMemoryBits, ['A_FP1', 'A_FP2', 'A_FN1']);
  });

  it('never gives an edge a memory bit the chart already uses as a signal', () => {
    // An edge contact rewrites its bit every scan, so reusing a real tag would corrupt it.
    const chart = read('sequence S\nstep 0 initial\nstep 1\n0 -> 1 : ↑A\n1 -> 0 : A_FP1\n');

    assert.deepEqual(generateLad(chart).edgeMemoryBits, ['A_FP2']);
  });

  it('sets every edge memory bit to its operand on the first scan, so start-up is not an edge', () => {
    const init = grafcetToLad(read('sequence S\nstep 0 initial\nstep 1\n0 -> 1 : ↑A\n1 -> 0 : /A\n')).find((document) => document.name === 'GRAFCET_INIT')!;
    const lines = init.text.split(String.fromCharCode(13, 10)).map((line) => line.trim());

    assert.ok(lines.join('|').includes('Contact( "FirstScan" )|Contact( "A" )|S_Coil( "A_FP1" )'));
    assert.ok(lines.join('|').includes('Contact( "FirstScan" )|I_Contact( "A" )|R_Coil( "A_FP1" )'));
  });

  it('writes a branch absorbed by another as the shorter condition, never as an empty branch', () => {
    const chart = read('sequence S\nstep 0 initial\nstep 1\n0 -> 1 : A + A.B\n1 -> 0 : /A\n');

    const sequence = grafcetToLad(chart).find((document) => document.name === 'S')!;

    assert.ok(!sequence.text.includes('wire#w2'));
  });

  it('refuses a negated edge with a reason about edges', () => {
    const chart = read('sequence S\nstep 0 initial\nstep 1\n0 -> 1 : /↑A\n1 -> 0 : A\n');

    assert.throws(() => grafcetToLad(chart), /negated edge/);
  });

  it('refuses two consecutive transitions waiting for the same edge, which one edge would cross together', () => {
    const chart = read('sequence S\nstep 0 initial\nstep 1\nstep 2\n0 -> 1 : ↑A\n1 -> 2 : ↑A\n2 -> 0 : /A\n');

    assert.throws(() => grafcetToLad(chart), /both wait for ↑A/);
  });

  it('refuses an edge that could only be written as copies in several branches', () => {
    const chart = read('sequence S\nstep 0 initial\nstep 1\n0 -> 1 : (↑A + B).(C + D)\n1 -> 0 : /A\n');

    assert.throws(() => grafcetToLad(chart), /copied into several parallel branches/);
  });
});
