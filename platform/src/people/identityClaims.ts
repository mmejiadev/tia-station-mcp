import { and, eq } from 'drizzle-orm';
import { roleCan } from '../auth/permissions.ts';
import type { PlatformDatabase } from '../db/connection.ts';
import { station, tiaIdentity } from '../db/schema.ts';
import { roleOf } from './membership.ts';

/** What asking to claim or confirm an identity came to. */
export type IdentityResult =
  | { readonly kind: 'claimed' | 'confirmed'; readonly identityId: number }
  | { readonly kind: 'refused'; readonly reason: string };

/** A person's claim to be a TIA author on a station. */
export type IdentityClaim = {
  readonly userId: string;
  readonly organizationId: string;
  readonly stationName: string;
  readonly tiaAuthor: string;
};

// PostgreSQL's code for a unique violation: the one-confirmed-person index refusing a second.
const UniqueViolation = '23505';

/**
 * Records that a person says they are a TIA author on a station.
 *
 * @param database Where identities are kept.
 * @param claim Who, in which organisation, on which station, as which author.
 * @returns The claim, or why it was refused.
 * @remarks
 * Only a member of the organisation may claim, and a claim names nobody on any project until it is
 * confirmed. Claiming the same thing twice returns the existing claim rather than refusing: the
 * person wanted it recorded, and it is.
 */
export async function claimIdentity(database: PlatformDatabase, claim: IdentityClaim): Promise<IdentityResult> {
  const tiaAuthor = claim.tiaAuthor.trim();

  if (tiaAuthor.length === 0) {
    return refused('Name the TIA Portal author you are - the name TIA Portal records as a project\'s author.');
  }

  if ((await roleOf(database, claim.userId, claim.organizationId)) === undefined) {
    return refused('Only a member of the organisation can claim an identity in it.');
  }

  // Only a station of this organisation: a confirmation about another organisation's machine
  // would name nobody on any project this one can see, and would say who works where elsewhere.
  const [found] = await database
    .select({ id: station.id })
    .from(station)
    .where(and(eq(station.name, claim.stationName.trim()), eq(station.organizationId, claim.organizationId)));

  if (found === undefined) {
    return refused(`No station called '${claim.stationName}' is linked to this organisation.`);
  }

  await database
    .insert(tiaIdentity)
    .values({ organizationId: claim.organizationId, userId: claim.userId, stationId: found.id, tiaAuthor })
    .onConflictDoNothing();

  const [identity] = await database
    .select({ id: tiaIdentity.id })
    .from(tiaIdentity)
    .where(
      and(
        eq(tiaIdentity.organizationId, claim.organizationId),
        eq(tiaIdentity.userId, claim.userId),
        eq(tiaIdentity.stationId, found.id),
        eq(tiaIdentity.tiaAuthor, tiaAuthor)
      )
    );

  return identity === undefined ? refused('The claim was not recorded.') : { kind: 'claimed', identityId: identity.id };
}

/**
 * Confirms a claim, so that the claimant's name appears on that author's projects.
 *
 * @param database Where identities are kept.
 * @param request Who is confirming, and which claim.
 * @returns The confirmation, or why it was refused.
 * @remarks
 * Three refusals, each a rule: the confirmer needs `identity:confirm` in the claim's organisation
 * (a supervisor or an admin); **nobody confirms their own claim, an admin included** — the same two
 * pairs of eyes the Workshop Mode asks of a change plan; and an author already confirmed as somebody
 * else stays theirs, which the database enforces so that two confirmations racing each other cannot
 * both succeed.
 */
export async function confirmIdentity(
  database: PlatformDatabase,
  request: { readonly confirmerId: string; readonly identityId: number }
): Promise<IdentityResult> {
  const [identity] = await database.select().from(tiaIdentity).where(eq(tiaIdentity.id, request.identityId));

  if (identity === undefined) {
    return refused(`There is no identity claim ${request.identityId}.`);
  }

  if (identity.userId === request.confirmerId) {
    return refused('Nobody confirms their own identity. Ask a supervisor or an admin of the organisation.');
  }

  const role = (await roleOf(database, request.confirmerId, identity.organizationId)) ?? '';

  if (!roleCan(role, { identity: ['confirm'] })) {
    return refused('Only a supervisor or an admin of the organisation can confirm an identity.');
  }

  return markConfirmed(database, identity.id, request.confirmerId);
}

async function markConfirmed(database: PlatformDatabase, identityId: number, confirmerId: string): Promise<IdentityResult> {
  try {
    await database
      .update(tiaIdentity)
      .set({ status: 'confirmed', confirmedBy: confirmerId, confirmedAt: new Date() })
      .where(eq(tiaIdentity.id, identityId));
  } catch (failure) {
    if (isUniqueViolation(failure)) {
      return refused('That TIA author is already confirmed as somebody else on this station.');
    }

    throw failure;
  }

  return { kind: 'confirmed', identityId };
}

/**
 * Whether a database failure is a unique violation, wherever the driver put its code.
 *
 * @remarks
 * Drizzle wraps the driver's error and keeps it as the cause, so the code is looked for on both.
 */
function isUniqueViolation(failure: unknown): boolean {
  const codeOf = (value: unknown): unknown =>
    typeof value === 'object' && value !== null ? (value as { code?: unknown }).code : undefined;
  const cause = typeof failure === 'object' && failure !== null ? (failure as { cause?: unknown }).cause : undefined;

  return codeOf(failure) === UniqueViolation || codeOf(cause) === UniqueViolation;
}

function refused(reason: string): IdentityResult {
  return { kind: 'refused', reason };
}
