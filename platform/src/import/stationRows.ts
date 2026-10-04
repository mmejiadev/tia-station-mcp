import { win32 } from 'node:path';
import type { PlatformTransaction } from '../db/connection.ts';
import { project, station } from '../db/schema.ts';

/**
 * The id of a station, created on first sight.
 *
 * @param transaction Where to look and create.
 * @param name The station's name.
 * @returns Its id.
 */
export async function ensureStation(transaction: PlatformTransaction, name: string): Promise<number> {
  await transaction.insert(station).values({ name }).onConflictDoNothing();

  const found = await transaction.query.station.findFirst({ where: (table, { eq }) => eq(table.name, name) });

  if (found === undefined) {
    throw new Error(`The station '${name}' was neither found nor created.`);
  }

  return found.id;
}

/**
 * The id of a project on a station, created with only its path and name when it is new.
 *
 * @param transaction Where to look and create.
 * @param stationId The station it is on.
 * @param tiaPath The project file, as TIA Portal reports it.
 * @returns Its id.
 * @remarks
 * For a project known only through a compilation — opened before the server recorded projects. Its
 * name is the file's, which is what TIA Portal names a project after; what TIA Portal says about it
 * arrives with the first project record and does not overwrite anything here, because there is
 * nothing here but the path.
 */
export async function ensureProject(transaction: PlatformTransaction, stationId: number, tiaPath: string): Promise<number> {
  await transaction
    .insert(project)
    .values({ stationId, tiaPath, name: win32.parse(tiaPath).name })
    .onConflictDoNothing();

  const found = await transaction.query.project.findFirst({
    where: (table, { and, eq }) => and(eq(table.stationId, stationId), eq(table.tiaPath, tiaPath))
  });

  if (found === undefined) {
    throw new Error(`The project '${tiaPath}' was neither found nor created.`);
  }

  return found.id;
}

/**
 * The moment a timestamp names, or null when it names none.
 *
 * @param timestamp Text as the server wrote it.
 * @returns The moment, or null.
 */
export function parseMoment(timestamp: string): Date | null {
  const moment = new Date(timestamp);

  return Number.isNaN(moment.getTime()) ? null : moment;
}
