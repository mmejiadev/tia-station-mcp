import type { FolderView } from '../../../platform/src/workspace/folders.ts';
import type { OrganizationView, ProjectSummaryView } from '../../../platform/src/workspace/workspaceRead.ts';

/** A folder with the folders and projects inside it. */
export type FolderNode = {
  readonly folder: FolderView;
  readonly children: readonly FolderNode[];
  readonly projects: readonly ProjectSummaryView[];
};

/** An organisation's sidebar: its top-level folders, and the projects in none. */
export type WorkspaceTree = {
  readonly roots: readonly FolderNode[];
  readonly unfiled: readonly ProjectSummaryView[];
};

/** A folder as a list of choices shows it: indented by how deep it sits. */
export type FolderChoice = { readonly id: number; readonly name: string; readonly depth: number };

/**
 * Turns the flat lists the API sends into the tree the sidebar draws.
 *
 * @param organization The organisation, with its folders and projects.
 * @param search Text to keep projects by — their name or their station's — or empty for all.
 * @returns The tree.
 * @remarks
 * **Nothing the API sent is left out of the tree.** A folder whose parent is not in the list, or one
 * caught in a loop the server should have prevented, is drawn at the top level; a project filed in a
 * folder that is not in the list is drawn as unfiled. A sidebar that silently dropped them would be
 * claiming the organisation has fewer projects than it has.
 *
 * While searching, a folder is kept only when something inside it matches.
 */
export function buildTree(organization: OrganizationView, search: string): WorkspaceTree {
  const matches = projectMatcher(search);
  const projects = organization.projects.filter(matches);
  const folderIds = new Set(organization.folders.map((folder) => folder.id));
  const placed = new Set<number>();
  const childrenOf = groupBy(organization.folders, (folder) => folder.parentId);
  const projectsIn = groupBy(projects, (project) => (project.folderId !== null && folderIds.has(project.folderId) ? project.folderId : null));

  const nodeOf = (folder: FolderView): FolderNode => {
    placed.add(folder.id);

    const children = (childrenOf.get(folder.id) ?? []).filter((child) => !placed.has(child.id)).map(nodeOf);

    return { folder, children, projects: projectsIn.get(folder.id) ?? [] };
  };

  const all = (childrenOf.get(null) ?? []).map(nodeOf);

  // Whatever the walk from the top did not reach — a missing parent, a loop — joins the top level.
  // One at a time, so a loop is entered once: placing A also places B, which is then not repeated.
  for (const folder of organization.folders) {
    if (!placed.has(folder.id)) {
      all.push(nodeOf(folder));
    }
  }

  return { roots: search.trim().length === 0 ? all : all.flatMap(pruned), unfiled: projectsIn.get(null) ?? [] };
}

/**
 * Every folder of a tree, in the order the sidebar draws them, with its depth.
 *
 * @param tree The tree.
 * @param excluded A folder to leave out together with everything inside it — the one being moved,
 *   which cannot go inside itself.
 * @returns The folders.
 */
export function folderChoices(tree: WorkspaceTree, excluded?: number): FolderChoice[] {
  const walk = (nodes: readonly FolderNode[], depth: number): FolderChoice[] =>
    nodes
      .filter((node) => node.folder.id !== excluded)
      .flatMap((node) => [{ id: node.folder.id, name: node.folder.name, depth }, ...walk(node.children, depth + 1)]);

  return walk(tree.roots, 0);
}

/**
 * How many projects a folder holds, its subfolders included.
 *
 * @param node The folder.
 * @returns The count.
 */
export function projectCount(node: FolderNode): number {
  return node.projects.length + node.children.reduce((total, child) => total + projectCount(child), 0);
}

function projectMatcher(search: string): (project: ProjectSummaryView) => boolean {
  const needle = search.trim().toLowerCase();

  return (project) =>
    needle.length === 0 || project.name.toLowerCase().includes(needle) || project.stationName.toLowerCase().includes(needle);
}

function pruned(node: FolderNode): FolderNode[] {
  const children = node.children.flatMap(pruned);

  return children.length === 0 && node.projects.length === 0 ? [] : [{ ...node, children }];
}

function groupBy<T>(items: readonly T[], keyOf: (item: T) => number | null): Map<number | null, T[]> {
  const groups = new Map<number | null, T[]>();

  for (const item of items) {
    const key = keyOf(item);
    const group = groups.get(key);

    if (group === undefined) {
      groups.set(key, [item]);
    } else {
      group.push(item);
    }
  }

  return groups;
}
