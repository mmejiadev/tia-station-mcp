/**
 * The whole database schema, split by responsibility:
 *
 * - `schema/history.ts` — what the MCP server recorded, imported from its files and never edited.
 * - `schema/auth.ts` — Better Auth's people, sessions and organisations.
 * - `schema/people.ts` — profiles, and the link from a TIA author to a person.
 */
export * from './schema/auth.ts';
export * from './schema/history.ts';
export * from './schema/people.ts';
