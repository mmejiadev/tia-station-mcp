import { and, eq, gt, isNull, or } from 'drizzle-orm';
import type { PlatformDatabase } from '../db/connection.ts';
import { station } from '../db/schema.ts';
import { requireRole } from './projectAccess.ts';
import { hashPairingCode } from './stationPairing.ts';
import { done, refused, type WorkspaceResult } from './workspaceResult.ts';

// One answer for every way it can fail, so that it reveals nothing about which stations exist.
const NotLinked = 'The station name or its pairing code is wrong, or the code has expired. Issue a new code on the station with npm run pair.';

/**
 * Links a station to an organisation with the pairing code its machine issued.
 *
 * @param database Where stations and memberships are kept.
 * @param request Which admin, which organisation, which station, and its pairing code.
 * @param now The current moment, which the code must not have outlived.
 * @returns The station's id, or why not.
 * @remarks
 * **The code, not the admin role, is what proves the station is theirs**: anybody can create an
 * organisation and be its admin. A code is used once, and is cleared by the link it allows.
 *
 * **One conditional update decides it**, with every condition in its WHERE — the name, the code,
 * its expiry, and that the station is unlinked or already this organisation's. Two admins linking at
 * the same moment cannot both win, and a station linked elsewhere is never taken.
 *
 * Every failure gives the same answer: a different one for "no such station" would let anybody
 * with an organisation list the names of other people's machines.
 */
export async function linkStation(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly organizationId: string; readonly stationName: string; readonly pairingCode: string },
  now: Date = new Date()
): Promise<WorkspaceResult<number>> {
  const allowed = await requireRole(database, { ...request, permission: { station: ['link'] } });

  if (allowed.kind === 'refused') {
    return allowed;
  }

  if (request.pairingCode.trim().length === 0) {
    return refused('invalid', NotLinked);
  }

  const linked = await database
    .update(station)
    .set({ organizationId: request.organizationId, pairingCodeHash: null, pairingCodeExpiresAt: null })
    .where(
      and(
        eq(station.name, request.stationName.trim()),
        eq(station.pairingCodeHash, hashPairingCode(request.pairingCode)),
        gt(station.pairingCodeExpiresAt, now),
        or(isNull(station.organizationId), eq(station.organizationId, request.organizationId))
      )
    )
    .returning({ id: station.id });

  const id = linked[0]?.id;

  return id === undefined ? refused('invalid', NotLinked) : done(id);
}
