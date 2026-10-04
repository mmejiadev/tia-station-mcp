import { eq } from 'drizzle-orm';
import { roleCan, type PlatformPermission } from '../auth/permissions.ts';
import type { PlatformDatabase } from '../db/connection.ts';
import { project, station } from '../db/schema.ts';
import { roleOf } from '../people/membership.ts';
import { done, refused, type WorkspaceResult } from './workspaceResult.ts';

/** A project a person may act on, where it belongs, and the row the check read. */
export type AccessibleProject = {
  readonly projectId: number;
  readonly organizationId: string;
  readonly stationId: number;
  readonly stationName: string;
  readonly role: string;
  /** The project as the check found it, so a caller need not read it again. */
  readonly row: typeof project.$inferSelect;
};

const NotFound = 'There is no such project.';

/**
 * Checks that a person may do something with a project.
 *
 * @param database Where projects and memberships are kept.
 * @param request Who, which project, and what for.
 * @returns The project's place, or why not.
 * @remarks
 * A project belongs to the organisation its station is linked to. **A project the person cannot
 * read is "not found", not "forbidden"**: telling a stranger that a project exists, but is not
 * theirs, already tells them something about somebody else's work. Only a member who can read it
 * learns that their role does not allow what they asked.
 */
export async function requireProject(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly projectId: number; readonly permission: PlatformPermission }
): Promise<WorkspaceResult<AccessibleProject>> {
  const [found] = await database
    .select({ row: project, stationId: station.id, stationName: station.name, organizationId: station.organizationId })
    .from(project)
    .innerJoin(station, eq(project.stationId, station.id))
    .where(eq(project.id, request.projectId));

  if (found === undefined || found.organizationId === null) {
    return refused('not-found', NotFound);
  }

  const role = (await roleOf(database, request.userId, found.organizationId)) ?? '';

  if (!roleCan(role, { project: ['read'] })) {
    return refused('not-found', NotFound);
  }

  if (!roleCan(role, request.permission)) {
    return refused('forbidden', 'Your role in this organisation does not allow that.');
  }

  return done({
    projectId: found.row.id,
    organizationId: found.organizationId,
    stationId: found.stationId,
    stationName: found.stationName,
    role,
    row: found.row
  });
}

/**
 * Checks that a person holds a permission in an organisation.
 *
 * @param database Where memberships are kept.
 * @param request Who, which organisation, and what for.
 * @returns The role, or why not.
 */
export async function requireRole(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly organizationId: string; readonly permission: PlatformPermission }
): Promise<WorkspaceResult<string>> {
  const role = await roleOf(database, request.userId, request.organizationId);

  if (role === undefined) {
    return refused('not-found', 'There is no such organisation.');
  }

  if (!roleCan(role, request.permission)) {
    return refused('forbidden', 'Your role in this organisation does not allow that.');
  }

  return done(role);
}
