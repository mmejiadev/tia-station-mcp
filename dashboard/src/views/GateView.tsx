import { CheckCircle2, XCircle } from 'lucide-react';
import type { ReactNode } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Progress } from '@/components/ui/progress';
import type { Criterion, GateVerdict } from '../../../harness/src/gate.ts';
import { readGate } from '../api.ts';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { useLive } from '../live.tsx';
import { useLoaded } from '../useLoaded.ts';

/**
 * The five criteria, and whether the door is open.
 *
 * @remarks
 * The verdict is computed on the server, by the same function `npm run gate` uses, from recorded
 * data. Nothing here recomputes it or interprets it: a browser that decided for itself which
 * criteria matter would be a second gate, and a gate that can be argued past is a gate in name only.
 *
 * Every criterion is shown, met or not, with the numbers behind it — including the ones that pass.
 * The point of the gate is to say what is missing, not merely to refuse.
 *
 * Each verdict carries an icon and the words MET or NOT MET as well as its colour. Nothing that
 * matters is said by colour alone, and on this page nothing does not matter.
 */
export function GateView(): ReactNode {
  const { revision } = useLive();
  const gate = useLoaded(readGate, [revision]);

  return (
    <div className="space-y-6">
      <WhenLoaded loaded={gate}>
        {(verdict) => (
          <>
            <Verdict verdict={verdict} />

            <Panel
              title="The five criteria"
              explanation="All five must be met. There is no majority, no weighting and no override — and a met criterion is a measurement, not a permission."
              aside={
                <span className="text-muted-foreground text-xs">
                  {verdict.criteria.filter((criterion) => criterion.met).length} of {verdict.criteria.length} met
                </span>
              }
            >
              <ol className="space-y-3">
                {verdict.criteria.map((criterion) => (
                  <CriterionRow key={criterion.number} criterion={criterion} />
                ))}
              </ol>
            </Panel>

            <Alert>
              <AlertTitle>What no measurement here can establish</AlertTitle>
              <AlertDescription>
                Workshop Mode also requires a teacher or workshop supervisor physically present at the cell,
                with access to the emergency stop. No software enforces that and none can, which is exactly
                why it is written down first.
              </AlertDescription>
            </Alert>
          </>
        )}
      </WhenLoaded>
    </div>
  );
}

/** The answer itself, said in a sentence before any of the detail. */
function Verdict({ verdict }: { verdict: GateVerdict }): ReactNode {
  const met = verdict.criteria.filter((criterion) => criterion.met).length;

  return (
    <Panel
      title="Workshop gate"
      explanation="Whether the recorded evidence allows Workshop Mode to be considered at all. The same verdict npm run gate prints in a terminal, from the same function over the same data."
    >
      <p
        className={`text-2xl leading-snug font-semibold ${
          verdict.open ? 'text-[var(--status-good)]' : 'text-[var(--status-critical)]'
        }`}
      >
        {verdict.open
          ? 'All five criteria are met. The decision is now a human one.'
          : 'The gate is shut. Workshop Mode stays unreachable in the default build.'}
      </p>

      <div className="mt-5 space-y-2">
        <Progress value={(met / verdict.criteria.length) * 100} />
        <p className="text-muted-foreground text-xs">
          {met} of {verdict.criteria.length} criteria met. The bar is progress towards being allowed to ask
          the question, not towards being allowed to run the cell.
        </p>
      </div>
    </Panel>
  );
}

/** One criterion: its verdict in a word, an icon and a colour, and the numbers behind it. */
function CriterionRow({ criterion }: { criterion: Criterion }): ReactNode {
  const Icon = criterion.met ? CheckCircle2 : XCircle;
  const colour = criterion.met ? 'text-[var(--status-good)]' : 'text-[var(--status-critical)]';

  return (
    <li className="flex gap-3 rounded-md border p-3">
      <Icon className={`mt-0.5 size-5 shrink-0 ${colour}`} aria-hidden="true" />
      <div className="space-y-1">
        <p className="text-sm font-medium">
          <span className={`mr-2 text-xs tracking-wide uppercase ${colour}`}>
            {criterion.met ? 'Met' : 'Not met'}
          </span>
          {criterion.number}. {criterion.name}
        </p>
        <p className="text-muted-foreground text-xs leading-relaxed">{criterion.evidence}</p>
      </div>
    </li>
  );
}
