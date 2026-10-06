import type { ReactNode } from 'react';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { useLoaded } from '../useLoaded.ts';
import { useControlRoom } from './ControlRoomContext.tsx';
import { ProjectSections, type ProjectSection } from './controlRoomRoute.ts';
import { readProject, type ProjectView } from './platformApi.ts';
import { ProjectChanges } from './ProjectChanges.tsx';
import { ProjectCompilations } from './ProjectCompilations.tsx';
import { ProjectSummary } from './ProjectSummary.tsx';

/** Each tab's title and what it shows; a record, so a section without a tab does not compile. */
const Sections: Readonly<Record<ProjectSection, { readonly title: string; readonly Body: (properties: { project: ProjectView }) => ReactNode }>> = {
  summary: { title: 'Summary', Body: ProjectSummary },
  changes: { title: 'Changes', Body: ({ project }) => <ProjectChanges projectId={project.id} /> },
  compilations: { title: 'Compilations', Body: ({ project }) => <ProjectCompilations projectId={project.id} total={project.compilationCount} /> }
};

type Properties = { readonly projectId: number; readonly section: ProjectSection };

/**
 * One project: what TIA Portal says about it, and the tabs.
 *
 * @remarks
 * A project the person may not read and one that does not exist are the same answer from the
 * server, on purpose, and the page shows that answer as it came.
 */
export function ProjectPage({ projectId, section }: Properties): ReactNode {
  const loaded = useLoaded(() => readProject(projectId), [projectId]);

  return (
    <WhenLoaded loaded={loaded}>
      {(project) => (
        <article className="space-y-6">
          <ProjectHeader project={project} />
          <SectionTabs project={project} open={section} />
          <SectionBody project={project} section={section} />
        </article>
      )}
    </WhenLoaded>
  );
}

function ProjectHeader({ project }: { readonly project: ProjectView }): ReactNode {
  return (
    <header className="space-y-2">
      <p className="text-primary font-mono text-xs tracking-widest uppercase">Project · station {project.stationName}</p>
      <h2 className="text-3xl font-semibold tracking-tight">{project.name}</h2>
      <p className="text-muted-foreground font-mono text-xs break-all">{project.tiaPath}</p>
    </header>
  );
}

function SectionTabs({ project, open }: { readonly project: ProjectView; readonly open: ProjectSection }): ReactNode {
  const { linkTo } = useControlRoom();
  const counts: Readonly<Record<ProjectSection, number | undefined>> = {
    summary: undefined,
    changes: Object.values(project.changesByOutcome).reduce((total, count) => total + count, 0),
    compilations: project.compilationCount
  };

  return (
    <nav aria-label="Project sections" className="bg-muted inline-flex flex-wrap gap-1 rounded-lg p-1">
      {ProjectSections.map((section) => (
        <a
          key={section}
          href={linkTo({ kind: 'project', projectId: project.id, section })}
          aria-current={section === open ? 'page' : undefined}
          className={`rounded-md px-3 py-1.5 text-sm ${section === open ? 'bg-background font-medium shadow-xs' : 'text-muted-foreground hover:text-foreground'}`}
        >
          {Sections[section].title}
          {counts[section] === undefined ? '' : <span className="tabular ml-1.5 font-mono text-[11px]">{counts[section]}</span>}
        </a>
      ))}
    </nav>
  );
}

function SectionBody({ project, section }: { readonly project: ProjectView; readonly section: ProjectSection }): ReactNode {
  const { Body } = Sections[section];

  return <Body project={project} />;
}
