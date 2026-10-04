import { createAccessControl } from 'better-auth/plugins/access';
import { defaultStatements } from 'better-auth/plugins/organization/access';

/**
 * What can be done in an organisation, and by which role.
 *
 * @remarks
 * One table for both readers: Better Auth enforces it on its own endpoints — inviting, removing a
 * member — and the platform's endpoints ask the same roles through {@link roleCan}. Two tables
 * would drift, and the drift would be a permission one of them grants and the other was meant to
 * refuse.
 *
 * Better Auth's statements, plus three resources of the platform's own: a project, which is read
 * or described (its description, its folder); a TIA identity, which only a supervisor or an admin
 * confirms; and a station, which only an admin links to the organisation — the act that decides who
 * sees a machine's history. Nothing here
 * reaches TIA Portal or a controller — the web never writes to either (docs/WEB-PLATFORM.md).
 */
export const statements = {
  ...defaultStatements,
  project: ['read', 'describe'],
  identity: ['confirm'],
  station: ['link']
} as const;

export const accessControl = createAccessControl(statements);

/** Reads projects and their history. */
const viewer = accessControl.newRole({
  project: ['read']
});

/** Also writes descriptions and organises projects into folders. */
const engineer = accessControl.newRole({
  project: ['read', 'describe']
});

/** Also confirms who a TIA author is, and invites people. */
const supervisor = accessControl.newRole({
  project: ['read', 'describe'],
  identity: ['confirm'],
  invitation: ['create', 'cancel']
});

/** Everything, the organisation itself included. Whoever creates an organisation is its admin. */
const admin = accessControl.newRole({
  project: ['read', 'describe'],
  identity: ['confirm'],
  station: ['link'],
  organization: ['update', 'delete'],
  member: ['create', 'update', 'delete'],
  invitation: ['create', 'cancel'],
  team: ['create', 'update', 'delete'],
  ac: ['create', 'read', 'update', 'delete']
});

/** The roles, by the name stored in a membership. */
export const roles = { viewer, engineer, supervisor, admin } as const;

/** A role's name. */
export type RoleName = keyof typeof roles;

/** The role whoever creates an organisation gets. */
export const CreatorRole: RoleName = 'admin';

/** The role a person gets when nothing else says which. */
export const DefaultRole: RoleName = 'viewer';

/** What the platform's own endpoints ask a role for. */
export type PlatformPermission = {
  readonly project?: readonly ('read' | 'describe')[];
  readonly identity?: readonly 'confirm'[];
  readonly station?: readonly 'link'[];
};

/**
 * Whether a membership's role allows something.
 *
 * @param roleName The role as the membership stores it — text, because a database can hold anything.
 * @param permission What is being asked for.
 * @returns True only when the role is one of the four and grants every part of the request.
 * @remarks
 * **A role this does not know grants nothing.** A role renamed, mistyped or added to the database by
 * hand is a refusal, never a permission — the rule the governance layer follows, for the same reason.
 * A membership may hold several roles separated by commas, as Better Auth allows; the request is
 * granted when any one of them grants all of it.
 */
export function roleCan(roleName: string, permission: PlatformPermission): boolean {
  return roleName
    .split(',')
    .map((name) => name.trim())
    .some((name) => isKnownRole(name) && roles[name].authorize(toRequest(permission)).success);
}

function isKnownRole(name: string): name is RoleName {
  return Object.prototype.hasOwnProperty.call(roles, name);
}

function toRequest(permission: PlatformPermission): {
  project?: ('read' | 'describe')[];
  identity?: 'confirm'[];
  station?: 'link'[];
} {
  return {
    ...(permission.project === undefined ? {} : { project: [...permission.project] }),
    ...(permission.identity === undefined ? {} : { identity: [...permission.identity] }),
    ...(permission.station === undefined ? {} : { station: [...permission.station] })
  };
}
