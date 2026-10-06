import { createContext, useContext } from 'react';
import type { OrganizationView } from './platformApi.ts';
import type { ControlRoomPlace } from './controlRoomRoute.ts';
import type { WorkspaceTree } from './workspaceTree.ts';

/** What every part of the control room needs to know about where it is. */
export type ControlRoom = {
  /** The organisation the sidebar shows. */
  readonly organization: OrganizationView;
  /** Its folders and projects as a tree, unfiltered. */
  readonly tree: WorkspaceTree;
  /** Where the address bar points. */
  readonly place: ControlRoomPlace;
  /** The fragment that links to a place. */
  readonly linkTo: (place: ControlRoomPlace) => string;
  /** Reads the workspace again after a change, so the sidebar shows what the server now holds. */
  readonly refresh: () => void;
  /** Opens an organisation in the sidebar, leaving any open project, and reads the workspace again. */
  readonly choose: (organizationId: string) => void;
};

const Context = createContext<ControlRoom | undefined>(undefined);

/** Provides the control room to everything inside it. */
export const ControlRoomProvider = Context.Provider;

/**
 * The control room the component sits in.
 *
 * @throws {Error} It is used outside one — a mistake in the page's structure, not in the data.
 */
export function useControlRoom(): ControlRoom {
  const room = useContext(Context);

  if (room === undefined) {
    throw new Error('A control-room component was rendered outside the control room.');
  }

  return room;
}
