import { ChevronDown, ChevronRight, Folder, FolderInput, FolderPlus, Pencil, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { useControlRoom } from './ControlRoomContext.tsx';
import { FolderEditor, type FolderEdit } from './FolderEditor.tsx';
import { ProjectLink } from './ProjectLink.tsx';
import { canOrganize } from './roleActions.ts';
import { projectCount, type FolderNode } from './workspaceTree.ts';

/** The buttons on a folder, in the order they are shown, with what each says to a screen reader. */
const EditButtons: readonly { readonly edit: FolderEdit; readonly icon: typeof Folder; readonly label: string }[] = [
  { edit: 'subfolder', icon: FolderPlus, label: 'New folder inside' },
  { edit: 'rename', icon: Pencil, label: 'Rename' },
  { edit: 'move', icon: FolderInput, label: 'Move' },
  { edit: 'delete', icon: Trash2, label: 'Delete' }
];

/**
 * One folder of the sidebar, with what is inside it.
 *
 * @remarks
 * Open by default: a sidebar whose projects are hidden until clicked is one that looks empty. The
 * editing buttons are only drawn for a role that may use them (see `roleActions.ts`).
 */
export function FolderBranch({ node }: { readonly node: FolderNode }): ReactNode {
  const [isOpen, setOpen] = useState(true);
  const [edit, setEdit] = useState<FolderEdit | undefined>(undefined);

  return (
    <li>
      <FolderHeader node={node} isOpen={isOpen} onToggle={() => setOpen(!isOpen)} onEdit={setEdit} />
      {edit === undefined ? undefined : (
        <div className="pl-6">
          <FolderEditor folder={node.folder} edit={edit} onDone={() => setEdit(undefined)} />
        </div>
      )}
      {isOpen ? (
        <ul className="border-border ml-3 border-l pl-2">
          {node.children.map((child) => (
            <FolderBranch key={child.folder.id} node={child} />
          ))}
          {node.projects.map((project) => (
            <li key={project.id}>
              <ProjectLink project={project} />
            </li>
          ))}
        </ul>
      ) : undefined}
    </li>
  );
}

type HeaderProperties = {
  readonly node: FolderNode;
  readonly isOpen: boolean;
  readonly onToggle: () => void;
  readonly onEdit: (edit: FolderEdit) => void;
};

function FolderHeader({ node, isOpen, onToggle, onEdit }: HeaderProperties): ReactNode {
  const { organization } = useControlRoom();
  const Chevron = isOpen ? ChevronDown : ChevronRight;

  return (
    <div className="group flex items-center gap-1">
      <button
        type="button"
        aria-expanded={isOpen}
        onClick={onToggle}
        className="hover:bg-accent flex min-h-8 min-w-0 flex-1 items-center gap-1.5 rounded-md px-1 text-left text-sm font-medium"
      >
        <Chevron className="text-muted-foreground size-3.5 shrink-0" aria-hidden="true" />
        <span className="truncate">{node.folder.name}</span>
        <span className="text-muted-foreground tabular font-mono text-[11px]">{projectCount(node)}</span>
      </button>
      {canOrganize(organization.role) ? (
        // Where there is a pointer that hovers, hidden until the folder is hovered or reached from
        // the keyboard: always drawn, the four buttons cut a name to "0965 Sistemes …" (seen
        // 2026-10-06). On a touch screen there is no hover and a tap does not focus a button, so
        // there they are always drawn — a cut name beats folders nobody can edit.
        <span className="flex [@media(hover:hover)]:hidden [@media(hover:hover)]:group-focus-within:flex [@media(hover:hover)]:group-hover:flex">
          {EditButtons.map(({ edit, icon: Icon, label }) => (
            <Button key={edit} variant="ghost" size="icon-xs" aria-label={`${label}: ${node.folder.name}`} title={label} onClick={() => onEdit(edit)}>
              <Icon aria-hidden="true" />
            </Button>
          ))}
        </span>
      ) : undefined}
    </div>
  );
}
