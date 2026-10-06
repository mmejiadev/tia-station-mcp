import { UserRound } from 'lucide-react';
import type { ReactNode } from 'react';
import { Panel } from '../components/Panel.tsx';
import { formatRecordedInstant } from '../format.ts';
import { ProjectFiling } from './ProjectFiling.tsx';
import type { ProjectView } from './platformApi.ts';
import { StatusLooks } from './projectStatus.ts';
import { StatusLed } from './StatusLed.tsx';

/**
 * A project's summary: how it stands, who made it, and what happened to it.
 *
 * @remarks
 * Only what the platform holds. The design also drew a rack, a description and an AI button; those
 * have no data behind them yet, and a page that drew them would be showing a project that does not
 * exist.
 */
export function ProjectSummary({ project }: { readonly project: ProjectView }): ReactNode {
  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <StatusTiles project={project} />
      <Panel title="Author" explanation="Who TIA Portal says made the project, and the person confirmed as that author in this organisation.">
        <Author project={project} />
      </Panel>
      <Panel title="Changes by outcome" explanation="Every change the MCP server planned in this project, by what became of it. Refused is the guard working, not a failure.">
        <Outcomes counts={project.changesByOutcome} />
      </Panel>
      <Panel title="Where it is filed" explanation="The folder this project sits in, in this organisation's sidebar. Moving it changes nothing in TIA Portal.">
        <ProjectFiling project={project} />
      </Panel>
    </div>
  );
}

function StatusTiles({ project }: { readonly project: ProjectView }): ReactNode {
  const look = StatusLooks[project.status];
  const tiles = [
    { label: 'Compilation', value: <StatusLed status={project.status} className="gap-2 text-lg tracking-normal normal-case" />, note: look.meaning },
    { label: 'Errors', value: project.errorCount, note: 'In the latest compilation.' },
    { label: 'Warnings', value: project.warningCount, note: 'In the latest compilation.' },
    { label: 'Last compiled', value: project.lastCompiledAt === null ? 'Never' : formatRecordedInstant(project.lastCompiledAt), note: `${project.compilationCount} compilation(s) recorded.` }
  ];

  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:col-span-3 lg:grid-cols-4">
      {tiles.map((tile) => (
        <div key={tile.label} className="bg-card rounded-lg border p-4" title={tile.note}>
          <p className="text-muted-foreground text-[11px] tracking-widest uppercase">{tile.label}</p>
          <p className="tabular mt-1 font-mono text-lg font-semibold">{tile.value}</p>
        </div>
      ))}
    </div>
  );
}

function Author({ project }: { readonly project: ProjectView }): ReactNode {
  return (
    <div className="space-y-2 text-sm">
      <p className="flex items-center gap-2">
        {project.author?.image ? (
          <img src={project.author.image} alt="" className="size-8 rounded-full" referrerPolicy="no-referrer" />
        ) : (
          <UserRound className="text-muted-foreground size-8" aria-hidden="true" />
        )}
        <span>
          <span className="block font-medium">{project.author?.name ?? 'Nobody confirmed yet'}</span>
          <span className="text-muted-foreground font-mono text-xs">TIA author: {project.tiaAuthor ?? 'not recorded'}</span>
        </span>
      </p>
      <p className="text-muted-foreground text-xs">
        Created {formatOptional(project.tiaCreatedAt)} · modified {formatOptional(project.tiaModifiedAt)}
      </p>
    </div>
  );
}

function Outcomes({ counts }: { readonly counts: Readonly<Record<string, number>> }): ReactNode {
  const entries = Object.entries(counts);

  if (entries.length === 0) {
    return <p className="text-muted-foreground text-sm">No change has been recorded in this project.</p>;
  }

  return (
    <dl className="grid grid-cols-2 gap-2 text-sm">
      {entries.map(([outcome, count]) => (
        <div key={outcome} className="flex justify-between gap-2 rounded-md border px-3 py-1.5">
          <dt>{outcome}</dt>
          <dd className="tabular font-mono">{count}</dd>
        </div>
      ))}
    </dl>
  );
}

function formatOptional(timestamp: string | null): string {
  return timestamp === null ? 'unknown' : formatRecordedInstant(timestamp);
}
