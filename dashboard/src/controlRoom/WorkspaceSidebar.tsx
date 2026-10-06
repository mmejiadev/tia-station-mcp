import { FolderPlus, Search } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useControlRoom } from './ControlRoomContext.tsx';
import { FolderBranch } from './FolderBranch.tsx';
import { NameForm } from './NameForm.tsx';
import { createFolder, type OrganizationView } from './platformApi.ts';
import { ProjectLink } from './ProjectLink.tsx';
import { canOrganize } from './roleActions.ts';
import { useAction } from './useAction.ts';
import { buildTree } from './workspaceTree.ts';

type Properties = { readonly organizations: readonly OrganizationView[] };

/**
 * The left of the control room: which organisation, a search, and its folders with their projects.
 *
 * @remarks
 * The search filters what is already loaded rather than asking the server: an organisation's
 * projects are all in the workspace answer, and a filter that waits for a round trip per key is one
 * nobody uses.
 */
export function WorkspaceSidebar({ organizations }: Properties): ReactNode {
  const { organization, tree: wholeTree } = useControlRoom();
  const [search, setSearch] = useState('');
  const tree = search.trim().length === 0 ? wholeTree : buildTree(organization, search);
  const isEmpty = tree.roots.length === 0 && tree.unfiled.length === 0;

  return (
    <nav aria-label="Projects" className="space-y-4">
      <OrganizationChooser organizations={organizations} />

      <label className="relative block">
        <span className="sr-only">Search projects</span>
        <Search className="text-muted-foreground absolute top-1/2 left-2.5 size-4 -translate-y-1/2" aria-hidden="true" />
        <Input className="pl-8" type="search" placeholder="Project or station" value={search} onChange={(event) => setSearch(event.target.value)} />
      </label>

      <TopLevelHeader />

      <ul className="space-y-0.5">
        {tree.roots.map((node) => (
          <FolderBranch key={node.folder.id} node={node} />
        ))}
        {tree.unfiled.map((project) => (
          <li key={project.id}>
            <ProjectLink project={project} />
          </li>
        ))}
      </ul>

      {isEmpty ? <p className="text-muted-foreground px-2 text-xs">{search.length > 0 ? 'Nothing matches.' : 'No projects yet.'}</p> : undefined}
    </nav>
  );
}

function OrganizationChooser({ organizations }: Properties): ReactNode {
  const { organization, choose } = useControlRoom();

  return (
    <label className="block space-y-1">
      <span className="text-muted-foreground block font-mono text-[11px] tracking-widest uppercase">Organisation</span>
      <select
        className="border-input bg-background h-9 w-full rounded-md border px-2 text-sm font-medium"
        value={organization.id}
        onChange={(event) => choose(event.target.value)}
      >
        {organizations.map((choice) => (
          <option key={choice.id} value={choice.id}>
            {choice.name} · {choice.role}
          </option>
        ))}
      </select>
    </label>
  );
}

/** "Folders", and the button that creates one at the top level. */
function TopLevelHeader(): ReactNode {
  const { organization, refresh } = useControlRoom();
  const [isCreating, setCreating] = useState(false);
  const action = useAction();

  const create = async (name: string): Promise<boolean> => {
    const created = await action.run(() => createFolder({ organizationId: organization.id, parentId: null, name }));

    if (created) {
      refresh();
      setCreating(false);
    }

    return created;
  };

  return (
    <div>
      <div className="flex items-center justify-between px-1">
        <span className="text-muted-foreground font-mono text-[11px] tracking-widest uppercase">Folders</span>
        {canOrganize(organization.role) ? (
          <Button variant="ghost" size="icon-xs" aria-label="New folder" title="New folder" onClick={() => setCreating(true)}>
            <FolderPlus aria-hidden="true" />
          </Button>
        ) : undefined}
      </div>
      {isCreating ? <NameForm label="New folder name" initial="" submitLabel="Create" action={action} onSubmit={create} onCancel={() => setCreating(false)} /> : undefined}
    </div>
  );
}
