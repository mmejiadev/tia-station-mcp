/**
 * Which view the address bar is asking for.
 *
 * @remarks
 * The hash rather than a path, so the whole dashboard stays a static file that any server can hand
 * over without being taught to rewrite unknown paths onto index.html.
 *
 * It earns its place twice. A link to `#/workshop-gate` is a link somebody can send, which a tab
 * whose state lives only in memory is not. And it is what makes the views checkable without a
 * browser to click in: a headless render of that URL lands on that view.
 */

/** Turns a view's name into the fragment that opens it. */
export function hashFor(view: string): string {
  return `#/${view.toLowerCase().replace(/\s+/g, '-')}`;
}

/**
 * Reads which view a fragment is asking for.
 *
 * @param hash The fragment, as `location.hash` gives it, empty included.
 * @param views The views that exist, in order.
 * @returns The name of the view to open.
 * @remarks
 * A fragment that names no view falls back to the first one rather than rendering nothing. This is
 * the one place in the project where falling back is right: the fragment is a URL somebody typed or
 * a stale bookmark, nothing is gated on it, and an empty page would say less than the first view.
 */
export function viewFromHash(hash: string, views: readonly string[]): string {
  const first = views[0];

  if (first === undefined) {
    throw new Error('The dashboard was built with no views at all, so there is nothing to open.');
  }

  return views.find((view) => hashFor(view) === hash) ?? first;
}
