import type { ReactNode } from 'react';
import type { PhaseDuration } from '../../../harness/src/metricsReader.ts';
import { formatDuration } from '../format.ts';
import { BarChart } from './BarChart.tsx';

/**
 * How long each phase of the loop takes, longest first.
 *
 * @remarks
 * One series, so the colour job is magnitude: a single hue, more-is-darker. Categorical colours here
 * would say the five phases are five different kinds of thing, when they are five measurements of
 * the same kind and the only question is which is biggest.
 *
 * The sample count under the chart is not decoration. Thirteen seconds over ninety downloads and
 * thirteen seconds over two are different claims, and the bar looks identical either way.
 */
export function PhaseDurationChart({ phases }: { readonly phases: readonly PhaseDuration[] }): ReactNode {
  if (phases.length === 0) {
    return <p className="text-muted-foreground text-sm">No phase has been timed here.</p>;
  }

  const ordered = [...phases].sort((a, b) => b.meanMilliseconds - a.meanMilliseconds);

  return (
    <>
      <BarChart
        categories={ordered.map((phase) => phase.phase)}
        series={[
          { label: 'Mean duration', values: ordered.map((phase) => phase.meanMilliseconds), colour: 'magnitude' }
        ]}
        format={formatDuration}
        unit=""
      />

      <ul className="text-muted-foreground mt-3 flex flex-wrap gap-x-6 gap-y-1 text-xs">
        {ordered.map((phase) => (
          <li key={phase.phase} className="tabular">
            <span className="text-foreground font-medium">{phase.phase}</span> {formatDuration(phase.meanMilliseconds)}{' '}
            over {phase.samples} sample(s)
            {phase.failures > 0 ? `, ${phase.failures} of which threw` : ''}
          </li>
        ))}
      </ul>
    </>
  );
}
