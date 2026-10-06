import type { FolderView } from '../../../platform/src/workspace/folders.ts';
import type { ChangeView, CompilationView, ProjectView } from '../../../platform/src/workspace/projectRead.ts';
import type { OrganizationView } from '../../../platform/src/workspace/workspaceRead.ts';
import { read, send } from '../api.ts';

export type { ChangeView, CompilationView, FolderView, OrganizationView, ProjectView };

/**
 * The platform's endpoints, as the control room calls them.
 *
 * @remarks
 * The types come from `platform/src/` for the reason the harness's come from `harness/src/`: one
 * contract, so a payload the platform stops sending stops compiling here.
 *
 * None of these reaches TIA Portal or a controller. What they change is the platform's own record —
 * folders, where a project is filed, which organisation a station belongs to.
 */
const Base = '/api/platform';

/** Every organisation the signed-in person belongs to, with its folders and projects. */
export function readWorkspace(): Promise<{ organizations: OrganizationView[] }> {
  return read(`${Base}/workspace`);
}

/** One project's page. */
export function readProject(projectId: number): Promise<ProjectView> {
  return read(`${Base}/projects/${projectId}`);
}

/**
 * How many changes one page asks for.
 *
 * @remarks
 * Asked for explicitly rather than left to the server's default, because "a full page came back, so
 * there may be more" is only true when the page size is the one this side knows.
 */
export const ChangePageSize = 50;

/**
 * A page of a project's changes, newest first.
 *
 * @param projectId The project.
 * @param filter An outcome to keep, or empty for all; and the id the previous page ended at.
 */
export function readChanges(projectId: number, filter: { readonly outcome: string; readonly before?: number }): Promise<ChangeView[]> {
  const query = new URLSearchParams({ limit: String(ChangePageSize) });

  if (filter.outcome.length > 0) {
    query.set('outcome', filter.outcome);
  }

  if (filter.before !== undefined) {
    query.set('before', String(filter.before));
  }

  return read(`${Base}/projects/${projectId}/changes?${query.toString()}`);
}

/**
 * The most compilations one read returns: the platform's own page limit.
 *
 * @remarks
 * The platform has no paging for compilations, so the list asks for all it may and says when there
 * were more (see `describeShown`).
 */
export const CompilationPageSize = 100;

/** A project's newest compilations, each with its messages. */
export function readCompilations(projectId: number): Promise<CompilationView[]> {
  return read(`${Base}/projects/${projectId}/compilations?limit=${CompilationPageSize}`);
}

/** Creates a folder, at the top level when the parent is null. */
export function createFolder(request: { readonly organizationId: string; readonly parentId: number | null; readonly name: string }): Promise<FolderView> {
  return send('POST', `${Base}/folders`, request);
}

/** Renames a folder, moves it, or both. */
export function updateFolder(folderId: number, changes: { readonly name?: string; readonly parentId?: number | null }): Promise<FolderView> {
  return send('PATCH', `${Base}/folders/${folderId}`, changes);
}

/** Deletes a folder and the folders inside; its projects return to the top level. */
export function deleteFolder(folderId: number): Promise<FolderView> {
  return send('DELETE', `${Base}/folders/${folderId}`);
}

/** Files a project in a folder, or at the top level when the folder is null. */
export function fileProject(projectId: number, folderId: number | null): Promise<number | null> {
  return send('PATCH', `${Base}/projects/${projectId}`, { folderId });
}

/** Links a station to an organisation with the code its own machine printed. */
export function linkStation(organizationId: string, request: { readonly stationName: string; readonly pairingCode: string }): Promise<number> {
  return send('POST', `${Base}/organizations/${encodeURIComponent(organizationId)}/stations`, request);
}
