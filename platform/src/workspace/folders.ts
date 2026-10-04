import { eq, inArray, sql } from 'drizzle-orm';
import type { PlatformDatabase, PlatformTransaction } from '../db/connection.ts';
import { folder, project } from '../db/schema.ts';
import { requireProject, requireRole } from './projectAccess.ts';
import { done, refused, type WorkspaceResult } from './workspaceResult.ts';

/** A folder as the web shows it. */
export type FolderView = {
  readonly id: number;
  readonly organizationId: string;
  readonly parentId: number | null;
  readonly name: string;
};

// Long enough for "0965 Sistemes programables avançats", short enough to fit the sidebar.
const MaximumNameLength = 80;

// Deeper than any real organisation of projects. A tree that reaches it is refused, never trusted.
const MaximumDepth = 32;

/**
 * Creates a folder.
 *
 * @param database Where folders are kept.
 * @param request Who, in which organisation, under which parent (null for the top), and its name.
 * @returns The folder, or why not.
 * @remarks Organising projects is describing them, so it takes `project:describe`: an engineer or above.
 */
export async function createFolder(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly organizationId: string; readonly parentId: number | null; readonly name: string }
): Promise<WorkspaceResult<FolderView>> {
  const allowed = await requireRole(database, { ...request, permission: { project: ['describe'] } });
  const name = validName(request.name);

  if (allowed.kind === 'refused') {
    return allowed;
  }

  if (name.kind === 'refused') {
    return name;
  }

  return inOrganization(database, request.organizationId, async (transaction) => {
    const depth = await depthBelow(transaction, request.organizationId, request.parentId);

    if (depth.kind === 'refused') {
      return depth;
    }

    const [created] = await transaction
      .insert(folder)
      .values({ organizationId: request.organizationId, parentId: request.parentId, name: name.value })
      .returning();

    return created === undefined ? refused('invalid', 'The folder was not created.') : done(toView(created));
  });
}

/**
 * Renames a folder, moves it under another, or both.
 *
 * @param database Where folders are kept.
 * @param request Who, which folder, and what changes; a field left out is left as it is.
 * @returns The folder afterwards — unchanged when nothing was asked — or why not.
 * @remarks A folder cannot go inside itself or inside a folder within it: the tree would loop.
 */
export async function updateFolder(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly folderId: number; readonly name?: string; readonly parentId?: number | null }
): Promise<WorkspaceResult<FolderView>> {
  const found = await editableFolder(database, request.userId, request.folderId);

  if (found.kind === 'refused') {
    return found;
  }

  return inOrganization(database, found.value.organizationId, async (transaction) => {
    const changes = await validChanges(transaction, found.value, request);

    if (changes.kind === 'refused') {
      return changes;
    }

    if (Object.keys(changes.value).length === 0) {
      return found;
    }

    const [updated] = await transaction.update(folder).set(changes.value).where(eq(folder.id, request.folderId)).returning();

    return updated === undefined ? refused('not-found', 'There is no such folder.') : done(toView(updated));
  });
}

/**
 * Deletes a folder and the folders inside it.
 *
 * @param database Where folders are kept.
 * @param request Who, and which folder.
 * @returns The deleted folder, or why not.
 * @remarks The projects inside are not deleted: they return to the top level of the organisation.
 */
export async function deleteFolder(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly folderId: number }
): Promise<WorkspaceResult<FolderView>> {
  const found = await editableFolder(database, request.userId, request.folderId);

  if (found.kind === 'refused') {
    return found;
  }

  return inOrganization(database, found.value.organizationId, async (transaction) => {
    await transaction.delete(folder).where(eq(folder.id, request.folderId));

    return found;
  });
}

/**
 * Files a project in a folder, or at the top level.
 *
 * @param database Where projects and folders are kept.
 * @param request Who, which project, and which folder (null for the top level).
 * @returns The folder it is now in, or why not.
 */
export async function fileProject(
  database: PlatformDatabase,
  request: { readonly userId: string; readonly projectId: number; readonly folderId: number | null }
): Promise<WorkspaceResult<number | null>> {
  const accessible = await requireProject(database, { ...request, permission: { project: ['describe'] } });

  if (accessible.kind === 'refused') {
    return accessible;
  }

  return inOrganization(database, accessible.value.organizationId, async (transaction) => {
    const parent = await parentIn(transaction, accessible.value.organizationId, request.folderId);

    if (parent.kind === 'refused') {
      return parent;
    }

    await transaction.update(project).set({ folderId: request.folderId }).where(eq(project.id, request.projectId));

    return done(request.folderId);
  });
}

/**
 * Runs a change to an organisation's folders while no other change to them can run.
 *
 * @remarks
 * A transaction-scoped advisory lock per organisation. Without it two moves made at the same moment
 * — A under B while B goes under A — each pass the loop check against a tree the other is about to
 * change, and the tree loops. Organisations are independent, so they do not wait for each other.
 */
async function inOrganization<T>(
  database: PlatformDatabase,
  organizationId: string,
  change: (transaction: PlatformTransaction) => Promise<WorkspaceResult<T>>
): Promise<WorkspaceResult<T>> {
  return database.transaction(async (transaction) => {
    await transaction.execute(sql`select pg_advisory_xact_lock(hashtext(${`folders:${organizationId}`}))`);

    return change(transaction);
  });
}

