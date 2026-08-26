import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { isKnownOutcome, readAuditTrail } from '../src/auditTrail.ts';

/**
 * Reading the audit trail is what two of the five workshop criteria are answered from, so the ways
 * it can be wrong are the ways the gate can be wrong.
 */
describe('audit trail', () => {
  const directory = mkdtempSync(join(tmpdir(), 'tia-audit-'));

  after(() => {
    rmSync(directory, { recursive: true, force: true });
  });

  it('reads every entry and ignores blank lines', () => {
    const path = write('good.jsonl', [line({ planId: 'AAA-111' }), '', line({ planId: 'BBB-222' }), '']);

    const result = readAuditTrail(path);

    assert.equal(result.entries.length, 2);
    assert.equal(result.unreadableLines.length, 0);
  });

  it('reports an unreadable line instead of skipping it', () => {
    // Skipping would let a corrupt trail look complete, which is the one thing an audit may not do.
    const path = write('broken.jsonl', [line({}), 'this is not JSON', line({})]);

    const result = readAuditTrail(path);

    assert.equal(result.entries.length, 2);
    assert.deepEqual(result.unreadableLines, [2]);
  });

  it('counts an entry with no outcome as unreadable rather than as an entry', () => {
    // An entry the gate cannot judge must not pass through it as one with a blank in it.
    const path = write('no-outcome.jsonl', ['{"planId":"AAA-111","mode":"Study","tool":"WriteScl"}']);

    const result = readAuditTrail(path);

    assert.equal(result.entries.length, 0);
    assert.deepEqual(result.unreadableLines, [1]);
  });

  it('throws when there is no trail at all', () => {
    // "There is no file" means nobody knows what was written, which is not the same as "nothing
    // was written" and must not be reported as it.
    assert.throws(() => readAuditTrail(join(directory, 'absent.jsonl')));
  });

  it('recognises exactly the four outcomes the server writes', () => {
    assert.equal(isKnownOutcome('Applied'), true);
    assert.equal(isKnownOutcome('Refused'), true);
    assert.equal(isKnownOutcome('Probably fine'), false);
  });

  function write(name: string, lines: readonly string[]): string {
    const path = join(directory, name);

    writeFileSync(path, lines.join('\n'), 'utf8');

    return path;
  }
});

function line(overrides: Record<string, string>): string {
  return JSON.stringify({
    timestamp: '2026-08-26T19:00:00+02:00',
    planId: 'AAA-111',
    mode: 'Study',
    tool: 'WriteScl',
    target: 'PLC_0',
    backupPath: '',
    origin: 'agent',
    outcome: 'Applied',
    detail: '',
    ...overrides
  });
}
