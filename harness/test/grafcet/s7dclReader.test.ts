import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatCondition } from '../../src/grafcet/conditionText.ts';
import { readS7dcl, type LadOperation } from '../../src/grafcet/s7dclReader.ts';
import { FaultyFeeder, fc, Init, Outputs, Transfer } from './ladFixtures.ts';

function describeOperation(operation: LadOperation): string {
  const target = 'operand' in operation ? operation.operand : 'instance' in operation ? operation.instance : operation.target;

  return `${operation.kind} ${target} if ${formatCondition(operation.condition)}`;
}

describe('SIMATIC SD LAD reader', () => {
  it('reads contacts in series as AND and a junction as shared by the rungs hanging off it', () => {
    const block = readS7dcl(Transfer);

    assert.equal(block.name, 'TRANSFER');
    assert.deepEqual(block.operations.filter((operation) => operation.network === 1).map(describeOperation), ['set X31 if X30.START', 'reset X30 if X30.START']);
  });

  it('reads a branch that ends on a junction as OR, even when the coil is written first', () => {
    // TIA writes the coil in the first rung and the parallel contact in a later one, so the
    // junction can only be resolved after the whole network has been read.
    const block = readS7dcl(Outputs);

    assert.deepEqual(block.operations.map(describeOperation), ['assign MOTOR if X1 + X3']);
  });

  it('carries a TON input through the box, since Q cannot be true without it', () => {
    const block = readS7dcl(Transfer);

    const set = block.operations.find((operation) => operation.network === 2 && operation.kind === 'set');

    assert.equal(set === undefined ? '' : describeOperation(set), 'set X32 if X31.IEC_Timer_0_DB.Q');
  });

  it('reads the timer itself with its preset in seconds', () => {
    const timer = readS7dcl(Transfer).operations.find((operation) => operation.kind === 'timer');

    assert.equal(timer?.kind === 'timer' ? timer.presetSeconds : null, 30);
  });

  it('reads a bit range reset with its count', () => {
    const range = readS7dcl(Init).operations.find((operation) => operation.kind === 'resetRange');

    assert.equal(range === undefined ? '' : describeOperation(range), 'resetRange X1 if FirstScan');
    assert.equal(range?.kind === 'resetRange' ? range.count : 0, 3);
  });

  it('reports an unquoted operand as an error, because TIA reads it as an address', () => {
    const block = readS7dcl(FaultyFeeder);

    assert.deepEqual(block.findings.map((finding) => [finding.severity, finding.network]), [['error', 1]]);
    assert.match(block.findings[0]!.message, /operand I is not a tag/);
  });

  it('reports an element it does not know instead of guessing', () => {
    const document = fc('ODD', ['        RUNG wire#powerrail\n            Contact( "A" )\n            "Counter_DB".CTU(\n                pv := 5\n            )\n        END_RUNG\n']);

    const block = readS7dcl(document);

    assert.equal(block.findings.length, 1);
    assert.match(block.findings[0]!.message, /unsupported LAD element/);
  });

  it('reads a block call', () => {
    const document = `ORGANIZATION_BLOCK "Main"\n    NETWORK\n        RUNG wire#powerrail\n            "FEEDER"()\n        END_RUNG\n    END_NETWORK\nEND_ORGANIZATION_BLOCK\n`;

    const block = readS7dcl(document);

    assert.deepEqual(block.operations.map(describeOperation), ['call FEEDER if 1']);
  });

  it('ignores the byte order mark and Windows line endings TIA writes', () => {
    const document = String.fromCharCode(0xfeff) + Outputs.replace(/\n/g, '\r\n');

    assert.deepEqual(readS7dcl(document).operations.map(describeOperation), ['assign MOTOR if X1 + X3']);
  });
});
