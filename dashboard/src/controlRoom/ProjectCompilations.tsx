import type { ReactNode } from 'react';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { formatRecordedInstant } from '../format.ts';
import { useLoaded } from '../useLoaded.ts';
import { describeShown } from './describeShown.ts';
import { readCompilations, type CompilationView } from './platformApi.ts';
import { StatusLed } from './StatusLed.tsx';
import { statusOfCounts } from './projectStatus.ts';

/**
 * A project's compilations, newest first, each with what TIA Portal said.
 *
 * @remarks
 * Each compilation opens to its messages with a native disclosure: it works from a keyboard and a
 * screen reader without anything written for it, and a compilation with a hundred messages does not
 * push the next one off the page until somebody asks to see them.
 */
export function ProjectCompilations({ projectId, total }: { readonly projectId: number; readonly total: number }): ReactNode {
  const loaded = useLoaded(() => readCompilations(projectId), [projectId]);

  return (
    <Panel
      title="Compilations"
      explanation="Each compilation made through the MCP server, with its error and warning counts and the messages TIA Portal gave. Compilations whose moment was not recorded are listed last."
    >
      <WhenLoaded loaded={loaded}>{(compilations) => <CompilationList compilations={compilations} total={total} />}</WhenLoaded>
    </Panel>
  );
}

function CompilationList({ compilations, total }: { readonly compilations: readonly CompilationView[]; readonly total: number }): ReactNode {
  if (compilations.length === 0) {
    return <p className="text-muted-foreground text-sm">This project has not been compiled through the MCP server.</p>;
  }

  const cut = describeShown(compilations.length, total, 'compilations');

  return (
    <div className="space-y-3">
      {cut.length === 0 ? undefined : <p className="text-muted-foreground text-xs">{cut}</p>}
      <ol className="space-y-2">
        {compilations.map((compilation) => (
          <li key={compilation.id}>
            <CompilationItem compilation={compilation} />
          </li>
        ))}
      </ol>
    </div>
  );
}

function CompilationItem({ compilation }: { readonly compilation: CompilationView }): ReactNode {
  return (
    <details className="group rounded-md border">
      <summary className="hover:bg-accent flex cursor-pointer flex-wrap items-center gap-x-4 gap-y-1 px-3 py-2 text-sm">
        <StatusLed status={statusOfCounts(compilation)} />
        <span className="tabular">{compilation.compiledAt === null ? 'Moment not recorded' : formatRecordedInstant(compilation.compiledAt)}</span>
        <span className="text-muted-foreground font-mono text-xs">{compilation.softwarePath}</span>
        <span className="tabular ml-auto font-mono text-xs">
          {compilation.errorCount} error(s) · {compilation.warningCount} warning(s) · {compilation.messages.length} message(s)
        </span>
      </summary>
      <Messages messages={compilation.messages} />
    </details>
  );
}

function Messages({ messages }: { readonly messages: CompilationView['messages'] }): ReactNode {
  if (messages.length === 0) {
    return <p className="text-muted-foreground border-t px-3 py-2 text-xs">TIA Portal gave no messages.</p>;
  }

  return (
    <ul className="max-h-80 divide-y overflow-auto border-t text-xs">
      {messages.map((message, index) => (
        <li key={index} className="grid gap-1 px-3 py-1.5 sm:grid-cols-[6rem_minmax(0,16rem)_1fr]">
          <span className="font-medium">{message.severity}</span>
          <span className="text-muted-foreground truncate font-mono" title={message.path}>
            {message.path}
          </span>
          <span>{message.description}</span>
        </li>
      ))}
    </ul>
  );
}