async function editableFolder(database: PlatformDatabase, userId: string, folderId: number): Promise<WorkspaceResult<FolderView>> {
  const [found] = await database.select().from(folder).where(eq(folder.id, folderId));

  if (found === undefined) {
    return refused('not-found', 'There is no such folder.');
  }

  const allowed = await requireRole(database, { userId, organizationId: found.organizationId, permission: { project: ['describe'] } });

  if (allowed.kind === 'refused') {
    return allowed.refusal === 'not-found' ? refused('not-found', 'There is no such folder.') : allowed;
  }

  return done(toView(found));
}

async function validChanges(
  transaction: PlatformTransaction,
  current: FolderView,
  request: { readonly name?: string; readonly parentId?: number | null }
): Promise<WorkspaceResult<{ name?: string; parentId?: number | null }>> {
  const name = request.name === undefined ? undefined : validName(request.name);

  if (name?.kind === 'refused') {
    return name;
  }

  if (request.parentId === undefined) {
    return done(name === undefined ? {} : { name: name.value });
  }

  const parent = await newParent(transaction, current, request.parentId);

  if (parent.kind === 'refused') {
    return parent;
  }

  return done({ ...(name === undefined ? {} : { name: name.value }), parentId: request.parentId });
}

/** Whether a folder may move under a parent: same organisation, and not into itself or below. */
async function newParent(transaction: PlatformTransaction, moving: FolderView, parentId: number | null): Promise<WorkspaceResult<null>> {
  const parent = await parentIn(transaction, moving.organizationId, parentId);

  if (parent.kind === 'refused') {
    return parent;
  }

  const ancestors = await ancestorsOf(transaction, parentId);

  if (ancestors.kind === 'refused') {
    return ancestors;
  }

  if (ancestors.value.includes(moving.id)) {
    return refused('invalid', 'A folder cannot go inside itself or inside a folder within it.');
  }

  // The folder takes its whole subtree with it, so it is the subtree's height that must fit.
  const height = await heightOf(transaction, moving.id);

  return height.kind === 'refused' || ancestors.value.length + height.value > MaximumDepth
    ? refused('invalid', `Folders nest at most ${MaximumDepth} deep, and this move would go deeper.`)
    : done(null);
}

/**
 * How many levels a folder and the folders inside it span: 1 for a folder with none.
 *
 * @remarks Walked a level at a time, and refused past the limit like the walk up.
 */
async function heightOf(transaction: PlatformTransaction, folderId: number): Promise<WorkspaceResult<number>> {
  let level = [folderId];
  let height = 0;

  while (level.length > 0) {
    height++;

    if (height > MaximumDepth) {
      return refused('invalid', `Folders nest at most ${MaximumDepth} deep.`);
    }

    const children = await transaction.select({ id: folder.id }).from(folder).where(inArray(folder.parentId, level));
    level = children.map((child) => child.id);
  }

  return done(height);
}

/** Whether a new folder may go under a parent: it exists in the organisation, and is not too deep. */
async function depthBelow(transaction: PlatformTransaction, organizationId: string, parentId: number | null): Promise<WorkspaceResult<null>> {
  const parent = await parentIn(transaction, organizationId, parentId);

  if (parent.kind === 'refused') {
    return parent;
  }

  const ancestors = await ancestorsOf(transaction, parentId);

  if (ancestors.kind === 'refused') {
    return ancestors;
  }

  // The parent and everything above it, plus the new folder itself.
  return ancestors.value.length + 1 > MaximumDepth
    ? refused('invalid', `Folders nest at most ${MaximumDepth} deep.`)
    : done(null);
}

/**
 * A folder and every folder above it, up to the top level.
 *
 * @remarks
 * A walk that reaches the depth limit is a refusal. The first version returned what it had walked
 * so far, and a tree deeper than the limit then passed the loop check — the bound failed open.
 */
async function ancestorsOf(transaction: PlatformTransaction, folderId: number | null): Promise<WorkspaceResult<number[]>> {
  const ancestors: number[] = [];
  let cursor = folderId;

  while (cursor !== null) {
    if (ancestors.length >= MaximumDepth) {
      return refused('invalid', `Folders nest at most ${MaximumDepth} deep.`);
    }

    ancestors.push(cursor);

    const [above] = await transaction.select({ parentId: folder.parentId }).from(folder).where(eq(folder.id, cursor));
    cursor = above?.parentId ?? null;
  }

  return done(ancestors);
}

/** Whether a parent folder exists in the organisation; null, the top level, always does. */
async function parentIn(transaction: PlatformTransaction, organizationId: string, parentId: number | null): Promise<WorkspaceResult<null>> {
  if (parentId === null) {
    return done(null);
  }

  const [parent] = await transaction.select({ organizationId: folder.organizationId }).from(folder).where(eq(folder.id, parentId));

  return parent?.organizationId === organizationId ? done(null) : refused('invalid', 'That folder is not in this organisation.');
}

function validName(name: string): WorkspaceResult<string> {
  const trimmed = name.trim();

  if (trimmed.length === 0) {
    return refused('invalid', 'A folder needs a name.');
  }

  return trimmed.length > MaximumNameLength
    ? refused('invalid', `A folder name is at most ${MaximumNameLength} characters.`)
    : done(trimmed);
}

function toView(row: typeof folder.$inferSelect): FolderView {
  return { id: row.id, organizationId: row.organizationId, parentId: row.parentId, name: row.name };
}
