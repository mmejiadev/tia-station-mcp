import { createHash } from 'node:crypto';
import { verifyAuditChain } from '../../../harness/src/auditChain.ts';
import { parseEntry } from '../../../harness/src/auditTrail.ts';
import type { PlatformDatabase } from '../db/connection.ts';
import { change, station } from '../db/schema.ts';

/** What one import of an audit trail did. */
export type AuditImportResult =
  | {
      readonly kind: 'imported';
      /** Entries in the trail. */
      readonly read: number;
      /** Entries that were not in the database yet. */
      readonly inserted: number;
      /** Entries a previous import had already brought in. */
      readonly alreadyPresent: number;
      /** Entries from before chaining, imported but not attested by the chain. */
      readonly unchained: number;
    }
  | {
      readonly kind: 'refused';
      /** The line that made the trail unimportable, counting from one. */
      readonly line: number;
      readonly reason: string;
    };

/** What to import, and from where. */
export type AuditImportRequest = {
  /** The machine the trail was written on. Created on first import. */
  readonly stationName: string;
  /** The trail, one element per line, blank lines included. */
  readonly lines: readonly string[];
};

type ChangeRow = typeof change.$inferInsert;

// Rows per INSERT. PostgreSQL takes at most 65535 parameters in one statement and a row is twenty.
const RowsPerInsert = 1000;

/**
 * Imports an audit trail into the database.
 *
 * @param database Where to import it.
 * @param request The station and the trail's lines.
 * @returns How many entries were new, or the line that made the trail unimportable.
 * @remarks
 * **All or nothing.** A trail whose chain is broken, or with a line that is not an entry, is refused
 * whole and nothing of it is written. Importing the part before the break would present an edited
 * history as a shorter, clean one — the one thing an index of an audit trail may never do.
 *
 * Refusals are results rather than exceptions: a tampered or corrupt trail is the importer working,
 * and the caller has to report it, not retry it. An exception means the database failed.
 *
 * Idempotent: an entry already imported is skipped by its key, so the same trail can be imported
 * on a timer, and a trail that has grown brings in only its new lines.
 */
export async function importAuditTrail(
  database: PlatformDatabase,
  request: AuditImportRequest
): Promise<AuditImportResult> {
  if (request.stationName.trim().length === 0) {
    throw new Error('A station name is required: it says which machine the trail was written on.');
  }

  const chain = verifyAuditChain(request.lines);

  if (!chain.intact) {
    return { kind: 'refused', line: chain.brokenAtLine, reason: chain.reason };
  }

  const read = readRows(request.lines);

  if (read.kind === 'refused') {
    return read;
  }

  const inserted = await insertRows(database, request.stationName.trim(), read.rows);

  return {
    kind: 'imported',
    read: read.rows.length,
    inserted,
    alreadyPresent: read.rows.length - inserted,
    unchained: chain.unchained
  };
}

type ReadResult =
  | { readonly kind: 'rows'; readonly rows: readonly Omit<ChangeRow, 'stationId'>[] }
  | { readonly kind: 'refused'; readonly line: number; readonly reason: string };

function readRows(lines: readonly string[]): ReadResult {
  const rows: Omit<ChangeRow, 'stationId'>[] = [];

  for (let index = 0; index < lines.length; index++) {
    const line = lines[index] ?? '';

    if (line.trim().length === 0) {
      continue;
    }

    const row = toRow(line, index + 1);

    if (row === undefined) {
      return {
        kind: 'refused',
        line: index + 1,
        reason: 'this line is not an audit entry - it is not JSON, or it lacks the plan, mode, tool or outcome'
      };
    }

    rows.push(row);
  }

  return { kind: 'rows', rows };
}

/**
 * One line as a row, or nothing when it is not an entry.
 *
 * @remarks
 * The entry is judged by the harness's own parser, so a line the workshop gate counts as unreadable
 * is unreadable here too. The fields that parser does not carry — the chain's and the two values
 * only some entries have — are read from the same JSON.
 */
function toRow(line: string, lineNumber: number): Omit<ChangeRow, 'stationId'> | undefined {
  const entry = parseEntry(line);

  if (entry === undefined) {
    return undefined;
  }

  const raw = JSON.parse(line) as Record<string, unknown>;
  const chainHash = textOf(raw, 'hash');
  const sequence = Number.parseInt(textOf(raw, 'seq'), 10);

  return {
    ...entry,
    entryKey: chainHash.length > 0 ? chainHash : createHash('sha256').update(line.trim(), 'utf8').digest('hex'),
    lineNumber,
    sequence: Number.isNaN(sequence) ? null : sequence,
    chainHash: chainHash.length > 0 ? chainHash : null,
    occurredAt: parseMoment(entry.timestamp),
    value: textOf(raw, 'value'),
    documentation: textOf(raw, 'documentation')
  };
}

async function insertRows(
  database: PlatformDatabase,
  stationName: string,
  rows: readonly Omit<ChangeRow, 'stationId'>[]
): Promise<number> {
  return database.transaction(async (transaction) => {
    const stationId = await ensureStation(transaction, stationName);
    let inserted = 0;

    for (let start = 0; start < rows.length; start += RowsPerInsert) {
      const batch = rows.slice(start, start + RowsPerInsert).map((row) => ({ ...row, stationId }));
      const written = await transaction.insert(change).values(batch).onConflictDoNothing().returning({ id: change.id });

      inserted += written.length;
    }

    return inserted;
  });
}

type Transaction = Parameters<Parameters<PlatformDatabase['transaction']>[0]>[0];

async function ensureStation(transaction: Transaction, name: string): Promise<number> {
  await transaction.insert(station).values({ name }).onConflictDoNothing();

  const found = await transaction.query.station.findFirst({ where: (table, { eq }) => eq(table.name, name) });

  if (found === undefined) {
    throw new Error(`The station '${name}' was neither found nor created.`);
  }

  return found.id;
}

/** The moment a timestamp names, or null when it names none. */
function parseMoment(timestamp: string): Date | null {
  const moment = new Date(timestamp);

  return Number.isNaN(moment.getTime()) ? null : moment;
}

function textOf(raw: Record<string, unknown>, field: string): string {
  return typeof raw[field] === 'string' ? (raw[field] as string) : '';
}
