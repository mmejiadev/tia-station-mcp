import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { ActionFailure } from './ActionFailure.tsx';
import { useControlRoom } from './ControlRoomContext.tsx';
import { FolderSelect } from './FolderSelect.tsx';
import { fileProject, type ProjectView } from './platformApi.ts';
import { canOrganize } from './roleActions.ts';
import { useAction } from './useAction.ts';
import { folderChoices } from './workspaceTree.ts';

/**
 * Which folder a project is filed in, and — for a role that may — moving it.
 *
 * @remarks
 * The folders offered are those of the organisation the sidebar shows, so they are only offered
 * when the project is one of its projects; otherwise they would be another organisation's, which
 * the server refuses. The permission is the project's own role, from its page.
 *
 * The folder shown is the sidebar's, read with the workspace, not the one the page was read with:
 * after a move or a folder deleted, the page has not been read again and the sidebar has.
 */
export function ProjectFiling({ project }: { readonly project: ProjectView }): ReactNode {
  const { organization, tree } = useControlRoom();
  const summary = organization.projects.find((candidate) => candidate.id === project.id);
  const choices = folderChoices(tree);

  if (summary === undefined) {
    return <p className="text-muted-foreground text-sm">This project is not in the sidebar yet. Reload the page to file it.</p>;
  }

  if (!canOrganize(project.role)) {
    return <p className="text-sm">{choices.find((choice) => choice.id === summary.folderId)?.name ?? 'Top level'}</p>;
  }

  // Keyed by where the project is now, so the choice starts again whenever that changes — a folder
  // deleted under it must not stay selected as a folder that no longer exists.
  return <FilingForm key={`${project.id}:${summary.folderId ?? 'top'}`} projectId={project.id} current={summary.folderId} />;
}

function FilingForm({ projectId, current }: { readonly projectId: number; readonly current: number | null }): ReactNode {
  const { tree, refresh } = useControlRoom();
  const [folderId, setFolderId] = useState<number | null>(current);
  const action = useAction();

  const file = async (): Promise<void> => {
    if (await action.run(() => fileProject(projectId, folderId))) {
      refresh();
    }
  };

  return (
    <div className="space-y-2">
      <FolderSelect label="Folder" choices={folderChoices(tree)} value={folderId} onChange={setFolderId} />
      <Button size="sm" variant="outline" disabled={action.pending || folderId === current} onClick={() => void file()}>
        Move here
      </Button>
      <ActionFailure action={action} />
    </div>
  );
}
