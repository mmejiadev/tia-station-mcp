import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';

/** One thing the machine either has or has not. */
export type PreconditionCheck = {
  readonly name: string;
  readonly met: boolean;
  readonly required: boolean;
  readonly found: string;
  readonly fix: string;
};

/**
 * What the machine meets, or why that could not be established.
 *
 * @remarks
 * `available: false` is not the same as `ready: false`, and the difference is the whole reason this
 * type has both. "Your machine is missing TIA Portal" and "I could not find out what your machine
 * has" are different sentences, and a view that showed the second as the first would be telling
 * somebody their installation is broken when the truth is that nothing was checked.
 */
export type PreconditionReport = {
  readonly available: boolean;
  readonly ready: boolean;
  readonly checks: readonly PreconditionCheck[];
  readonly reason: string;
};

/** How long the script gets before it is assumed to be stuck. */
const Patience = 30_000;

/**
 * Asks the machine what it has, by running the same script the bootstrap runs.
 *
 * @param scriptPath Where `Test-Preconditions.ps1` is.
 * @returns What it found, or an unavailable report carrying the reason.
 * @remarks
 * **It runs the script rather than asking the same questions again.** A TypeScript copy of these
 * checks would be a second implementation of the answer to "can this machine run the server", and
 * the copy that drifts is always the one telling somebody they are ready when they are not. The
 * script exists because it has to work before Node does; this reads what it says.
 *
 * **Exit code 1 is an answer, not a failure.** The script exits 1 when a requirement is not met,
 * which is exactly the case this view is for. Only a failure to *run* it is reported as
 * unavailable.
 *
 * Nothing here is a write. The script installs nothing and grants nothing, which is what keeps the
 * dashboard API read-only in the sense that matters: it inspects, it does not act.
 */
export function checkPreconditions(scriptPath: string): PreconditionReport {
  if (!existsSync(scriptPath)) {
    return unavailable(`No precondition script at '${scriptPath}'.`);
  }

  if (process.platform !== 'win32') {
    return unavailable(
      'Preconditions can only be checked on Windows, which is the only platform this server runs on.'
    );
  }

  let output: string;

  try {
    output = execFileSync(
      'powershell.exe',
      ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath, '-Json'],
      { encoding: 'utf8', timeout: Patience, windowsHide: true }
    );
  } catch (failure) {
    const asProcess = failure as { status?: number; stdout?: string; message?: string };

    // Exit 1 with output is the script saying a requirement is not met, which is an answer.
    if (asProcess.status === 1 && typeof asProcess.stdout === 'string' && asProcess.stdout.length > 0) {
      output = asProcess.stdout;
    } else {
      return unavailable(`The precondition script could not be run: ${asProcess.message ?? String(failure)}`);
    }
  }

  return readPreconditionReport(output, scriptPath);
}

/**
 * Reads what the script said, refusing anything that is not the shape it promises.
 *
 * @param output The script's JSON.
 * @param source What produced it, for the message when it is not readable.
 * @returns The report, or an unavailable one carrying the reason.
 * @remarks
 * Separate from running the script, and exported, because these are two different things: this is
 * the part with the decisions in it, and it can be checked on any machine. Starting a PowerShell
 * process can only be checked on Windows, and a rule that can only be checked on one machine is a
 * rule that stops being checked.
 */
export function readPreconditionReport(output: string, source: string): PreconditionReport {
  let parsed: unknown;

  try {
    parsed = JSON.parse(output);
  } catch {
    return unavailable(`'${source}' answered something that is not JSON.`);
  }

  if (parsed === null || typeof parsed !== 'object') {
    return unavailable(`'${source}' answered something that is not a report.`);
  }

  const body = parsed as { Ready?: unknown; Checks?: unknown };

  if (typeof body.Ready !== 'boolean' || !Array.isArray(body.Checks)) {
    return unavailable(`'${source}' answered a report with no verdict or no checks in it.`);
  }

  const checks = body.Checks.map(readCheck).filter((check): check is PreconditionCheck => check !== undefined);

  if (checks.length !== body.Checks.length) {
    return unavailable(`'${source}' answered a check this reader does not understand.`);
  }

  return { available: true, ready: body.Ready, checks, reason: '' };
}

function readCheck(entry: unknown): PreconditionCheck | undefined {
  if (entry === null || typeof entry !== 'object') {
    return undefined;
  }

  const check = entry as Record<string, unknown>;

  if (typeof check['Name'] !== 'string' || typeof check['Met'] !== 'boolean' || typeof check['Required'] !== 'boolean') {
    return undefined;
  }

  return {
    name: check['Name'],
    met: check['Met'],
    required: check['Required'],
    found: typeof check['Found'] === 'string' ? check['Found'] : '',
    fix: typeof check['Fix'] === 'string' ? check['Fix'] : ''
  };
}

function unavailable(reason: string): PreconditionReport {
  return { available: false, ready: false, checks: [], reason };
}
