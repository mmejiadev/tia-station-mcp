import type { PlatformDatabase } from '../src/db/connection.ts';
import { member, organization, station, user } from '../src/db/schema.ts';

/**
 * People, organisations and memberships written straight into the test database.
 *
 * @remarks
 * Sign-in through Google or GitHub cannot run in a test, and does not need to: what these tests
 * check is what the platform does with a person once Better Auth has created them, and these rows
 * are the rows Better Auth would have written.
 */
export async function aPerson(database: PlatformDatabase, id: string): Promise<string> {
  await database.insert(user).values({ id, name: id, email: `${id}@example.com`, emailVerified: true });

  return id;
}

/** An organisation, with no members yet. */
export async function anOrganization(database: PlatformDatabase, id: string): Promise<string> {
  await database.insert(organization).values({ id, name: id, slug: id, createdAt: new Date() });

  return id;
}

/** Makes a person a member of an organisation with a role. */
export async function addMember(
  database: PlatformDatabase,
  organizationId: string,
  userId: string,
  role: string
): Promise<void> {
  await database.insert(member).values({ id: `${organizationId}-${userId}`, organizationId, userId, role, createdAt: new Date() });
}

/** A station whose history has been imported. */
export async function aStation(database: PlatformDatabase, name: string): Promise<void> {
  await database.insert(station).values({ name });
}
