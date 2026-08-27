import { useMemo, type ReactNode } from 'react';
import type { MetricsResponse, RunsResponse } from '../api.ts';
import { readMetrics, readRuns } from '../api.ts';
import { PhaseDurationChart } from '../charts/PhaseDurationChart.tsx';
import { readChartPalette, seriesColour } from '../charts/palette.ts';
import { RateLine } from '../charts/RateLine.tsx';
import { ChartLegend } from '../components/ChartLegend.tsx';
import { Panel } from '../components/Panel.tsx';
import { StatTile } from '../components/StatTile.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { formatDuration, formatRate } from '../format.ts';
import { useLive } from '../live.tsx';
import { useTheme } from '../theme.tsx';
import { useLoaded } from '../useLoaded.ts';

/** How many runs the trend looks back over. The same window criterion 4 of the gate judges. */
const TrendWindow = 20;

/**
 * What the whole thing has been doing, in one screen.
 *
 * @remarks
 * It exists because the roadmap's "metrics and charts" view was doing two jobs: answering *how is it
 * going* at a glance, and answering *what exactly did each specification cost*. Those want different
 * forms — headline figures against a table of detail — and putting them on one page meant neither
 * was quite readable. This one answers the first question and nothing else.
 */
export function OverviewView(): ReactNode {
  const { revision } = useLive();
  const metrics = useLoaded(readMetrics, [revision]);
  const runs = useLoaded(readRuns, [revision]);

  return (
    <div className="space-y-6">
      <WhenLoaded loaded={metrics}>
        {(loaded) => (
          <WhenLoaded loaded={runs}>{(recorded) => <Headlines metrics={loaded} runs={recorded} />}</WhenLoaded>
        )}
      </WhenLoaded>

      <WhenLoaded loaded={runs}>{(recorded) => <Trend runs={recorded} />}</WhenLoaded>

      <WhenLoaded loaded={metrics}>{(loaded) => <WhereTheTimeGoes metrics={loaded} />}</WhenLoaded>
    </div>
  );
}

/** The four figures somebody wants before they want anything else. */
function Headlines({ metrics, runs }: { metrics: MetricsResponse; runs: RunsResponse }): ReactNode {
  const attempts = metrics.sampleSize.specificationAttempts;
  const passed = metrics.specifications.reduce((total, entry) => total + entry.passed, 0);
  const compiled = metrics.specifications.reduce((total, entry) => total + entry.cleanCompilations, 0);
  const finished = runs.runs.filter((run) => run.outcome !== undefined).length;
  const loop = metrics.phases.reduce((total, phase) => total + phase.meanMilliseconds, 0);

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatTile
        label="Runs recorded"
        value={String(runs.runs.length)}
        basis={`${finished} of them finished; the rest were interrupted or are still going`}
      />
      <StatTile
        label="Compiled cleanly"
        value={formatRate(compiled, attempts)}
        basis="specification attempts whose SCL the compiler accepted"
        tone={compiled === attempts ? 'good' : undefined}
      />
      <StatTile
        label="Passed on a simulated CPU"
        value={formatRate(passed, attempts)}
        basis="compiled, downloaded, reached RUN, and behaved as the specification says"
      />
      <StatTile
        label="One iteration costs"
        value={formatDuration(loop)}
        basis={`the five phases added up, each a mean over its own sample`}
      />
    </div>
  );
}

/**
 * The clean-compilation rate over the recent runs.
 *
 * @remarks
 * This is the picture behind criterion 4 of the workshop gate, which asks whether the rate is
 * *falling*. The gate compares two halves of this window arithmetically; this draws the same window
 * so the verdict can be looked at rather than only read.
 */
function Trend({ runs }: { runs: RunsResponse }): ReactNode {
  const { theme } = useTheme();
  const palette = useMemo(readChartPalette, [theme]);
  // Newest first from the API, so the window is the head of the list, reversed to read left to right.
  const window = [...runs.runs].slice(0, TrendWindow).reverse();
  const attempted = window.filter((run) => run.specifications > 0);

  return (
    <Panel
      title={`How the last ${TrendWindow} runs went`}
      explanation="Each point is one run. Compiling cleanly is pinned at the top and has been for the whole window — that flat line is what criterion 4 of the workshop gate is looking at, and it is the finding rather than an empty chart. What varies is the line below it: compiling is not the hard part, behaving as specified on a simulated CPU is."
      aside={<span className="text-muted-foreground text-xs">n = {attempted.length} runs that attempted anything</span>}
    >
      {attempted.length === 0 ? (
        <p className="text-muted-foreground text-sm">No run in this window attempted a specification.</p>
      ) : (
        <>
          <ChartLegend
            entries={[
              {
                label: 'Compiled cleanly',
                colour: seriesColour(palette, 0),
                meaning: 'of the specifications the run attempted'
              },
              {
                label: 'Passed on a simulated CPU',
                colour: seriesColour(palette, 1),
                meaning: 'downloaded, reached RUN, and behaved as specified'
              }
            ]}
          />

          <RateLine
            labels={attempted.map((run) => `Run ${run.runId}`)}
            series={[
              {
                label: 'Compiled cleanly',
                rates: attempted.map((run) => run.cleanCompilations / run.specifications)
              },
              {
                label: 'Passed on a simulated CPU',
                rates: attempted.map((run) => run.passed / run.specifications)
              }
            ]}
          />
        </>
      )}
    </Panel>
  );
}


/** Where the minute of an iteration actually goes. */
function WhereTheTimeGoes({ metrics }: { metrics: MetricsResponse }): ReactNode {
  const slowest = [...metrics.phases].sort((a, b) => b.meanMilliseconds - a.meanMilliseconds)[0];

  return (
    <Panel
      title="Where an iteration spends its time"
      explanation={
        slowest === undefined
          ? 'No phase has been timed yet.'
          : `Mean duration of each phase of the loop. The ${slowest.phase} phase dominates, which is why the loop is ordered so that an attempt which does not compile never reaches it.`
      }
    >
      <PhaseDurationChart phases={metrics.phases} />
    </Panel>
  );
}
