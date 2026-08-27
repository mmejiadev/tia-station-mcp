import {
  CategoryScale,
  Chart,
  Filler,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
  type ChartOptions,
  type TooltipItem
} from 'chart.js';
import { useMemo, type ReactNode } from 'react';
import { Line } from 'react-chartjs-2';
import { readChartPalette, seriesColour } from './palette.ts';
import { useTheme } from '../theme.tsx';

Chart.register(CategoryScale, Filler, LineElement, LinearScale, PointElement, Tooltip);

/** One line: a rate at each point, from 0 to 1. */
export type RateSeries = {
  readonly label: string;
  readonly rates: readonly number[];
};

type Properties = {
  /** What each point is, in order, oldest first. */
  readonly labels: readonly string[];
  readonly series: readonly RateSeries[];
};

/**
 * One or two rates over successive runs: the form for a trend.
 *
 * @remarks
 * The y axis runs from 0 to 1 always, never fitted to the data. A rate axis that rescales itself
 * turns a wobble between 98% and 100% into a mountain range, which is the most common way a true
 * chart tells a lie — and here it would matter, because one of these lines really is pinned at 100%
 * and the flatness is the finding.
 *
 * A single line is filled underneath, because there is nothing for the fill to be confused with. Two
 * lines are not: the fill of one would lie under the other, making a third colour that means nothing
 * and leaving the reader to work out which of the three is a series.
 */
export function RateLine({ labels, series }: Properties): ReactNode {
  const { theme } = useTheme();

  const { data, options } = useMemo(() => {
    const palette = readChartPalette();

    return {
      data: {
        labels: [...labels],
        datasets: series.map((one, index) => {
          const colour = seriesColour(palette, index);

          return {
            label: one.label,
            data: [...one.rates],
            borderColor: colour,
            backgroundColor: series.length === 1 ? `${colour}1f` : 'transparent',
            borderWidth: 2,
            pointRadius: 4,
            pointHoverRadius: 6,
            pointBackgroundColor: colour,
            // A ring in the surface colour so a point that lands on the line stays separate from it.
            pointBorderColor: palette.surface,
            pointBorderWidth: 2,
            fill: series.length === 1,
            tension: 0
          };
        })
      },
      options: lineOptions(palette)
    };
  }, [labels, series, theme]);

  return (
    <div className="h-64">
      <Line data={data} options={options} />
    </div>
  );
}

function lineOptions(palette: ReturnType<typeof readChartPalette>): ChartOptions<'line'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    // No animation. These charts redraw whenever the live stream says the store changed, and a line
    // that redraws itself from the left every second turns a dashboard into a fidget. It also means
    // that what is on screen is the data, never a frame on the way to it.
    animation: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      // The legend is drawn in HTML above the chart, where a screen reader can read it.
      legend: { display: false },
      tooltip: {
        backgroundColor: palette.surface,
        titleColor: palette.text,
        bodyColor: palette.text,
        borderColor: palette.grid,
        borderWidth: 1,
        padding: 10,
        callbacks: {
          label: (item: TooltipItem<'line'>) => {
            const value = item.parsed.y;

            return value === null
              ? `${item.dataset.label}: not recorded`
              : `${item.dataset.label}: ${Math.round(value * 100)}%`;
          }
        }
      }
    },
    scales: {
      x: {
        border: { display: false },
        grid: { display: false },
        ticks: { color: palette.mutedText, maxRotation: 0, autoSkipPadding: 16 }
      },
      y: {
        // Fixed, never fitted. See the remark above.
        min: 0,
        max: 1,
        border: { display: false },
        grid: { color: palette.grid, drawTicks: false },
        ticks: {
          color: palette.mutedText,
          padding: 8,
          stepSize: 0.25,
          callback: (value) => `${Math.round(Number(value) * 100)}%`
        }
      }
    }
  };
}
