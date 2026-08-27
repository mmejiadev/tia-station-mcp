/**
 * The colours a chart is allowed to use, read from the stylesheet rather than written here.
 *
 * @remarks
 * Chart.js paints onto a canvas, so it needs concrete colours: a CSS custom property means nothing
 * to it. Reading them back out of the document keeps one definition — the stylesheet — instead of a
 * second copy that drifts, and it is what makes the dark theme work at all, since the same slots
 * resolve to different steps once `.dark` is on the document.
 *
 * The slot order is the accessibility mechanism, not decoration: hues are assigned 1, 2, 3 in that
 * order and never cycled, because that ordering is what keeps adjacent series apart for a
 * colour-blind reader. A fourth series is not a fourth hue — it is a sign the chart should be split.
 */
export type ChartPalette = {
  /** Categorical slots, in assignment order. */
  readonly series: readonly string[];
  /** One hue for magnitude. */
  readonly sequential: string;
  readonly good: string;
  readonly warning: string;
  readonly critical: string;
  /** Ink for labels and values. Never a series colour: text wears text colours. */
  readonly text: string;
  readonly mutedText: string;
  /** The recessive grid, and the surface a mark is drawn on. */
  readonly grid: string;
  readonly surface: string;
};

/** Reads the palette currently in force. Call it again when the theme changes. */
export function readChartPalette(): ChartPalette {
  const style = getComputedStyle(document.documentElement);
  const read = (name: string): string => style.getPropertyValue(name).trim();

  return {
    series: [read('--series-1'), read('--series-2'), read('--series-3')],
    sequential: read('--sequential-450'),
    good: read('--status-good'),
    warning: read('--status-warning'),
    critical: read('--status-critical'),
    text: read('--foreground'),
    mutedText: read('--muted-foreground'),
    grid: read('--grid'),
    surface: read('--card')
  };
}

/**
 * The colour for one categorical slot.
 *
 * @param index Which slot, counting from zero.
 * @remarks
 * It refuses past the last slot instead of wrapping. Wrapping is how a chart ends up drawing two
 * different things in the same colour and looking perfectly fine while it does it.
 */
export function seriesColour(palette: ChartPalette, index: number): string {
  const colour = palette.series[index];

  if (colour === undefined) {
    throw new Error(
      `This chart asked for categorical slot ${index + 1} and there are ${palette.series.length}. ` +
        'Split the chart or fold the tail into one series: a generated hue is indistinguishable from ' +
        'one already on screen.'
    );
  }

  return colour;
}
