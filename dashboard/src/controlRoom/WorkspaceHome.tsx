import type { ReactNode } from 'react';
import { Panel } from '../components/Panel.tsx';
import { useControlRoom } from './ControlRoomContext.tsx';
import { CreateOrganization } from './CreateOrganization.tsx';
import { PairStation } from './PairStation.tsx';
import { StatusLooks } from './projectStatus.ts';
import { canLinkStations } from './roleActions.ts';
import { StatusLed } from './StatusLed.tsx';

/**
 * The control room with no project open: how the organisation's projects stand, and linking a
 * station for whoever may.
 */
export function WorkspaceHome(): ReactNode {
  const { organization, choose } = useControlRoom();
  const mayLink = canLinkStations(organization.role);

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <p className="text-primary font-mono text-xs tracking-widest uppercase">Organisation · your role: {organization.role}</p>
        <h2 className="text-3xl font-semibold tracking-tight">{organization.name}</h2>
      </header>

      <StatusCounts />

      {mayLink ? (
        <Panel title="Link a station" explanation="A station's projects become visible to this organisation once it is linked. Only an admin links, with a code only the station itself can print.">
          <PairStation />
        </Panel>
      ) : organization.projects.length === 0 ? (
        <p className="text-muted-foreground text-sm">No station is linked to this organisation yet. An admin links one with the code the station prints.</p>
      ) : undefined}

      <Panel title="Another organisation" explanation="A class, a workshop or a company. Whoever creates one is its admin, and it sees nothing until a station is linked to it.">
        <CreateOrganization onCreated={choose} />
      </Panel>
    </div>
  );
}

/** How many projects stand at each status, every status listed even at zero. */
function StatusCounts(): ReactNode {
  const { organization } = useControlRoom();
  const statuses = Object.keys(StatusLooks) as (keyof typeof StatusLooks)[];

  return (
    <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {statuses.map((status) => (
        <div key={status} className="bg-card rounded-lg border p-4" title={StatusLooks[status].meaning}>
          <dt>
            <StatusLed status={status} className="text-muted-foreground gap-2 font-sans tracking-widest" />
          </dt>
          <dd className="tabular mt-1 font-mono text-3xl font-semibold">{organization.projects.filter((project) => project.status === status).length}</dd>
        </div>
      ))}
    </dl>
  );
}
