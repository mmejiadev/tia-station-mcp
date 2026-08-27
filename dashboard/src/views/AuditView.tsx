import { AlertTriangle } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { AuditEntry } from '../../../harness/src/auditTrail.ts';
import { readAudit, type AuditResponse } from '../api.ts';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { formatRecordedInstant } from '../format.ts';
import { useLive } from '../live.tsx';
import { useLoaded } from '../useLoaded.ts';

/** The filters the API understands. Naming them here keeps the form from asking for one it does not. */
const Filters = ['mode', 'tool', 'outcome', 'target'] as const;

type Filter = (typeof Filters)[number];

type Query = Record<Filter, string>;

const NoFilters: Query = { mode: '', tool: '', outcome: '', target: '' };

/** What each filter is for, said next to it rather than left to be guessed. */
const FilterHelp: Readonly<Record<Filter, string>> = {
  mode: 'Study or Workshop',
  tool: 'exact name, e.g. WriteScl',
  outcome: 'Planned, Applied, Refused, Failed',
  target: 'any part of the path'
};

/**
 * Everything the server changed, or refused to.
 *
 * @remarks
 * The filtering and the truncation both happen in the API rather than in the browser. The trail of a
 * machine that has been running the loop is thousands of lines, and a view that downloaded all of it
 * and built a row per line would be slow in exactly the situation somebody is in when they open it.
 *
 * Unreadable lines are reported above the table, never dropped. A corrupt trail that renders as a
 * clean one is the single worst thing this view could do — the same failure criterion 2 of the
 * workshop gate exists to catch.
 */
export function AuditView(): ReactNode {
  const { revision } = useLive();
  const [query, setQuery] = useState<Query>(NoFilters);
  const loaded = useLoaded(() => readAudit(query), [query.mode, query.tool, query.outcome, query.target, revision]);
  const filtered = Filters.some((filter) => query[filter].length > 0);

  return (
    <Panel
      title="Audit trail"
      explanation="One line per planned change and one per what became of it. Every write the MCP server makes passes through the guard and lands here first, whether it was applied, refused or failed."
      aside={
        <form className="flex flex-wrap items-end gap-3" onSubmit={(event) => event.preventDefault()}>
          {Filters.map((filter) => (
            <label key={filter} className="space-y-1">
              <span className="text-muted-foreground block text-xs font-medium">{filter}</span>
              <Input
                className="h-8 w-40"
                placeholder={FilterHelp[filter]}
                value={query[filter]}
                onChange={(event) => setQuery({ ...query, [filter]: event.target.value })}
              />
            </label>
          ))}
          <Button variant="outline" size="sm" disabled={!filtered} onClick={() => setQuery(NoFilters)}>
            Clear
          </Button>
        </form>
      }
    >
      <WhenLoaded loaded={loaded}>{(audit) => <Trail audit={audit} />}</WhenLoaded>
    </Panel>
  );
}

function Trail({ audit }: { audit: AuditResponse }): ReactNode {
  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-x-6 gap-y-2">
        <p className="text-muted-foreground text-xs">
          Showing the most recent {audit.entries.length} of {audit.matched} matching entr(ies), out of{' '}
          {audit.total} recorded.
          {audit.matched > audit.entries.length
            ? ' There is more behind this — narrow the filters to see the rest.'
            : ''}
        </p>
        <OutcomeCounts entries={audit.entries} />
      </div>

      {audit.unreadableLines.length === 0 ? undefined : (
        <Alert variant="destructive" role="alert" className="mb-4">
          <AlertTriangle aria-hidden="true" />
          <AlertTitle>
            {audit.unreadableLines.length} line(s) of the trail could not be read, the first at line{' '}
            {audit.unreadableLines[0]}.
          </AlertTitle>
          <AlertDescription>
            They are not in the table below, and a trail with holes in it cannot be treated as complete.
          </AlertDescription>
        </Alert>
      )}

      {/*
        The log scrolls inside its own box rather than stretching the page. Two hundred rows made the
        page eight thousand pixels tall, so the filters — the thing you came here to use — scrolled off
        the top the moment you started reading.
      */}
      <div className="max-h-[34rem] overflow-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>When</TableHead>
              <TableHead>Plan</TableHead>
              <TableHead>Mode</TableHead>
              <TableHead>Tool</TableHead>
              <TableHead>Target</TableHead>
              <TableHead>Outcome</TableHead>
              <TableHead>Backup</TableHead>
              <TableHead>Detail</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {audit.entries.map((entry, index) => (
              <TableRow key={`${entry.planId}-${entry.outcome}-${index}`}>
                <TableCell className="tabular whitespace-nowrap">{formatRecordedInstant(entry.timestamp)}</TableCell>
                <TableCell className="tabular">{entry.planId}</TableCell>
                <TableCell>{entry.mode}</TableCell>
                <TableCell className="font-medium">{entry.tool}</TableCell>
                <TableCell>{entry.target}</TableCell>
                <TableCell>
                  <AuditOutcomeBadge outcome={entry.outcome} />
                </TableCell>
                <TableCell className="max-w-64 truncate font-mono text-xs" title={entry.backupPath}>
                  {entry.backupPath === '' ? '—' : entry.backupPath}
                </TableCell>
                <TableCell className="text-muted-foreground text-xs">{entry.detail}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </>
  );
}

/**
 * How the shown entries ended, counted.
 *
 * @remarks
 * Counted from what is on screen rather than from the whole trail, and it says so, because counting
 * one and labelling it the other is how a summary comes to disagree with the table under it.
 *
 * Grouped rather than summed against a fixed list of outcomes: an outcome nobody has heard of is
 * counted here as itself, which is the same reason the store's own summary groups instead of adding
 * up four named cases.
 */
function OutcomeCounts({ entries }: { entries: readonly AuditEntry[] }): ReactNode {
  const counts = new Map<string, number>();

  for (const entry of entries) {
    counts.set(entry.outcome, (counts.get(entry.outcome) ?? 0) + 1);
  }

  return (
    <ul className="flex flex-wrap items-center gap-2">
      {[...counts].map(([outcome, count]) => (
        <li key={outcome}>
          <AuditOutcomeBadge outcome={outcome} count={count} />
        </li>
      ))}
    </ul>
  );
}

/**
 * One audit outcome.
 *
 * @remarks
 * Refused is deliberately not painted as a failure. A refusal is the governance layer working, and
 * colouring it red would teach whoever reads this page to see the guard doing its job as something
 * going wrong.
 */
function AuditOutcomeBadge({ outcome, count }: { outcome: string; count?: number }): ReactNode {
  const tone =
    outcome === 'Applied'
      ? 'border-[var(--status-good)] text-[var(--status-good)]'
      : outcome === 'Failed'
        ? 'border-[var(--status-critical)] text-[var(--status-critical)]'
        : outcome === 'Refused'
          ? 'border-[var(--status-warning)] text-[var(--status-warning)]'
          : '';

  return (
    <Badge variant="outline" className={`tabular ${tone}`}>
      {outcome}
      {count === undefined ? '' : ` ${count}`}
    </Badge>
  );
}
