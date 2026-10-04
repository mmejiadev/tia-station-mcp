import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { openDatabase, type PlatformDatabase } from '../db/connection.ts';
import { importAuditTrail } from './auditImport.ts';
import { importCompilations } from './compilationImport.ts';
import { importProjects } from './projectImport.ts';

/**
 * `npm run import -- --station <name> --directory <the server's .tia-mcp directory>`
 *
 * Imports everything one station's server recorded: the audit trail, the projects it opened and its
 * compilations, in that order, so a compilation finds its project already described. A file that
 * does not exist is skipped and said so — a server that never compiled has no compilation journal.
 * Exits with 1 when any file is refused, so a scheduled import that meets a tampered or corrupt
 * file fails visibly.
 */
type Outcome = { readonly kind: 'imported' | 'refused'; readonly line?: number; readonly reason?: string };

type Step = {
  readonly file: string;
  readonly run: (database: PlatformDatabase, request: { stationName: string; lines: string[] }) => Promise<Outcome>;
  readonly describe: (outcome: Outcome) => string;
};

const Steps: readonly Step[] = [
  { file: 'audit.jsonl', run: importAuditTrail, describe: (outcome) => `${countOf(outcome, 'inserted')} new change(s)` },
  { file: 'projects.jsonl', run: importProjects, describe: (outcome) => `${countOf(outcome, 'read')} project record(s) read` },
  { file: 'compilations.jsonl', run: importCompilations, describe: (outcome) => `${countOf(outcome, 'inserted')} new compilation(s)` }
];

const { values } = parseArgs({ options: { station: { type: 'string' }, directory: { type: 'string' } } });

if (values.station === undefined || values.directory === undefined) {
  console.error('Usage: npm run import -- --station <name> --directory <path to the .tia-mcp directory>');
  process.exit(2);
}

const connection = openDatabase(process.env['DATABASE_URL']);

try {
  for (const step of Steps) {
    await runStep(connection.database, values.station, join(values.directory, step.file), step);
  }
} finally {
  await connection.close();
}

async function runStep(database: PlatformDatabase, stationName: string, path: string, step: Step): Promise<void> {
  if (!existsSync(path)) {
    console.log(`${step.file}: not there, skipped.`);
    return;
  }

  const lines = readFileSync(path, 'utf8').split('\n');
  const outcome = await step.run(database, { stationName, lines });

  if (outcome.kind === 'refused') {
    console.error(`${step.file}: refused, nothing imported: line ${outcome.line}: ${outcome.reason}`);
    process.exitCode = 1;
    return;
  }

  console.log(`${step.file}: ${step.describe(outcome)}.`);
}

function countOf(outcome: Outcome, field: string): number {
  return (outcome as Record<string, unknown>)[field] as number;
}
