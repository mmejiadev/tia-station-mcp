import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { OutcomeBadge } from '../components/OutcomeBadge.tsx';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { formatRecordedInstant } from '../format.ts';
import { useLoaded } from '../useLoaded.ts';
import { ActionFailure } from './ActionFailure.tsx';
import { ChangePageSize, readChanges, type ChangeView } from './platformApi.ts';
import { useAction } from './useAction.ts';

/** The outcomes the filter offers; empty is all of them. Any other the trail holds still shows under "All". */
const OutcomeFilters = ['', 'Planned', 'Applied', 'Refused', 'Failed'] as const;

/**
 * A project's change history, newest first, filtered by outcome and read a page at a time.
 *
 * @remarks
 * The list is keyed by the filter, so changing it starts from the newest page again instead of
 * appending one outcome's older pages to another's.
 */
export function ProjectChanges({ projectId }: { readonly projectId: number }): ReactNode {
  const [outcome, setOutcome] = useState('');

  return (
    <Panel
      title="Changes"
      explanation="Every change the MCP server planned in this project and what became of it, as its audit trail recorded it. Each write was preceded by a backup at the path shown."
      aside={
        <div role="group" aria-label="Filter by outcome" className="flex flex-wrap gap-1.5">
          {OutcomeFilters.map((filter) => (
            <Button key={filter} size="xs" variant={filter === outcome ? 'default' : 'outline'} aria-pressed={filter === outcome} onClick={() => setOutcome(filter)}>
              {filter === '' ? 'All' : filter}
            </Button>
          ))}
        </div>
      }
    >
      <ChangeList key={`${projectId}:${outcome}`} projectId={projectId} outcome={outcome} />
    </Panel>
  );
}

type ListProperties = { readonly projectId: number; readonly outcome: string };

function ChangeList({ projectId, outcome }: ListProperties): ReactNode {
  const first = useLoaded(() => readChanges(projectId, { outcome }), [projectId, outcome]);

  return <WhenLoaded loaded={first}>{(rows) => <ChangePages firstPage={rows} projectId={projectId} outcome={outcome} />}</WhenLoaded>;
}

/**
 * The first page and any older ones asked for.
 *
 * @remarks
 * A page shorter than the page size is the last one, so the button goes away rather than offering a
 * page that would come back empty.
 */
function ChangePages({ firstPage, projectId, outcome }: ListProperties & { readonly firstPage: readonly ChangeView[] }): ReactNode {
  const [older, setOlder] = useState<{ readonly rows: readonly ChangeView[]; readonly isLast: boolean }>({ rows: [], isLast: false });
  const action = useAction();
  const all = [...firstPage, ...older.rows];
  const oldest = all.at(-1);
  const mayHaveMore = oldest !== undefined && firstPage.length === ChangePageSize && !older.isLast;

  const loadOlder = (): void => {
    void action.run(async () => {
      const page = await readChanges(projectId, { outcome, before: oldest?.id ?? 0 });
      setOlder({ rows: [...older.rows, ...page], isLast: page.length < ChangePageSize });
    });
  };

  return (
    <div className="space-y-3">
      <ChangeTable rows={all} />
      {mayHaveMore ? (
        <Button size="sm" variant="outline" disabled={action.pending} onClick={loadOlder}>
          Load older changes
        </Button>
      ) : undefined}
      <ActionFailure action={action} />
    </div>
  );
}

function ChangeTable({ rows }: { readonly rows: readonly ChangeView[] }): ReactNode {
  if (rows.length === 0) {
    return <p className="text-muted-foreground text-sm">No change of this kind has been recorded in this project.</p>;
  }

  return (
    <div className="max-h-[34rem] overflow-auto rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>When</TableHead>
            <TableHead>Tool</TableHead>
            <TableHead>Target</TableHead>
            <TableHead>Outcome</TableHead>
            <TableHead>Mode</TableHead>
            <TableHead>Backup</TableHead>
            <TableHead>Detail</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((change) => (
            <TableRow key={change.id}>
              <TableCell className="tabular whitespace-nowrap">{change.occurredAt === null ? 'unknown' : formatRecordedInstant(change.occurredAt)}</TableCell>
              <TableCell className="font-medium">{change.tool}</TableCell>
              <TableCell className="font-mono text-xs">{change.target}</TableCell>
              <TableCell>
                <OutcomeBadge outcome={change.outcome} />
              </TableCell>
              <TableCell>{change.mode}</TableCell>
              <TableCell className="max-w-64 truncate font-mono text-xs" title={change.backupPath}>
                {change.backupPath === '' ? '—' : change.backupPath}
              </TableCell>
              <TableCell className="text-muted-foreground text-xs">{change.detail}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
