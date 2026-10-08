import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { ActionFailure } from './ActionFailure.tsx';
import { useControlRoom } from './ControlRoomContext.tsx';
import { FolderSelect } from './FolderSelect.tsx';
import { NameForm } from './NameForm.tsx';
import { createFolder, deleteFolder, updateFolder, type FolderView } from './platformApi.ts';
import { useAction } from './useAction.ts';
import { folderChoices } from './workspaceTree.ts';

/** What is being done to a folder, if anything. */
export type FolderEdit = 'subfolder' | 'rename' | 'move' | 'delete';

type Properties = {
  readonly folder: FolderView;
  readonly edit: FolderEdit;
  readonly onDone: () => void;
};

/**
 * The form for one change to a folder, shown in place under it.
 *
 * @remarks
 * A record of forms rather than a switch: a fifth kind of edit added to the type without its form
 * does not compile.
 */
export function FolderEditor({ folder, edit, onDone }: Properties): ReactNode {
  const Form = Forms[edit];

  return <Form folder={folder} onDone={onDone} />;
}

type FormProperties = { readonly folder: FolderView; readonly onDone: () => void };

const Forms: Readonly<Record<FolderEdit, (properties: FormProperties) => ReactNode>> = {
  subfolder: SubfolderForm,
  rename: RenameForm,
  move: MoveForm,
  delete: DeleteForm
};

function SubfolderForm({ folder, onDone }: FormProperties): ReactNode {
  const { refresh } = useControlRoom();
  const action = useAction();

  const create = async (name: string): Promise<boolean> => {
    const created = await action.run(() => createFolder({ organizationId: folder.organizationId, parentId: folder.id, name }));
    return created ? finish(refresh, onDone) : false;
  };

  return <NameForm label={`New folder inside ${folder.name}`} initial="" submitLabel="Create" action={action} onSubmit={create} onCancel={onDone} />;
}

function RenameForm({ folder, onDone }: FormProperties): ReactNode {
  const { refresh } = useControlRoom();
  const action = useAction();

  const rename = async (name: string): Promise<boolean> => {
    const renamed = await action.run(() => updateFolder(folder.id, { name }));
    return renamed ? finish(refresh, onDone) : false;
  };

  return <NameForm label={`New name for ${folder.name}`} initial={folder.name} submitLabel="Rename" action={action} onSubmit={rename} onCancel={onDone} />;
}

/** Moves a folder under another. The folder itself and everything inside it are not offered. */
function MoveForm({ folder, onDone }: FormProperties): ReactNode {
  const { tree, refresh } = useControlRoom();
  const action = useAction();
  const [parentId, setParentId] = useState<number | null>(folder.parentId);

  const move = async (): Promise<void> => {
    if (await action.run(() => updateFolder(folder.id, { parentId }))) {
      finish(refresh, onDone);
    }
  };

  return (
    <div className="space-y-1.5 py-1">
      <FolderSelect label={`Move ${folder.name} into`} choices={folderChoices(tree, folder.id)} value={parentId} onChange={setParentId} />
      <EditButtons confirmLabel="Move" pending={action.pending} onConfirm={move} onCancel={onDone} />
      <ActionFailure action={action} />
    </div>
  );
}

/** Deleting asks once more in place — no browser dialog — and says what happens to the projects. */
function DeleteForm({ folder, onDone }: FormProperties): ReactNode {
  const { refresh } = useControlRoom();
  const action = useAction();

  const remove = async (): Promise<void> => {
    if (await action.run(() => deleteFolder(folder.id))) {
      finish(refresh, onDone);
    }
  };

  return (
    <div className="space-y-1.5 py-1 text-xs">
      <p>
        Delete <strong>{folder.name}</strong> and the folders inside it? Their projects are not deleted: they move to the top level.
      </p>
      <EditButtons confirmLabel="Delete" pending={action.pending} onConfirm={remove} onCancel={onDone} destructive />
      <ActionFailure action={action} />
    </div>
  );
}

type ButtonsProperties = {
  readonly confirmLabel: string;
  readonly pending: boolean;
  readonly onConfirm: () => Promise<void>;
  readonly onCancel: () => void;
  readonly destructive?: boolean;
};

function EditButtons({ confirmLabel, pending, onConfirm, onCancel, destructive = false }: ButtonsProperties): ReactNode {
  return (
    <div className="flex gap-1.5">
      <Button size="xs" variant={destructive ? 'destructive' : 'default'} disabled={pending} onClick={() => void onConfirm()}>
        {confirmLabel}
      </Button>
      <Button size="xs" variant="ghost" onClick={onCancel}>
        Cancel
      </Button>
    </div>
  );
}

function finish(refresh: () => void, onDone: () => void): true {
  refresh();
  onDone();
  return true;
}
