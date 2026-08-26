import { existsSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { readAuditTrail } from './auditTrail.ts';
import { evaluateGate, type GateVerdict } from './gate.ts';
import { repositoryRoot } from './serverLocation.ts';
import { Telemetry } from './telemetry.ts';
import { readReviewRecord } from './workshopReview.ts';

/** Where the evidence is. */
type Options = {
  readonly databasePath: string;
  readonly auditPath: string;
  readonly reviewPath: string;
};

/**
 * Answers, from the recorded data alone, whether Workshop Mode may be enabled.
 *
 * @returns 0 when all five criteria are met, 1 when any is not.
 * @remarks
 * A separate entry point from `run.ts` on purpose: this reads and judges, and touches neither TIA
 * Portal nor a controller. It can be run on any machine, by anyone, including somebody who wants to
 * check the claim rather than take it — which is the only reason a gate like this is worth having.
 *
 * The exit code carries the verdict so a script can ask without parsing the report.
 */
function main(): number {
  const options = parseOptions(process.argv.slice(2));
  const telemetry = Telemetry.open(options.databasePath);

  try {
    const verdict = evaluateGate({
      runs: telemetry.runStatistics(),
      unfinishedIterations: telemetry.countUnfinishedIterations(),
      audit: readAuditTrail(options.auditPath),
      backupExists: existsSync,
      review: readReviewRecord(options.reviewPath)
    });

    report(verdict);

    return verdict.open ? 0 : 1;
  } finally {
    telemetry.close();
  }
}

/** Prints every criterion, met or not, and the verdict. */
function report(verdict: GateVerdict): void {
  console.log('Workshop gate');
  console.log('');

  for (const criterion of verdict.criteria) {
    console.log(`${criterion.met ? 'MET    ' : 'NOT MET'}  ${criterion.number}. ${criterion.name}`);
    console.log(`         ${criterion.evidence}`);
  }

  console.log('');
  console.log(
    verdict.open
      ? 'All five criteria are met. The decision to enable Workshop Mode is now a human one.'
      : 'The gate is shut. Workshop Mode stays unreachable in the default build.'
  );
}

function parseOptions(args: readonly string[]): Options {
  const values = new Map<string, string>();

  for (let index = 0; index < args.length; index += 2) {
    const flag = args[index];
    const value = args[index + 1];

    if (flag === undefined || value === undefined || !flag.startsWith('--')) {
      throw new Error(
        `Bad arguments near '${flag ?? ''}'. Usage: node src/gateReport.ts [--database <file>] ` +
          '[--audit <file>] [--review <file>]. Every flag takes a value.'
      );
    }

    values.set(flag, value);
  }

  // The same defaults a run writes to, so asking the question needs no arguments on the machine
  // that produced the evidence.
  const harnessRoot = join(repositoryRoot(), '.tia-mcp', 'harness');

  return {
    databasePath: resolve(values.get('--database') ?? join(harnessRoot, 'metrics.db')),
    auditPath: resolve(values.get('--audit') ?? join(harnessRoot, 'audit.jsonl')),
    reviewPath: resolve(values.get('--review') ?? join(repositoryRoot(), 'docs', 'workshop-review.md'))
  };
}

process.exitCode = main();
