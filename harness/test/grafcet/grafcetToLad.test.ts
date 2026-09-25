import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatCondition } from '../../src/grafcet/conditionText.ts';
import { grafcetToLad } from '../../src/grafcet/grafcetToLad.ts';
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

  it('refuses edges rather than guess their syntax', () => {
    const edge = read('sequence S\nstep 0 initial\n0 -> 1 : ↑A\n1 -> 0 : /A\n');

    assert.throws(() => grafcetToLad(edge), /edges/);
  });
});
