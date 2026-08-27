import { CircleDashed, CircleCheck, CircleX } from 'lucide-react';
import type { ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import type { IterationPhase } from '../../../harness/src/metricsReader.ts';
import { readIterationPhases, readRun, readRuns, type RunDetailResponse, type RunsResponse } from '../api.ts';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { formatDuration, formatInstant, formatSpan } from '../format.ts';
import { useLive } from '../live.tsx';
import { useLoaded } from '../useLoaded.ts';

/** The five phases in the order the loop runs them, so a run in progress reads as a sequence. */
const LoopPhases: readonly string[] = ['generate', 'write', 'compile', 'download', 'verify'];

/**
 * What the loop is doing right now.
 *
 * @remarks
 * This is the half of the roadmap's plant copilot that recorded data can answer: which run, which
 * specification, which attempt, and what it has got through. The chat half was cut on 2026-08-27 —
 * it needs a model, and this repository has already paid five times for code that was written and
 * never run.
 *
 * **It never claims to know which phase is running.** A phase is written to the store when it
 * *ends*, in a finally block, which is what makes a phase that threw get recorded at all. So the
 * one in flight is knowable only as "started, not finished" — and that is what this says. Inventing
 * a current phase from the elapsed time would be a guess dressed as a measurement, on the one screen
 * somebody watches to know whether a controller is being written to.
 */
export function LiveRunView(): ReactNode {
  const { revision, connected } = useLive();
  const runs = useLoaded(readRuns, [revision]);

  return (
    <div className="space-y-6">
      <WhenLoaded loaded={runs}>
        {(loaded) => <CurrentRun runs={loaded} revision={revision} connected={connected} />}
      </WhenLoaded>
    </div>
  );
}

function CurrentRun({
  runs,
  revision,
  connected
}: {
  runs: RunsResponse;
  revision: number;
  connected: boolean;
}): ReactNode {
  // Newest first, so the first run without an outcome is the one in progress. A run interrupted
  // long ago also has no outcome, which is why the panel shows when it started and lets the reader
  // judge rather than announcing "running" on the strength of a missing field.
  const running = runs.runs.find((run) => run.outcome === undefined);
  const latest = runs.runs[0];

  if (running === undefined) {
    return (
      <Panel
        title="Nothing is running"
        explanation="No recorded run is still open. When one starts, this page follows it on its own — the API streams a notice whenever the store changes and this view re-reads it."
      >
        {latest === undefined ? (
          <p className="text-muted-foreground text-sm">Nothing has ever been recorded here.</p>
        ) : (
          <p className="text-sm">
            The most recent was <strong>run {latest.runId}</strong>, started {formatInstant(latest.startedAt)}, and it{' '}
            {latest.outcome === 'passed' ? 'passed' : `ended: ${latest.outcome ?? 'never finished'}`} after{' '}
            {formatSpan(latest.startedAt, latest.endedAt)}.
          </p>
        )}
        {connected ? undefined : (
          <p className="text-muted-foreground mt-3 text-xs">
            The live stream is not connected, so this will not update on its own. Start the API with{' '}
            <code>npm run api</code>.
          </p>
        )}
      </Panel>
    );
  }

  return <OpenRun runId={running.runId} startedAt={running.startedAt} revision={revision} />;
}

/** One run that has not ended, with whichever attempt it is on. */
function OpenRun({
  runId,
  startedAt,
  revision
}: {
  runId: number;
  startedAt: number;
  revision: number;
}): ReactNode {
  const detail = useLoaded(() => readRun(runId), [runId, revision]);

  return (
    <WhenLoaded loaded={detail}>
      {(loaded) => <OpenRunPanels detail={loaded} startedAt={startedAt} revision={revision} />}
    </WhenLoaded>
  );
}

function OpenRunPanels({
  detail,
  startedAt,
  revision
}: {
  detail: RunDetailResponse;
  startedAt: number;
  revision: number;
}): ReactNode {
  const iterations = detail.iterations;
  const current = [...iterations].reverse().find((iteration) => iteration.outcome === undefined);
  const done = iterations.filter((iteration) => iteration.outcome !== undefined).length;

  return (
    <>
      <Panel
        title={`Run ${detail.run.runId} is open`}
        explanation="A run with no recorded outcome. It is either in progress or it was interrupted — the store cannot tell those apart, so neither does this."
        aside={<Badge variant="outline">{done} iteration(s) finished</Badge>}
      >
        <dl className="grid gap-x-8 gap-y-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <Fact label="Started" value={formatInstant(startedAt)} />
          <Fact label="Specification set" value={detail.run.specSet} />
          <Fact label="Generator" value={detail.run.generator} />
          <Fact
            label="Attempt limit"
            value={`${detail.run.iterationLimit} per specification`}
          />
        </dl>
      </Panel>

      {current === undefined ? (
        <Panel
          title="Between attempts"
          explanation="Every iteration this run has started has an outcome, so nothing is part-way through. The run is either about to begin its next specification or it stopped here."
        >
          <p className="text-muted-foreground text-sm">Nothing is part-way through.</p>
        </Panel>
      ) : (
        <Panel
          title={`Attempt ${current.attempt} at ${current.specification}`}
          explanation="What this attempt has finished. A phase is recorded when it ends, so whatever is running right now is deliberately not shown as a phase — it is shown as time that has not been accounted for yet."
          aside={<Badge variant="outline">started {formatInstant(current.startedAt)}</Badge>}
        >
          <IterationProgress iterationId={current.iterationId} revision={revision} />
        </Panel>
      )}
    </>
  );
}

/** The phases of the attempt in flight, and the honest gap at the end of them. */
function IterationProgress({ iterationId, revision }: { iterationId: number; revision: number }): ReactNode {
  const loaded = useLoaded(() => readIterationPhases(iterationId), [iterationId, revision]);

  return (
    <WhenLoaded loaded={loaded}>
      {(answer) => {
        // Keyed by plain string, because the names below are the loop's order written out here and
        // the store is free to record a phase this list has never heard of. One it does not know
        // shows as "not reported yet" rather than making the page fail to render.
        const finished = new Map<string, IterationPhase>(answer.phases.map((phase) => [phase.phase, phase]));
        const accounted = answer.phases.reduce((total, phase) => total + phase.durationMilliseconds, 0);

        return (
          <>
            <ol className="space-y-2">
              {LoopPhases.map((name) => (
                <PhaseRow key={name} name={name} phase={finished.get(name)} />
              ))}
            </ol>

            <p className="text-muted-foreground mt-4 text-xs">
              {formatDuration(accounted)} accounted for across {answer.phases.length} finished phase(s). Whatever
              is running now is not in that figure, and will not be until it ends.
            </p>
          </>
        );
      }}
    </WhenLoaded>
  );
}

/**
 * One phase: finished with its duration, failed, or not yet reported.
 *
 * @remarks
 * Three states and three words. "Not reported yet" covers both the phase that is running and the
 * phases that have not begun, because from the store's side those are the same thing — and saying so
 * is better than a spinner that implies this one is next when the attempt may already have failed.
 */
function PhaseRow({ name, phase }: { name: string; phase: IterationPhase | undefined }): ReactNode {
  if (phase === undefined) {
    return (
      <li className="text-muted-foreground flex items-center gap-3 text-sm">
        <CircleDashed className="size-4 shrink-0" aria-hidden="true" />
        <span className="w-24 font-medium">{name}</span>
        <span className="text-xs">not reported yet</span>
      </li>
    );
  }

  const failed = phase.outcome === 'failed';
  const Icon = failed ? CircleX : CircleCheck;

  return (
    <li className="flex items-center gap-3 text-sm">
      <Icon
        className={`size-4 shrink-0 ${failed ? 'text-[var(--status-critical)]' : 'text-[var(--status-good)]'}`}
        aria-hidden="true"
      />
      <span className="w-24 font-medium">{name}</span>
      <span className="tabular">{formatDuration(phase.durationMilliseconds)}</span>
      {failed ? <span className="text-[var(--status-critical)] text-xs">and it threw</span> : undefined}
    </li>
  );
}

function Fact({ label, value }: { label: string; value: string }): ReactNode {
  return (
    <div>
      <dt className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{label}</dt>
      <dd className="mt-1 break-words">{value}</dd>
    </div>
  );
}
