import type { ReactNode } from 'react';

/** One entry: the swatch, and the word that means the same thing the swatch does. */
export type LegendEntry = {
  readonly label: string;
  /** A CSS colour, taken from the same tokens the chart is drawn from. */
  readonly colour: string;
  /** What this series counts, shown under the label so the legend explains rather than only names. */
  readonly meaning?: string;
};

/**
 * The legend, in HTML rather than on the canvas.
 *
 * @remarks
 * On the canvas it would be an image: unreadable to a screen reader, unselectable, and liable to be
 * clipped. Here it is text.
 *
 * It is present whenever a chart has two or more series, and never for one — a legend box for a
 * single series is furniture pretending to be information, and the panel's title already names it.
 */
export function ChartLegend({ entries }: { readonly entries: readonly LegendEntry[] }): ReactNode {
  if (entries.length < 2) {
    return undefined;
  }

  return (
    <ul className="mb-4 flex flex-wrap gap-x-6 gap-y-2">
      {entries.map((entry) => (
        <li key={entry.label} className="flex items-start gap-2 text-xs">
          <span
            aria-hidden="true"
            className="mt-1 size-2.5 shrink-0 rounded-[2px]"
            style={{ backgroundColor: entry.colour }}
          />
          <span>
            <span className="font-medium">{entry.label}</span>
            {entry.meaning === undefined ? undefined : (
              <span className="text-muted-foreground"> — {entry.meaning}</span>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}
