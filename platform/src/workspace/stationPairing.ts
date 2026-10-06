import { createHash, randomInt } from 'node:crypto';
import { eq } from 'drizzle-orm';
import type { PlatformDatabase } from '../db/connection.ts';
import { station } from '../db/schema.ts';
import { done, refused, type WorkspaceResult } from './workspaceResult.ts';

/** A pairing code as the station's operator sees it, and until when it works. */
export type PairingCode = {
  readonly code: string;
  readonly expiresAt: Date;
};

// No 0/O, 1/I/L: a code read off one screen and typed into another must survive the trip.
const Alphabet = '23456789ABCDEFGHJKMNPQRSTUVWXYZ';
const CodeLength = 8;

// Long enough to walk to the other computer, short enough that a code left on a screen goes stale.
const Lifetime = 30 * 60 * 1000;

/**
 * Issues a pairing code for a station, replacing any earlier one.
 *
 * @param database Where stations are kept.
 * @param stationName The station, as its history was imported.
 * @param now The current moment.
 * @returns The code — shown once, stored only as a hash — or why not.
 * @remarks
 * Run on the station's own machine (`npm run pair`): printing the code there is the proof that
 * whoever types it into the web has that machine in front of them.
 */
export async function issuePairingCode(
  database: PlatformDatabase,
  stationName: string,
  now: Date = new Date()
): Promise<WorkspaceResult<PairingCode>> {
  const code = Array.from({ length: CodeLength }, () => Alphabet[randomInt(Alphabet.length)]).join('');
  const expiresAt = new Date(now.getTime() + Lifetime);

  const updated = await database
    .update(station)
    .set({ pairingCodeHash: hashPairingCode(code), pairingCodeExpiresAt: expiresAt })
    .where(eq(station.name, stationName.trim()))
    .returning({ id: station.id });

  if (updated.length === 0) {
    return refused('not-found', `No station is called '${stationName}'. Import its history first.`);
  }

  return done({ code: `${code.slice(0, 4)}-${code.slice(4)}`, expiresAt });
}

/**
 * The hash a pairing code is stored and compared as.
 *
 * @param code The code as typed: case, spaces and dashes do not matter.
 * @returns Its SHA-256, in hexadecimal.
 */
export function hashPairingCode(code: string): string {
  return createHash('sha256').update(code.toUpperCase().replace(/[\s-]/g, ''), 'utf8').digest('hex');
}
