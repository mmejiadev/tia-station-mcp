import { AlertTriangle, CheckCircle2, CircleAlert, XCircle } from 'lucide-react';
import type { ReactNode } from 'react';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import type { PreconditionCheck, PreconditionReport } from '../../../harness/src/preconditions.ts';
import type { GuideDocument } from '../../../harness/src/dashboardApi.ts';
import { readGuide, readPreconditions } from '../api.ts';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { useLoaded } from '../useLoaded.ts';

/**
 * How to install this, and what this machine actually has.
 *
 * @remarks
 * The instructions are `INSTALL.md` itself, fetched and rendered, not a copy of what it says. The
 * file is what somebody reads on GitHub before any of this is running, and this view is what they
 * keep open once it is; a hand-written second copy would drift, and the copy that drifts is the one
 * being shown to whoever is installing.
 *
 * The machine check is the one thing this view can do that the file cannot. It runs the same
 * PowerShell script the bootstrap runs and reports what came back — it installs nothing and changes
 * no setting, which is what keeps this API read-only in the sense that matters.
 *
 * It is deliberately not live-refreshed with the rest of the dashboard. Nothing about a machine's
 * installed software changes because a run finished, and a panel that re-ran a PowerShell process
 * every few seconds would be a cost with no answer attached.
 */
export function GuideView(): ReactNode {
  const preconditions = useLoaded(readPreconditions, []);
  const guide = useLoaded(readGuide, []);

  return (
    <div className="space-y-6">
      <WhenLoaded loaded={preconditions}>{(report) => <Machine report={report} />}</WhenLoaded>
      <WhenLoaded loaded={guide}>{(document) => <Guide document={document} />}</WhenLoaded>
    </div>
  );
}

/** What this machine meets, item by item. */
function Machine({ report }: { report: PreconditionReport }): ReactNode {
  if (!report.available) {
    return (
      <Panel
        title="This machine"
        explanation="The same check the bootstrap script runs, asked of this machine."
      >
        <Alert>
          <CircleAlert />
          <AlertTitle>Nothing was checked</AlertTitle>
          <AlertDescription>
            {report.reason} That is not the same as something being missing: it means the question was
            never answered, so nothing below should be read as a verdict on this machine.
          </AlertDescription>
        </Alert>
      </Panel>
    );
  }

  const blocking = report.checks.filter((check) => check.required && !check.met);

  return (
    <Panel
      title="This machine"
      explanation="The same check the bootstrap script runs. It reads the machine and changes nothing: no install, no group granted, no setting written."
      aside={
        <span className="text-muted-foreground text-xs">
          {report.checks.filter((check) => check.met).length} of {report.checks.length} met
        </span>
      }
    >
      <p
        className={`mb-4 text-lg font-semibold ${
          report.ready ? 'text-[var(--status-good)]' : 'text-[var(--status-critical)]'
        }`}
      >
        {report.ready
          ? 'This machine meets everything the server requires.'
          : `${blocking.length} requirement(s) not met. The server will not run until they are.`}
      </p>

      <ul className="space-y-3">
        {report.checks.map((check) => (
          <CheckRow key={check.name} check={check} />
        ))}
      </ul>
    </Panel>
  );
}

/**
 * One requirement.
 *
 * @remarks
 * Three states, not two, and the middle one carries its weight: something required and missing stops
 * the server, something optional and missing costs a feature. Colouring both red would tell somebody
 * their machine is broken because they have no PLCSIM licence.
 *
 * The state is said in words and by an icon as well as by colour. A red dot beside a line of text is
 * not a message to somebody who cannot see red.
 */
function CheckRow({ check }: { check: PreconditionCheck }): ReactNode {
  const state = check.met ? 'met' : check.required ? 'blocking' : 'optional';

  const icon = {
    met: <CheckCircle2 className="size-4 text-[var(--status-good)]" aria-hidden />,
    blocking: <XCircle className="size-4 text-[var(--status-critical)]" aria-hidden />,
    optional: <AlertTriangle className="size-4 text-[var(--status-warning)]" aria-hidden />
  }[state];

  const label = { met: 'MET', blocking: 'MISSING', optional: 'OPTIONAL, MISSING' }[state];

  return (
    <li className="border-border border-b pb-3 last:border-b-0">
      <div className="flex items-center gap-2">
        {icon}
        <span className="font-medium">{check.name}</span>
        <span className="text-muted-foreground text-xs tracking-wide">{label}</span>
      </div>

      {check.found.length > 0 ? (
        <p className="text-muted-foreground mt-1 ml-6 text-sm">{check.found}</p>
      ) : undefined}

      {check.met || check.fix.length === 0 ? undefined : (
        <p className="mt-2 ml-6 text-sm">{check.fix}</p>
      )}
    </li>
  );
}

/** The install guide, rendered from the file rather than restated. */
function Guide({ document }: { document: GuideDocument }): ReactNode {
  if (!document.available) {
    return (
      <Panel title="Installing" explanation="INSTALL.md, rendered from the file itself.">
        <Alert>
          <CircleAlert />
          <AlertTitle>The guide could not be read</AlertTitle>
          <AlertDescription>{document.reason}</AlertDescription>
        </Alert>
      </Panel>
    );
  }

  return (
    <Panel
      title="Installing"
      explanation="INSTALL.md itself, rendered here rather than restated. Editing the file changes this page."
    >
      <div className="prose-guide">
        <Markdown remarkPlugins={[remarkGfm]}>{document.markdown}</Markdown>
      </div>
    </Panel>
  );
}
