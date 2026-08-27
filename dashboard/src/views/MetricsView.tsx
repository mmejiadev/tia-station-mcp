import { useMemo, type ReactNode } from 'react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { SpecificationStatistics } from '../../../harness/src/metricsReader.ts';
import { readMetrics, type MetricsResponse } from '../api.ts';
import { BarChart } from '../charts/BarChart.tsx';
import { readChartPalette, seriesColour } from '../charts/palette.ts';
import { ChartLegend } from '../components/ChartLegend.tsx';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { formatMean, formatRate } from '../format.ts';
import { useLive } from '../live.tsx';
import { useTheme } from '../theme.tsx';
import { useLoaded } from '../useLoaded.ts';

/**
 * What each specification cost, in detail.
 *
 * @remarks
 * The numbers phase 3 exists to produce, per specification. Every rate here is a count first and a
 * percentage second, and every mean carries what it was averaged over — the roadmap's rule, and the
 * one thing this view must not get wrong, because a dashboard is where a number stops being
 * questioned.
 *
 * Phase durations are deliberately not here. They are on the overview, and per run inside a run;
 * this view is about specifications. A second copy of that chart drawn from the same endpoint would
 * be a distraction pretending to be detail.
 */
export function MetricsView(): ReactNode {
  const { revision } = useLive();
  const metrics = useLoaded(readMetrics, [revision]);

  return (
    <div className="space-y-6">
      <WhenLoaded loaded={metrics}>{(loaded) => <Outcomes metrics={loaded} />}</WhenLoaded>
      <WhenLoaded loaded={metrics}>{(loaded) => <Detail metrics={loaded} />}</WhenLoaded>
    </div>
  );
}

/**
 * How every attempt at each specification ended.
 *
 * @remarks
 * Stacked, because the three outcomes add up to the attempts — this is a part-to-whole question, and
 * three bars side by side would invite reading them as three independent measurements of the same
 * specification. Horizontal, because the categories are specification names and vertical columns
 * would have to rotate them.
 *
 * The order of the segments is the order of the pipeline: what got all the way through, what got
 * partway, what did not start. Sorting the segments by size instead would put a different story in
 * each row.
 */
function Outcomes({ metrics }: { metrics: MetricsResponse }): ReactNode {
  const { theme } = useTheme();
  // Re-read when the theme changes: the same slots resolve to different steps under `.dark`.
  const palette = useMemo(readChartPalette, [theme]);
  const specifications = [...metrics.specifications].sort((a, b) => passRate(b) - passRate(a));

  return (
    <Panel
      title="How every attempt at each specification ended"
      explanation="One bar per specification, one segment per fate, adding up to the number of runs that attempted it. A specification that compiles but does not pass is a program the compiler accepted and the cell did not: that is the interesting middle segment."
      aside={
        <span className="text-muted-foreground text-xs">
          n = {metrics.sampleSize.specificationAttempts} attempts across {metrics.sampleSize.runs} runs
        </span>
      }
    >
      <ChartLegend
        entries={[
          {
            label: 'Passed',
            colour: seriesColour(palette, 0),
            meaning: 'ran on a simulated CPU and behaved as specified'
          },
          {
            label: 'Compiled, did not pass',
            colour: seriesColour(palette, 1),
            meaning: 'the compiler accepted it; the cell did not'
          },
          {
            label: 'Never compiled',
            colour: seriesColour(palette, 2),
            meaning: 'no attempt of that run reached a clean compilation'
          }
        ]}
      />

      <BarChart
        stacked
        categories={specifications.map((entry) => entry.specification)}
        series={[
          { label: 'Passed', values: specifications.map((entry) => entry.passed), colour: 'identity' },
          {
            label: 'Compiled, did not pass',
            values: specifications.map((entry) => entry.cleanCompilations - entry.passed),
            colour: 'identity'
          },
          {
            label: 'Never compiled',
            values: specifications.map((entry) => entry.attempts - entry.cleanCompilations),
            colour: 'identity'
          }
        ]}
        format={(value) => String(Math.round(value))}
        unit="attempt(s)"
      />
    </Panel>
  );
}

/** The same thing as a table, which is what somebody checking a number actually wants. */
function Detail({ metrics }: { metrics: MetricsResponse }): ReactNode {
  return (
    <Panel
      title="The same numbers, exactly"
      explanation="Every rate as a count and then a percentage, never a percentage alone: 83% hides whether it was five of six or fifty of sixty. The mean counts only the attempts that reached a clean compilation, and says how many those were."
    >
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Specification</TableHead>
            <TableHead>Attempts</TableHead>
            <TableHead>Compiled</TableHead>
            <TableHead>Passed on a simulated CPU</TableHead>
            <TableHead>Iterations to a clean compilation</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {metrics.specifications.map((entry) => (
            <TableRow key={entry.specification}>
              <TableCell className="font-medium">{entry.specification}</TableCell>
              <TableCell className="tabular">{entry.attempts}</TableCell>
              <TableCell className="tabular">{formatRate(entry.cleanCompilations, entry.attempts)}</TableCell>
              <TableCell className="tabular">{formatRate(entry.passed, entry.attempts)}</TableCell>
              <TableCell className="tabular">
                {formatMean(entry.meanIterationsToCleanCompilation, entry.cleanCompilations)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Panel>
  );
}

function passRate(entry: SpecificationStatistics): number {
  return entry.attempts === 0 ? 0 : entry.passed / entry.attempts;
}
