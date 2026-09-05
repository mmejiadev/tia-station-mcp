import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { checkPreconditions, readPreconditionReport } from '../src/preconditions.ts';

/**
 * Reading what the precondition script said.
 *
 * @remarks
 * The reading is separated from the running so that it can be checked anywhere. Starting a
 * PowerShell process only works on Windows, and the decisions are all on this side.
 */
describe('reading a precondition report', () => {
  it('reads a machine that meets everything', () => {
    const report = readPreconditionReport(
      JSON.stringify({ Ready: true, Checks: [check('TIA Portal V20', true, true)] }),
      'a script'
    );

    assert.equal(report.available, true);
    assert.equal(report.ready, true);
    assert.equal(report.checks[0]?.name, 'TIA Portal V20');
  });

  it('carries the fix for each thing the machine has not', () => {
    const report = readPreconditionReport(
      JSON.stringify({
        Ready: false,
        Checks: [{ ...check('Node', false, false), Fix: 'Install Node from nodejs.org.' }]
      }),
      'a script'
    );

    assert.equal(report.ready, false);
    assert.equal(report.checks[0]?.fix, 'Install Node from nodejs.org.');
  });

  it('keeps required and optional apart, because one blocks and the other warns', () => {
    const report = readPreconditionReport(
      JSON.stringify({
        Ready: false,
        Checks: [check('TIA Portal', false, true), check('PLCSIM', false, false)]
      }),
      'a script'
    );

    assert.equal(report.checks[0]?.required, true);
    assert.equal(report.checks[1]?.required, false);
  });

  /**
   * The distinction the whole type exists for: "your machine is missing something" and "I could not
   * find out what your machine has" are different sentences, and showing the second as the first
   * tells somebody their installation is broken when nothing was checked.
   */
  it('is unavailable rather than not-ready when the answer is not JSON', () => {
    const report = readPreconditionReport('powershell said something else entirely', 'a script');

    assert.equal(report.available, false);
    assert.equal(report.ready, false);
    assert.match(report.reason, /not JSON/);
  });

  it('is unavailable when the report has no verdict', () => {
    const report = readPreconditionReport(JSON.stringify({ Checks: [] }), 'a script');

    assert.equal(report.available, false);
    assert.match(report.reason, /no verdict/);
  });

  it('is unavailable when a check is not one', () => {
    const report = readPreconditionReport(JSON.stringify({ Ready: true, Checks: [{ Name: 'TIA' }] }), 'a script');

    assert.equal(report.available, false);
    assert.match(report.reason, /does not understand/);
  });

  it('refuses a report that is not an object at all', () => {
    assert.equal(readPreconditionReport('[]', 'a script').available, false);
    assert.equal(readPreconditionReport('null', 'a script').available, false);
  });

  it('says where it could not find a script instead of reporting a broken machine', () => {
    const report = checkPreconditions('no-such-script.ps1');

    assert.equal(report.available, false);
    assert.match(report.reason, /no-such-script/i);
  });
});

/**
 * The adapter half: PowerShell exists, the script runs, and what comes back parses.
 *
 * @remarks
 * Windows only, and skipped elsewhere with the reason printed rather than failed. The whole point of
 * this script is that it runs before anything else is installed, on the one platform this server
 * supports; a hosted Linux runner cannot say anything about it either way.
 */
describe('running the precondition script', { skip: process.platform === 'win32' ? undefined : 'Windows only' }, () => {
  const script = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'scripts', 'Test-Preconditions.ps1');

  it('runs the real script and reads five checks back', () => {
    const report = checkPreconditions(script);

    assert.equal(report.available, true, report.reason);
    assert.equal(report.checks.length, 5);
  });

  /**
   * The script exits 1 when a requirement is not met. That is its answer, not a failure to run, and
   * treating a non-zero exit as an error would report every unready machine as unknowable.
   */
  it('treats a not-ready verdict as an answer rather than as a broken script', () => {
    const report = checkPreconditions(script);

    assert.equal(report.available, true, report.reason);
    assert.equal(
      report.ready,
      report.checks.filter((one) => one.required && !one.met).length === 0
    );
  });
});

function check(name: string, met: boolean, required: boolean): Record<string, unknown> {
  return { Name: name, Met: met, Required: required, Found: '', Fix: '' };
}
