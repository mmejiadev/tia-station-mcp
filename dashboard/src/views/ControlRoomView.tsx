import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { authClient } from '../auth/authClient.ts';
import { Panel } from '../components/Panel.tsx';
import { WhenLoaded } from '../components/WhenLoaded.tsx';
import { chooseOrganization } from '../controlRoom/chooseOrganization.ts';
import { ControlRoomProvider, type ControlRoom } from '../controlRoom/ControlRoomContext.tsx';
import { hashForPlace, placeFromHash, type ControlRoomPlace } from '../controlRoom/controlRoomRoute.ts';
import { CreateOrganization } from '../controlRoom/CreateOrganization.tsx';
import { readWorkspace, type OrganizationView } from '../controlRoom/platformApi.ts';
import { ProjectPage } from '../controlRoom/ProjectPage.tsx';
import { useKeptWhileReloading } from '../controlRoom/useKeptWhileReloading.ts';
import { WorkspaceHome } from '../controlRoom/WorkspaceHome.tsx';
import { WorkspaceSidebar } from '../controlRoom/WorkspaceSidebar.tsx';
import { buildTree } from '../controlRoom/workspaceTree.ts';
import { useHash } from '../useHash.ts';
import { useLoaded } from '../useLoaded.ts';
import { hashFor } from '../viewRoute.ts';

/** This view's own fragment; everything below it is a place in the control room. */
const Base = hashFor('Control room');

function linkTo(place: ControlRoomPlace): string {
  return hashForPlace(place, Base);
}

/**
 * The web platform: the organisations a person belongs to, their projects, and each project's page.
 *
 * @remarks
 * Signed-in only — every endpoint behind it answers 401 otherwise — so a person signed out is told
 * to sign in rather than shown an error. The platform not running is said as such, as the account
 * menu says it.
 */
export function ControlRoomView(): ReactNode {
  const { data, isPending, error } = authClient.useSession();

  if (isPending) {
    return <p className="text-muted-foreground py-6 text-sm">Checking sign-in…</p>;
  }

  if (error !== null) {
    return (
      <Panel title="Control room" explanation="The control room is served by the web platform, which is not answering.">
        <p className="text-sm">Start it with <code className="bg-muted rounded px-1.5 py-0.5 font-mono text-xs">npm run serve</code> in <code className="bg-muted rounded px-1.5 py-0.5 font-mono text-xs">platform/</code>.</p>
      </Panel>
    );
  }

  if (data === null) {
    return (
      <Panel title="Control room" explanation="Each organisation's projects, folders and history. What you see depends on the organisations you belong to and your role in each.">
        <p className="text-sm">Sign in with Google or GitHub, at the top right, to open it.</p>
      </Panel>
    );
  }

  return <Workspace />;
}

function Workspace(): ReactNode {
  const [revision, setRevision] = useState(0);
  const loaded = useKeptWhileReloading(useLoaded(() => readWorkspace(), [revision]));
  const refresh = (): void => setRevision((current) => current + 1);

  return (
    <WhenLoaded loaded={loaded}>
      {({ organizations }) =>
        organizations.length === 0 ? <FirstOrganization onCreated={refresh} /> : <Room organizations={organizations} refresh={refresh} />
      }
    </WhenLoaded>
  );
}

function FirstOrganization({ onCreated }: { readonly onCreated: () => void }): ReactNode {
  return (
    <Panel
      title="Create your organisation"
      explanation="You do not belong to any organisation yet. Create one — a class, a workshop, a company — and you are its admin; then link a station to it to see its projects."
    >
      <CreateOrganization onCreated={() => onCreated()} />
    </Panel>
  );
}

function Room({ organizations, refresh }: { readonly organizations: readonly OrganizationView[]; readonly refresh: () => void }): ReactNode {
  const place = placeFromHash(useHash(), Base);
  const [chosenId, setChosenId] = useState<string | undefined>(undefined);
  const organization = chooseOrganization(organizations, place, chosenId);

  const openProjectId = place.kind === 'project' ? place.projectId : undefined;

  // Opening a project makes its organisation the choice, so leaving a project opened from a sent
  // link stays in that organisation instead of jumping back to the first one. Only when the open
  // project changes: picking another organisation sets the choice before the address bar has left
  // the project, and syncing on every render would put the old one back in between.
  useEffect(() => {
    if (openProjectId !== undefined && organization !== undefined) {
      setChosenId(organization.id);
    }
  }, [openProjectId]);

  const tree = useMemo(() => (organization === undefined ? undefined : buildTree(organization, '')), [organization]);

  // Unreachable — the room is only drawn for somebody in at least one organisation — and said so.
  if (organization === undefined || tree === undefined) {
    throw new Error('The control room was drawn for a person in no organisation.');
  }

  // Picking an organisation leaves the open project, which belongs to the one being left. It also
  // reads the workspace again, so one created a moment ago is there to be picked.
  const choose = (organizationId: string): void => {
    setChosenId(organizationId);
    window.location.hash = Base;
    refresh();
  };

  const room: ControlRoom = { organization, tree, place, linkTo, refresh, choose };

  return (
    <ControlRoomProvider value={room}>
      <div className="grid gap-8 lg:grid-cols-[17rem_minmax(0,1fr)]">
        <aside className="lg:border-r lg:pr-6">
          <WorkspaceSidebar organizations={organizations} />
        </aside>
        <main className="min-w-0">
          {place.kind === 'project' ? <ProjectPage projectId={place.projectId} section={place.section} /> : <WorkspaceHome />}
        </main>
      </div>
    </ControlRoomProvider>
  );
}
