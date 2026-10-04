import type { PlatformDatabase, PlatformTransaction } from '../db/connection.ts';
import { compilation, compilationMessage } from '../db/schema.ts';
import { readJournal, textOf, wholeNumberOf, type JournalLine } from './journalLines.ts';
import { ensureProject, ensureStation, parseMoment } from './stationRows.ts';

/** One message of a compilation record. */
type MessageRecord = {
  readonly severity: string;
  readonly path: string;
  readonly description: string;
};

/** One line of `compilations.jsonl`, as `CompilationRecord` in the C# server writes it. */
type CompilationRecord = {
  readonly timestamp: string;
  readonly projectPath: string;
  readonly softwarePath: string;
  readonly severity: string;
  readonly errorCount: number;
  readonly warningCount: number;
  readonly messages: readonly MessageRecord[];
};

/** What one import of a compilation journal did. */
export type CompilationImportResult =
  | { readonly kind: 'imported'; readonly read: number; readonly inserted: number }
  | { readonly kind: 'refused'; readonly line: number; readonly reason: string };

/**
 * Imports a station's compilation journal.
 *
 * @param database Where to import it.
 * @param request The station and the journal's lines.
 * @returns How many compilations were new, or the line that made the journal unimportable.
 * @remarks
 * All or nothing, and idempotent, for the reasons the audit import gives. A compilation of a
 * project the database has not seen creates the project with its path, so no compilation is lost
 * for having been made before the server recorded projects.
 */
export async function importCompilations(
  database: PlatformDatabase,
  request: { readonly stationName: string; readonly lines: readonly string[] }
): Promise<CompilationImportResult> {
  const read = readJournal(request.lines, toRecord, 'compilation');

  if (read.kind === 'refused') {
    return read;
  }

  const inserted = await database.transaction(async (transaction) => {
    const stationId = await ensureStation(transaction, request.stationName.trim());
    let count = 0;

    for (const line of read.lines) {
      count += await insertCompilation(transaction, stationId, line);
    }

    return count;
  });

  return { kind: 'imported', read: read.lines.length, inserted };
}

/** Inserts one compilation and its messages; returns 1 when it was new, 0 when it was there. */
async function insertCompilation(
  transaction: PlatformTransaction,
  stationId: number,
  line: JournalLine<CompilationRecord>
): Promise<number> {
  const { record } = line;
  const projectId = await ensureProject(transaction, stationId, record.projectPath);

  const written = await transaction
    .insert(compilation)
    .values({
      stationId,
      projectId,
      entryKey: line.entryKey,
      lineNumber: line.lineNumber,
      compiledAt: parseMoment(record.timestamp),
      softwarePath: record.softwarePath,
      severity: record.severity,
      errorCount: record.errorCount,
      warningCount: record.warningCount
    })
    .onConflictDoNothing()
    .returning({ id: compilation.id });

  const compilationId = written[0]?.id;

  if (compilationId === undefined) {
    return 0;
  }

  if (record.messages.length > 0) {
    await transaction
      .insert(compilationMessage)
      .values(record.messages.map((message, position) => ({ compilationId, position, ...message })));
  }

  return 1;
}

/**
 * One parsed line as a compilation, or undefined when it is not one.
 *
 * @remarks
 * The paths, the severity and both counts are required: a compilation without them cannot be shown
 * as one, and showing it with blanks would show a compilation that did not happen.
 */
function toRecord(raw: Record<string, unknown>): CompilationRecord | undefined {
  const errorCount = wholeNumberOf(raw, 'errorCount');
  const warningCount = wholeNumberOf(raw, 'warningCount');
  const messages = toMessages(raw['messages']);

  if (errorCount === undefined || warningCount === undefined || messages === undefined) {
    return undefined;
  }

  const required = ['projectPath', 'softwarePath', 'severity'].map((field) => textOf(raw, field));

  if (required.some((value) => value.length === 0)) {
    return undefined;
  }

  return {
    timestamp: textOf(raw, 'timestamp'),
    projectPath: textOf(raw, 'projectPath'),
    softwarePath: textOf(raw, 'softwarePath'),
    severity: textOf(raw, 'severity'),
    errorCount,
    warningCount,
    messages
  };
}

function toMessages(value: unknown): MessageRecord[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const messages: MessageRecord[] = [];

  for (const item of value) {
    if (typeof item !== 'object' || item === null) {
      return undefined;
    }

    const raw = item as Record<string, unknown>;

    messages.push({ severity: textOf(raw, 'severity'), path: textOf(raw, 'path'), description: textOf(raw, 'description') });
  }

  return messages;
}
