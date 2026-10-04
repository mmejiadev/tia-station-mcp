import { sql } from 'drizzle-orm';
import { win32 } from 'node:path';
import type { PlatformDatabase } from '../db/connection.ts';
import { project } from '../db/schema.ts';
import { readJournal, textOf } from './journalLines.ts';
import { ensureStation, parseMoment } from './stationRows.ts';

/** One line of `projects.jsonl`, as `ProjectRecord` in the C# server writes it. */
type ProjectRecord = {
  readonly timestamp: Date;
  readonly path: string;
  readonly name: string;
  readonly author: string;
  readonly createdAt: Date | null;
  readonly lastModified: Date | null;
  readonly lastModifiedBy: string;
};

/** What one import of a project journal did. */
export type ProjectImportResult =
  | { readonly kind: 'imported'; readonly read: number }
  | { readonly kind: 'refused'; readonly line: number; readonly reason: string };

/**
 * Imports a station's project journal: who made each project and when it was last changed.
 *
 * @param database Where to import it.
 * @param request The station and the journal's lines.
 * @returns How many records were read, or the line that made the journal unimportable.
 * @remarks
 * Each record updates its project only when it is newer than what the row already holds, so the
 * latest record wins however many times, and in whatever order, the journal is imported. That is
 * what makes this idempotent: there are no rows to skip, only values to keep or replace.
 */
export async function importProjects(
  database: PlatformDatabase,
  request: { readonly stationName: string; readonly lines: readonly string[] }
): Promise<ProjectImportResult> {
  const read = readJournal(request.lines, toRecord, 'project');

  if (read.kind === 'refused') {
    return read;
  }

  await database.transaction(async (transaction) => {
    const stationId = await ensureStation(transaction, request.stationName.trim());

    for (const { record } of read.lines) {
      const described = {
        name: record.name,
        tiaAuthor: record.author,
        tiaCreatedAt: record.createdAt,
        tiaModifiedAt: record.lastModified,
        tiaModifiedBy: record.lastModifiedBy,
        lastSeenAt: record.timestamp
      };

      await transaction
        .insert(project)
        .values({ stationId, tiaPath: record.path, ...described })
        .onConflictDoUpdate({
          target: [project.stationId, project.tiaPath],
          set: described,
          setWhere: sql`${project.lastSeenAt} is null or ${project.lastSeenAt} <= excluded.last_seen_at`
        });
    }
  });

  return { kind: 'imported', read: read.lines.length };
}

/** One parsed line as a project record, or undefined when it has no path or no moment. */
function toRecord(raw: Record<string, unknown>): ProjectRecord | undefined {
  const path = textOf(raw, 'path');
  const timestamp = parseMoment(textOf(raw, 'timestamp'));

  if (path.length === 0 || timestamp === null) {
    return undefined;
  }

  const name = textOf(raw, 'name');

  return {
    timestamp,
    path,
    name: name.length > 0 ? name : win32.parse(path).name,
    author: textOf(raw, 'author'),
    createdAt: parseMoment(textOf(raw, 'createdAt')),
    lastModified: parseMoment(textOf(raw, 'lastModified')),
    lastModifiedBy: textOf(raw, 'lastModifiedBy')
  };
}
