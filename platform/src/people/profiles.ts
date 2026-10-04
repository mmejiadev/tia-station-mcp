import { eq } from 'drizzle-orm';
import type { PlatformDatabase } from '../db/connection.ts';
import { profile } from '../db/schema.ts';

/** The fields a person can write about themselves. Every one is optional. */
export type ProfileFields = {
  readonly jobTitle?: string | null;
  readonly specialty?: string | null;
  readonly company?: string | null;
  readonly bio?: string | null;
  readonly languages?: string | null;
  readonly certifications?: string | null;
};

/** What asking to update a profile came to. */
export type ProfileResult =
  | { readonly kind: 'updated'; readonly profile: Required<ProfileFields> }
  | { readonly kind: 'refused'; readonly reason: string };

const Editable = ['jobTitle', 'specialty', 'company', 'bio', 'languages', 'certifications'] as const;

// Enough for a paragraph about oneself; a limit at all, so a profile cannot be used to store files.
const MaximumBioLength = 2000;
const MaximumFieldLength = 200;

/**
 * Updates the fields a person sent, and leaves the others as they were.
 *
 * @param database Where profiles are kept.
 * @param userId Whose profile — always the signed-in person's own; the server never takes it from
 *   the request.
 * @param fields What to change. Text is trimmed; an empty text clears the field.
 * @returns The profile afterwards, or why it was refused.
 */
export async function updateProfile(database: PlatformDatabase, userId: string, fields: ProfileFields): Promise<ProfileResult> {
  const tooLong = Editable.find((name) => (fields[name]?.trim().length ?? 0) > limitOf(name));

  if (tooLong !== undefined) {
    return { kind: 'refused', reason: `${tooLong} is longer than ${limitOf(tooLong)} characters.` };
  }

  const changes = Object.fromEntries(
    Editable.filter((name) => fields[name] !== undefined).map((name) => [name, normalise(fields[name] ?? null)])
  );

  await database.insert(profile).values({ userId, ...changes }).onConflictDoUpdate({ target: profile.userId, set: changes });

  return { kind: 'updated', profile: await readProfile(database, userId) };
}

/**
 * Reads a person's profile.
 *
 * @param database Where profiles are kept.
 * @param userId Whose.
 * @returns Every field, null where nothing was written — a person who never wrote one has an empty
 *   profile, not a missing one.
 */
export async function readProfile(database: PlatformDatabase, userId: string): Promise<Required<ProfileFields>> {
  const [found] = await database.select().from(profile).where(eq(profile.userId, userId));

  return {
    jobTitle: found?.jobTitle ?? null,
    specialty: found?.specialty ?? null,
    company: found?.company ?? null,
    bio: found?.bio ?? null,
    languages: found?.languages ?? null,
    certifications: found?.certifications ?? null
  };
}

function limitOf(name: (typeof Editable)[number]): number {
  return name === 'bio' ? MaximumBioLength : MaximumFieldLength;
}

function normalise(value: string | null): string | null {
  const trimmed = value?.trim() ?? '';

  return trimmed.length === 0 ? null : trimmed;
}
