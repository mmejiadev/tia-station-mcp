import { and, eq } from 'drizzle-orm';
import type { PlatformDatabase } from '../db/connection.ts';
import { member } from '../db/schema.ts';

/**
 * The role a person holds in an organisation.
 *
 * @param database Where memberships are kept.
 * @param userId The person.
 * @param organizationId The organisation.
 * @returns The role as stored, or undefined when the person is not a member.
 * @remarks
 * Somebody who is not a member gets undefined, and a member whose role is empty gets ''; through
 * `roleCan` both grant nothing. The absence of a role is a refusal, never a default permission.
 */
export async function roleOf(database: PlatformDatabase, userId: string, organizationId: string): Promise<string | undefined> {
  const [found] = await database
    .select({ role: member.role })
    .from(member)
    .where(and(eq(member.userId, userId), eq(member.organizationId, organizationId)))
    .limit(1);

  return found?.role;
}
