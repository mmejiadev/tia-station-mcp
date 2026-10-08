import type { ReactNode } from 'react';
import type { ProjectSummaryView } from '../../../platform/src/workspace/workspaceRead.ts';
import { useControlRoom } from './ControlRoomContext.tsx';
import { StatusLed } from './StatusLed.tsx';

/** One project in the sidebar: its name, its station, and its LED. */
export function ProjectLink({ project }: { readonly project: ProjectSummaryView }): ReactNode {
  const { place, linkTo } = useControlRoom();
  const isOpen = place.kind === 'project' && place.projectId === project.id;

  return (
    <a
      href={linkTo({ kind: 'project', projectId: project.id, section: 'summary' })}
      aria-current={isOpen ? 'page' : undefined}
      className={`flex min-h-10 items-center justify-between gap-2 rounded-md px-2 py-1.5 text-sm transition-colors hover:bg-accent ${
        isOpen ? 'bg-accent font-medium shadow-[inset_2px_0_0_var(--primary)]' : ''
      }`}
    >
      <span className="min-w-0">
        <span className="block truncate">{project.name}</span>
        <span className="text-muted-foreground block truncate font-mono text-[11px]">{project.stationName}</span>
      </span>
      <StatusLed status={project.status} />
    </a>
  );
}
