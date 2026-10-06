import type { OrganizationView } from '../../../platform/src/workspace/workspaceRead.ts';
import type { ControlRoomPlace } from './controlRoomRoute.ts';

/**
 * Which organisation the sidebar shows.
 *
 * @param organizations Those the person belongs to, in the workspace's order.
 * @param place Where the address bar points.
 * @param chosenId The one the person picked last, if any.
 * @returns The organisation, or undefined when they belong to none.
 * @remarks
 * **An open project's organisation wins.** A link to a project somebody sent opens with that
 * project's folders beside it, not with whichever organisation was picked last; otherwise the
 * sidebar and the page would describe two different places. Picking an organisation therefore also
 * leaves the project (see the sidebar).
 */
export function chooseOrganization(
  organizations: readonly OrganizationView[],
  place: ControlRoomPlace,
  chosenId: string | undefined
): OrganizationView | undefined {
  const holdingProject =
    place.kind === 'project' ? organizations.find((organization) => organization.projects.some((project) => project.id === place.projectId)) : undefined;

  return holdingProject ?? organizations.find((organization) => organization.id === chosenId) ?? organizations[0];
}
