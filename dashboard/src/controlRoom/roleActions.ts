/**
 * Which buttons a role is shown.
 *
 * @remarks
 * **This decides nothing.** The platform asks `roleCan` on every request and refuses what a role may
 * not do; these only spare somebody a button that would be refused. They mirror
 * `platform/src/auth/permissions.ts` rather than importing it, because that module is server code
 * and the browser bundle must not reach into it. Should the two drift, the cost is a button that
 * answers with the server's refusal, never an action the server would not allow.
 *
 * A role this does not know is shown nothing, as the server grants it nothing.
 */

// Engineer and above organise projects into folders (`project:describe`).
const Organizers: ReadonlySet<string> = new Set(['engineer', 'supervisor', 'admin']);

// Only an admin links a station (`station:link`): it decides who sees a machine's history.
const StationLinkers: ReadonlySet<string> = new Set(['admin']);

/**
 * Whether a membership may create, rename, move and delete folders, and file projects.
 *
 * @param role The role as the membership stores it, several separated by commas.
 */
export function canOrganize(role: string): boolean {
  return anyRoleIn(role, Organizers);
}

/**
 * Whether a membership may link a station to the organisation.
 *
 * @param role The role as the membership stores it, several separated by commas.
 */
export function canLinkStations(role: string): boolean {
  return anyRoleIn(role, StationLinkers);
}

function anyRoleIn(role: string, allowed: ReadonlySet<string>): boolean {
  return role.split(',').some((name) => allowed.has(name.trim()));
}
