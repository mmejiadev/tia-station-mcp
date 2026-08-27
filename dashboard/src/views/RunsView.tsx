import { useState, type ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { RecordedIteration } from '../../../harness/src/metricsReader.ts';
import { readRun, readRuns, type RunDetailResponse, type RunsResponse } from '../api.ts';
import { PhaseDurationChart } from '../charts/PhaseDurationChart.tsx';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { formatInstant, formatRate, formatSpan } from '../format.ts';
import { useLive } from '../live.tsx';
import { useLoaded } from '../useLoaded.ts';

/**
 * The runs, and what happened inside one of them.
 *
 * @remarks
 * This is the half of the plant copilot that can be shown from recorded data: which run, which
 * specification, which attempt, and what each phase cost. The chat half needs the live loop and is
 * not here — the roadmap's own list of what gets cut first puts it first.
 *
 * A run in progress updates itself: the live stream says the store changed and the open run is
 * re-read. That is what makes this view worth having open while a run is going rather than after it.
 */
export function RunsView(): ReactNode {
  const { revision } = useLive();
  const [selected, setSelected] = useState<number | undefined>(undefined);
  const runs = useLoaded(readRuns, [revision]);

  return (
    <div className="space-y-6">
      <Panel
        title="Every run"
        explanation="One row per run of the specification set. 'Compiled' counts the specifications that reached a clean compilation at least once, out of those the run attempted."
      >
        <WhenLoaded loaded={runs}>
          {(loaded) => <RunTable runs={loaded} selected={selected} onSelect={setSelected} />}
        </WhenLoaded>
      </Panel>

      {selected === undefined ? undefined : <RunDetail runId={selected} revision={revision} />}
    </div>
  );
}

function RunTable({
  runs,
  selected,
  onSelect
}: {
  runs: RunsResponse;
  selected: number | undefined;
  onSelect: (runId: number) => void;
}): ReactNode {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Run</TableHead>
          <TableHead>Started</TableHead>
          <TableHead>Took</TableHead>
          <TableHead>Outcome</TableHead>
          <TableHead>Generator</TableHead>
          <TableHead>Compiled</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {runs.runs.map((run) => (
          <TableRow key={run.runId} data-state={run.runId === selected ? 'selected' : undefined}>
            <TableCell className="tabular font-medium">{run.runId}</TableCell>
            <TableCell className="tabular">{formatInstant(run.startedAt)}</TableCell>
            <TableCell className="tabular">{formatSpan(run.startedAt, run.endedAt)}</TableCell>
            <TableCell>
              <OutcomeBadge outcome={run.outcome} />
            </TableCell>
            <TableCell>{run.generator}</TableCell>
            <TableCell className="tabular">{formatRate(run.cleanCompilations, run.specifications)}</TableCell>
            <TableCell className="text-right">
              <Button variant="ghost" size="sm" onClick={() => onSelect(run.runId)}>
                Open
              </Button>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/** One run: every iteration it ran, and where its time went. */
function RunDetail({ runId, revision }: { runId: number; revision: number }): ReactNode {
  const loaded = useLoaded(() => readRun(runId), [runId, revision]);

  return (
    <WhenLoaded loaded={loaded}>{(detail) => <RunPanels detail={detail} />}</WhenLoaded>
  );
}

function RunPanels({ detail }: { detail: RunDetailResponse }): ReactNode {
  return (
    <>
      <Panel
        title={`Run ${detail.run.runId}, iteration by iteration`}
        explanation="Each attempt at each specification, in the order it ran. A specification with two rows is one the loop had to fix: the compiler's errors from the first attempt were the input to the second."
        aside={
          <span className="text-muted-foreground text-xs">
            limit {detail.run.iterationLimit} attempt(s) per specification
          </span>
        }
      >
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Specification</TableHead>
              <TableHead>Attempt</TableHead>
              <TableHead>Outcome</TableHead>
              <TableHead>Compiler errors</TableHead>
              <TableHead>Took</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {detail.iterations.map((iteration) => (
              <TableRow key={iteration.iterationId}>
                <TableCell className="font-medium">{iteration.specification}</TableCell>
                <TableCell className="tabular">{iteration.attempt}</TableCell>
                <TableCell>
                  <OutcomeBadge outcome={iteration.outcome} />
                </TableCell>
                <TableCell className="tabular">{describeErrors(iteration)}</TableCell>
                <TableCell className="tabular">{formatSpan(iteration.startedAt, iteration.endedAt)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Panel>

      <Panel
        title={`Where run ${detail.run.runId} spent its time`}
        explanation="The same phases as on the overview, but measured only inside this run — which is how a run that was unusually slow can be told apart from one that simply did more."
      >
        <PhaseDurationChart phases={detail.phases} />
      </Panel>
    </>
  );
}

/**
 * An outcome, in its own words and colour.
 *
 * @remarks
 * An outcome this dashboard does not recognise is shown as it was recorded rather than dropped or
 * relabelled. The store is allowed to grow a category the browser has not been taught yet; what it
 * is not allowed to do is have one disappear on the way to a screen.
 */
function OutcomeBadge({ outcome }: { outcome: string | undefined }): ReactNode {
  if (outcome === undefined) {
    return <Badge variant="outline">never finished</Badge>;
  }

  const good = outcome === 'passed';
  const bad = outcome === 'failed' || outcome === 'download-failed' || outcome === 'behaviour-failed';

  return (
    <Badge variant="outline" className={good ? 'border-[var(--status-good)] text-[var(--status-good)]' : bad ? 'border-[var(--status-critical)] text-[var(--status-critical)]' : ''}>
      {outcome}
    </Badge>
  );
}

/**
 * How many compiler errors an iteration produced.
 *
 * @remarks
 * An iteration that never finished has no count, and that is shown as such rather than as zero.
 * Zero errors is what a clean compilation looks like, and an interrupted iteration is not one.
 */
function describeErrors(iteration: RecordedIteration): string {
  return iteration.errorCount === undefined ? 'not recorded' : String(iteration.errorCount);
}
