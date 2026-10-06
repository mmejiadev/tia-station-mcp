/**
 * The sentence a cut list owes its reader.
 *
 * @param shown How many rows are on screen.
 * @param total How many exist.
 * @param noun What they are, plural: "compilations".
 * @returns "Showing the 100 newest of 120 compilations.", or empty when nothing was left out.
 * @remarks
 * A list that stops at a page size without saying so claims the project has no more — measured by
 * the review of 2026-10-06: a tab counting 120 over a list of 50.
 */
export function describeShown(shown: number, total: number, noun: string): string {
  return shown >= total ? '' : `Showing the ${shown} newest of ${total} ${noun}.`;
}
