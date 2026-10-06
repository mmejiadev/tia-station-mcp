import { and, count, desc, eq, inArray, lt, sql, type SQL } from 'drizzle-orm';
import type { PlatformDatabase } from '../db/connection.ts';
import { change, compilation, compilationMessage, project, tiaIdentity, user } from '../db/schema.ts';
import { requireProject } from './projectAccess.ts';
import { latestCompilations, statusOf, type ProjectStatus } from './workspaceRead.ts';
import { done, type WorkspaceResult } from './workspaceResult.ts';

/** A project's page: what TIA Portal says about it, who made it, and how it stands. */
export type ProjectView = {
  readonly id: number;
  readonly name: string;
  readonly tiaPath: string;
  readonly folderId: number | null;
  readonly stationName: string;
  readonly role: string;
  readonly tiaAuthor: string | null;
  /** The person confirmed as that author, or null while nobody is. */
  readonly author: { readonly name: string; readonly image: string | null } | null;
  readonly tiaCreatedAt: string | null;
  readonly tiaModifiedAt: string | null;
  readonly status: ProjectStatus;
  readonly errorCount: number;
  readonly warningCount: number;
  readonly lastCompiledAt: string | null;
  readonly compilationCount: number;
  /** Changes by outcome: Planned, Applied, Refused, Failed — and any other the trail holds. */
  readonly changesByOutcome: Readonly<Record<string, number>>;
};

/** One change as the history lists it. */
export type ChangeView = {
  readonly id: number;
  readonly occurredAt: string | null;
  readonly tool: string;
  readonly target: string;
  readonly outcome: string;
  readonly mode: string;
  readonly detail: string;
  readonly backupPath: string;
  readonly planId: string;
};

/** One compilation with its messages. */
export type CompilationView = {
  readonly id: number;
  readonly compiledAt: string | null;
  readonly softwarePath: string;
  readonly severity: string;
  readonly errorCount: number;
  readonly warningCount: number;
  readonly messages: readonly { readonly severity: string; readonly path: string; readonly description: string }[];
};

/** The most rows one page of history holds; the web asks for the next with `before`. */
export const MaximumPage = 100;

/** The rows a page holds when the caller does not say. */
const DefaultPage = 50;

/**
 * A project's page.
 *
 * @param database Where it all is.
 * @param request Who is asking, and which project.
 * @returns The page, or "not found" when the project does not exist or is not theirs to read.
 */
export async function readProject(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly projectId: number }
): Promise<WorkspaceResult<ProjectView>> {
  const accessible = await requireProject(database, { ...request, permission: { project: ['read'] } });

  if (accessible.kind === 'refused') {
    return accessible;
  }

  const row = { project: accessible.value.row, stationName: accessible.value.stationName };

  // Independent of each other, so asked for together rather than one after another.
  const [latestByProject, author, compilations, outcomes] = await Promise.all([
    latestCompilations(database, [row.project.id]),
    confirmedAuthor(database, accessible.value, row.project.tiaAuthor),
    database.select({ total: count() }).from(compilation).where(eq(compilation.projectId, row.project.id)),
    changesByOutcome(database, row.project.id)
  ]);
  const latest = latestByProject.get(row.project.id);

  return done({
    ...describe(row.project, row.stationName, accessible.value.role),
    author,
    status: statusOf(latest),
    errorCount: latest?.errorCount ?? 0,
    warningCount: latest?.warningCount ?? 0,
    lastCompiledAt: latest?.compiledAt?.toISOString() ?? null,
    compilationCount: compilations[0]?.total ?? 0,
    changesByOutcome: outcomes
  });
}

/**
 * A page of a project's change history, newest first.
 *
 * @param database Where it all is.
 * @param request Who, which project, an optional outcome to keep, how many, and where the previous
 *   page ended (the id of its last change).
 * @returns The changes, or "not found".
 */
export async function listChanges(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly projectId: number; readonly outcome?: string; readonly limit?: number; readonly before?: number }
): Promise<WorkspaceResult<ChangeView[]>> {
  const accessible = await requireProject(database, { ...request, permission: { project: ['read'] } });

  if (accessible.kind === 'refused') {
    return accessible;
  }

  const conditions: SQL[] = [eq(change.projectId, request.projectId)];

  if (request.outcome !== undefined && request.outcome.length > 0) {
    conditions.push(eq(change.outcome, request.outcome));
  }

  if (request.before !== undefined) {
    conditions.push(lt(change.id, request.before));
  }

  const rows = await database
    .select()
    .from(change)
    .where(and(...conditions))
    .orderBy(desc(change.id))
    .limit(Math.min(Math.max(request.limit ?? DefaultPage, 1), MaximumPage));

  return done(rows.map(toChangeView));
}

