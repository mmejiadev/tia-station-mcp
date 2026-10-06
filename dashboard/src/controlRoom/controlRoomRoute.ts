/**
 * Where in the control room the address bar points.
 *
 * @remarks
 * Below the view's own fragment: `#/control-room` is the workspace, and
 * `#/control-room/projects/12/changes` one project's changes. A link to a project is then a link
 * somebody can send — the teacher reading a project from another machine is what phase 4 is for.
 */

/** The tabs of a project's page, in the order they are shown. */
export const ProjectSections = ['summary', 'changes', 'compilations'] as const;

/** One tab of a project's page. */
export type ProjectSection = (typeof ProjectSections)[number];

/** A place in the control room. */
export type ControlRoomPlace =
  | { readonly kind: 'workspace' }
  | { readonly kind: 'project'; readonly projectId: number; readonly section: ProjectSection };

// Nine digits stays inside PostgreSQL's integer range; anything longer cannot name a project.
const ProjectPath = /^\/projects\/(\d{1,9})(?:\/([a-z-]+))?$/;

/**
 * Reads the place a fragment points to.
 *
 * @param hash The fragment, as `location.hash` gives it.
 * @param base The control room's own fragment, e.g. `#/control-room`.
 * @returns The place; the workspace when the rest names nothing.
 * @remarks
 * A section that does not exist opens the project's summary rather than nothing, for the reason the
 * views fall back: the fragment is something typed or a stale bookmark, and nothing is gated on it.
 * The server still decides whether the project may be read.
 */
export function placeFromHash(hash: string, base: string): ControlRoomPlace {
  const match = hash.startsWith(base) ? ProjectPath.exec(hash.slice(base.length)) : null;

  if (match === null) {
    return { kind: 'workspace' };
  }

  const section = ProjectSections.find((name) => name === match[2]) ?? 'summary';

  return { kind: 'project', projectId: Number(match[1]), section };
}

/**
 * The fragment that opens a place.
 *
 * @param place Where to go.
 * @param base The control room's own fragment.
 * @returns The fragment, summary tabs without a suffix.
 */
export function hashForPlace(place: ControlRoomPlace, base: string): string {
  if (place.kind === 'workspace') {
    return base;
  }

  const project = `${base}/projects/${place.projectId}`;

  return place.section === 'summary' ? project : `${project}/${place.section}`;
}
