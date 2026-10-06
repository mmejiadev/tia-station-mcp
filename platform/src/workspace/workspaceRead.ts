import { eq, inArray, sql } from 'drizzle-orm';
import { roleCan } from '../auth/permissions.ts';
import type { PlatformDatabase } from '../db/connection.ts';
import { compilation, folder, member, organization, project, station } from '../db/schema.ts';
import type { FolderView } from './folders.ts';

/**
 * How a project stands, from its latest compilation.
 *
 * @remarks
 * Four words the web turns into a LED: `error` and `warning` from the counts, `ok` for a compilation
 * with neither, and `unknown` for a project never compiled through the MCP — said as such rather than
 * shown as healthy.
 */
export type ProjectStatus = 'ok' | 'warning' | 'error' | 'unknown';

/** A project as the sidebar lists it. */
export type ProjectSummaryView = {
  readonly id: number;
  readonly name: string;
  readonly folderId: number | null;
  readonly stationName: string;
  readonly status: ProjectStatus;
  readonly errorCount: number;
  readonly warningCount: number;
  readonly lastCompiledAt: string | null;
};

/** One organisation as the sidebar shows it: its folders and its projects. */
export type OrganizationView = {
  readonly id: string;
  readonly name: string;
  readonly role: string;
  readonly folders: readonly FolderView[];
  readonly projects: readonly ProjectSummaryView[];
};

type LatestCompilation = { severity: string; errorCount: number; warningCount: number; compiledAt: Date | null };

/**
 * Everything a person may see: each organisation they belong to, its folders and its projects.
 *
 * @param database Where it all is.
 * @param userId The signed-in person.
 * @returns One entry per organisation whose role lets them read projects.
 * @remarks
 * Projects come from the stations linked to the organisation; a station nobody linked shows nothing.
 * Four queries however many organisations — memberships, then folders, projects and latest
 * compilations for all the readable ones at once — and each organisation's rows are picked out by
 * its id in code, so a project can only appear under the organisation its station is linked to.
 */
export async function readWorkspace(database: PlatformDatabase, userId: string): Promise<OrganizationView[]> {
  const memberships = await database
    .select({ id: organization.id, name: organization.name, role: member.role })
    .from(member)
    .innerJoin(organization, eq(member.organizationId, organization.id))
    .where(eq(member.userId, userId))
    .orderBy(organization.name);

  const readable = memberships.filter((membership) => roleCan(membership.role, { project: ['read'] }));

  if (readable.length === 0) {
    return [];
  }

  const ids = readable.map((membership) => membership.id);
  const [folders, projects] = await Promise.all([foldersOf(database, ids), projectsOf(database, ids)]);
  const latest = await latestCompilations(database, projects.map((row) => row.id));

  return readable.map((membership) => ({
    ...membership,
    folders: folders.filter((item) => item.organizationId === membership.id),
    projects: projects
      .filter((row) => row.organizationId === membership.id)
      .map(({ organizationId: _organization, ...row }) => withStatus(row, latest.get(row.id)))
  }));
}

function foldersOf(database: PlatformDatabase, organizationIds: string[]): Promise<FolderView[]> {
  return database
    .select({ id: folder.id, organizationId: folder.organizationId, parentId: folder.parentId, name: folder.name })
    .from(folder)
    .where(inArray(folder.organizationId, organizationIds))
    .orderBy(folder.name);
}

async function projectsOf(database: PlatformDatabase, organizationIds: string[]) {
  const rows = await database
    .select({ id: project.id, name: project.name, folderId: project.folderId, stationName: station.name, organizationId: station.organizationId })
    .from(project)
    .innerJoin(station, eq(project.stationId, station.id))
    .where(inArray(station.organizationId, organizationIds))
    .orderBy(project.name);

  return rows.map((row) => ({ ...row, organizationId: row.organizationId ?? '' }));
}

/**
 * The latest compilation of each project, by the moment it finished.
 *
 * @param database Where compilations are.
 * @param projectIds The projects.
 * @returns Each project's latest, when it has one.
 * @remarks PostgreSQL's DISTINCT ON keeps the first row of each project in the given order.
 */
export async function latestCompilations(database: PlatformDatabase, projectIds: readonly number[]): Promise<Map<number, LatestCompilation>> {
  if (projectIds.length === 0) {
    return new Map();
  }

  const rows = await database
    .selectDistinctOn([compilation.projectId], {
      projectId: compilation.projectId,
      severity: compilation.severity,
      errorCount: compilation.errorCount,
      warningCount: compilation.warningCount,
      compiledAt: compilation.compiledAt
    })
    .from(compilation)
    .where(inArray(compilation.projectId, [...projectIds]))
    .orderBy(compilation.projectId, sql`${compilation.compiledAt} desc nulls last`, sql`${compilation.id} desc`);

  return new Map(rows.map((row) => [row.projectId, row]));
}

/**
 * The status a compilation gives a project.
 *
 * @param latest The latest compilation, or undefined when there is none.
 * @returns The status.
 */
export function statusOf(latest: LatestCompilation | undefined): ProjectStatus {
  if (latest === undefined) {
    return 'unknown';
  }

  if (latest.errorCount > 0) {
    return 'error';
  }

  return latest.warningCount > 0 ? 'warning' : 'ok';
}

function withStatus(
  row: { id: number; name: string; folderId: number | null; stationName: string },
  latest: LatestCompilation | undefined
): ProjectSummaryView {
  return {
    ...row,
    status: statusOf(latest),
    errorCount: latest?.errorCount ?? 0,
    warningCount: latest?.warningCount ?? 0,
    lastCompiledAt: latest?.compiledAt?.toISOString() ?? null
  };
}