/**
 * A project's compilations, newest first, each with its messages.
 *
 * @param database Where it all is.
 * @param request Who, which project, and how many.
 * @returns The compilations, or "not found".
 */
export async function listCompilations(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly projectId: number; readonly limit?: number }
): Promise<WorkspaceResult<CompilationView[]>> {
  const accessible = await requireProject(database, { ...request, permission: { project: ['read'] } });

  if (accessible.kind === 'refused') {
    return accessible;
  }

  const rows = await database
    .select()
    .from(compilation)
    .where(eq(compilation.projectId, request.projectId))
    .orderBy(sql`${compilation.compiledAt} desc nulls last`, desc(compilation.id))
    .limit(Math.min(Math.max(request.limit ?? DefaultPage, 1), MaximumPage));

  const messages = await messagesOf(database, rows.map((row) => row.id));

  return done(rows.map((row) => ({ ...toCompilationView(row), messages: messages.get(row.id) ?? [] })));
}

function describe(row: typeof project.$inferSelect, stationName: string, role: string) {
  return {
    id: row.id,
    name: row.name,
    tiaPath: row.tiaPath,
    folderId: row.folderId,
    stationName,
    role,
    tiaAuthor: row.tiaAuthor,
    tiaCreatedAt: row.tiaCreatedAt?.toISOString() ?? null,
    tiaModifiedAt: row.tiaModifiedAt?.toISOString() ?? null
  };
}

/** The person confirmed as the project's TIA author in its organisation, on its station. */
async function confirmedAuthor(
  database: PlatformDatabase,
  place: { readonly organizationId: string; readonly stationId: number },
  tiaAuthor: string | null
): Promise<ProjectView['author']> {
  if (tiaAuthor === null || tiaAuthor.length === 0) {
    return null;
  }

  const [found] = await database
    .select({ name: user.name, image: user.image })
    .from(tiaIdentity)
    .innerJoin(user, eq(tiaIdentity.userId, user.id))
    .where(
      and(
        eq(tiaIdentity.organizationId, place.organizationId),
        eq(tiaIdentity.stationId, place.stationId),
        eq(tiaIdentity.tiaAuthor, tiaAuthor),
        eq(tiaIdentity.status, 'confirmed')
      )
    );

  return found ?? null;
}

async function changesByOutcome(database: PlatformDatabase, projectId: number): Promise<Record<string, number>> {
  const rows = await database
    .select({ outcome: change.outcome, total: count() })
    .from(change)
    .where(eq(change.projectId, projectId))
    .groupBy(change.outcome);

  return Object.fromEntries(rows.map((row) => [row.outcome, row.total]));
}

async function messagesOf(database: PlatformDatabase, compilationIds: readonly number[]): Promise<Map<number, CompilationView['messages'][number][]>> {
  const grouped = new Map<number, CompilationView['messages'][number][]>();

  if (compilationIds.length === 0) {
    return grouped;
  }

  const rows = await database
    .select()
    .from(compilationMessage)
    .where(inArray(compilationMessage.compilationId, [...compilationIds]))
    .orderBy(compilationMessage.compilationId, compilationMessage.position);

  for (const row of rows) {
    const list = grouped.get(row.compilationId) ?? [];
    list.push({ severity: row.severity, path: row.path, description: row.description });
    grouped.set(row.compilationId, list);
  }

  return grouped;
}

function toChangeView(row: typeof change.$inferSelect): ChangeView {
  return {
    id: row.id,
    occurredAt: row.occurredAt?.toISOString() ?? null,
    tool: row.tool,
    target: row.target,
    outcome: row.outcome,
    mode: row.mode,
    detail: row.detail,
    backupPath: row.backupPath,
    planId: row.planId
  };
}

function toCompilationView(row: typeof compilation.$inferSelect): Omit<CompilationView, 'messages'> {
  return {
    id: row.id,
    compiledAt: row.compiledAt?.toISOString() ?? null,
    softwarePath: row.softwarePath,
    severity: row.severity,
    errorCount: row.errorCount,
    warningCount: row.warningCount
  };
}
