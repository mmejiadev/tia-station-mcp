import {
  BarElement,
  CategoryScale,
  Chart,
  LinearScale,
  Tooltip,
  type ChartOptions,
  type TooltipItem
} from 'chart.js';
import { useMemo, type ReactNode } from 'react';
import { Bar } from 'react-chartjs-2';
import { readChartPalette, seriesColour } from './palette.ts';
import { useTheme } from '../theme.tsx';

Chart.register(BarElement, CategoryScale, LinearScale, Tooltip);

/** One series of bars. */
export type BarSeries = {
  readonly label: string;
  readonly values: readonly number[];
  /**
   * Which colour job this series does.
   *
   * @remarks
   * `magnitude` is the single-hue default and is right whenever the bars are the same kind of thing
   * measured for different categories. `identity` takes the next categorical slot, and is only
   * correct when telling the series apart is the point.
   */
  readonly colour: 'magnitude' | 'identity';
};

type Properties = {
  readonly categories: readonly string[];
  readonly series: readonly BarSeries[];
  /** Stacked when the series add up to a whole; grouped when they are separate comparisons. */
  readonly stacked?: boolean;
  /** How to write one value in a tooltip and on the axis. */
  readonly format: (value: number) => string;
  /** What one row means, said in words for the tooltip: "attempt(s)", "of mean duration". */
  readonly unit: string;
};

/**
 * Horizontal bars: the form for comparing magnitude across named things.
 *
 * @remarks
 * Horizontal rather than vertical because the categories here are specification names and phase
 * names, which are words. Vertical columns would either rotate those labels or truncate them, and a
 * chart whose labels have to be read sideways is a chart nobody reads.
 *
 * The bars are thin, their data end is rounded, and stacked segments are separated by a two-pixel
 * ring in the surface colour so that adjacent fills stay legible without a border of their own.
 */
export function BarChart({ categories, series, stacked = false, format, unit }: Properties): ReactNode {
  const { theme } = useTheme();

  const { data, options } = useMemo(() => {
    // Read on every theme change: the same slots resolve to different steps under `.dark`.
    const palette = readChartPalette();
    let identitySlot = 0;

    const datasets = series.map((one) => {
      const colour = one.colour === 'magnitude' ? palette.sequential : seriesColour(palette, identitySlot++);

      return {
        label: one.label,
        data: [...one.values],
        backgroundColor: colour,
        // The gap between stacked segments, drawn in the surface colour rather than left empty, so it
        // works whatever is behind the chart.
        borderColor: palette.surface,
        borderWidth: stacked ? 2 : 0,
        borderRadius: 4,
        borderSkipped: false,
        barThickness: 18,
        maxBarThickness: 18
      };
    });

    return {
      data: { labels: [...categories], datasets },
      options: barOptions(palette, stacked, format, unit)
    };
    // The theme is what makes the palette worth re-reading; it is a dependency even though nothing
    // below names it.
  }, [categories, series, stacked, format, unit, theme]);

  return (
    <div style={{ height: `${Math.max(categories.length * 34 + 40, 120)}px` }}>
      <Bar data={data} options={options} />
    </div>
  );
}

/** The chart's furniture: recessive grid, no vertical rules, one axis. */
function barOptions(
  palette: ReturnType<typeof readChartPalette>,
  stacked: boolean,
  format: (value: number) => string,
  unit: string
): ChartOptions<'bar'> {
  return {
    indexAxis: 'y',
    responsive: true,
    maintainAspectRatio: false,
    // No animation. These charts redraw whenever the live stream says the store changed, and a bar
    // that regrows from zero every second while a run is going turns a dashboard into a fidget. It
    // also means what is on screen is the data, never a frame on the way to it.
    animation: false,
    // The legend is drawn in HTML beside the chart instead, where it can be read by a screen reader
    // and cannot be clipped by the canvas.
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: palette.surface,
        titleColor: palette.text,
        bodyColor: palette.text,
        borderColor: palette.grid,
        borderWidth: 1,
        padding: 10,
        displayColors: true,
        callbacks: {
          label: (item: TooltipItem<'bar'>) => {
            // Chart.js types this as possibly null and it is right: a gap in a dataset is null, and
            // formatting null as a number is how "no measurement" becomes "zero" on a tooltip.
            const value = item.parsed.x;

            return value === null
              ? `${item.dataset.label}: not recorded`
              : `${item.dataset.label}: ${format(value)} ${unit}`.trim();
          }
        }
      }
    },
    scales: {
      x: {
        stacked,
        beginAtZero: true,
        border: { display: false },
        grid: { color: palette.grid, drawTicks: false },
        ticks: { color: palette.mutedText, padding: 8, callback: (value) => format(Number(value)) }
      },
      y: {
        stacked,
        border: { display: false },
        // No horizontal rules behind the bars: the bars are the marks, the grid is furniture.
        grid: { display: false },
        ticks: { color: palette.text, padding: 8 }
      }
    }
  };
}
