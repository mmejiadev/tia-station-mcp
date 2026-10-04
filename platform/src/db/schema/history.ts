import { bigint, index, integer, pgTable, text, timestamp, unique, type AnyPgColumn } from 'drizzle-orm/pg-core';
import { organization } from './auth.ts';
import { folder } from './workspace.ts';

/**
 * A machine that runs TIA Portal and the MCP server, and whose files are imported.
 *
 * @remarks
 * Identified by name for now. Phase 3 of docs/WEB-PLATFORM.md gives each one a token, so that a
 * station can only push its own history; until then the importer runs on the machine itself.
 *
 * A station belongs to one organisation, and that is what decides who sees its projects: the
 * members of that organisation, as their role allows. A station nobody has linked yet is seen by
 * nobody — its history is imported, and stays out of sight until an admin says whose it is.
 *
 * **Linking takes a pairing code issued on the station's own machine** (`npm run pair`). Any
 * signed-in person can create an organisation and be its admin, so being an admin proves nothing
 * about a station; holding a code that only its machine could print does. Without it, the first
 * admin to type a station's name would read its whole history.
 */
export const station = pgTable('station', {
  id: integer('id').primaryKey().generatedAlwaysAsIdentity(),
  name: text('name').notNull().unique(),
  organizationId: text('organization_id').references(() => organization.id, { onDelete: 'set null' }),
  /**
   * The SHA-256 of the pairing code issued on the station's own machine, or null when none is
   * pending. Never the code itself: the database is not where it should be readable from.
   */
  pairingCodeHash: text('pairing_code_hash'),
  pairingCodeExpiresAt: timestamp('pairing_code_expires_at', { withTimezone: true }),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow()
}, (table) => [index('station_organization').on(table.organizationId)]);

/**
 * One entry of a station's audit trail: a change the MCP server planned, applied, refused or failed.
 *
 * @remarks
 * Imported, never edited. The trail on disk is the record and this is an index of it, so every value
 * is kept as the server wrote it — the outcome and the mode as text rather than an enumeration, for
 * the reason the harness gives: an unrecognised value has to survive being read to be noticed.
 *
 * `entryKey` is what makes an import idempotent. A chained entry is keyed by its chain hash, which
 * the server computed over its values and position; an entry from before chaining existed has no
 * hash, and is keyed by a hash of its line instead.
 */
export const change = pgTable(
  'change',
  {
    id: bigint('id', { mode: 'number' }).primaryKey().generatedAlwaysAsIdentity(),
    stationId: integer('station_id')
      .notNull()
      .references(() => station.id),
    entryKey: text('entry_key').notNull(),
    lineNumber: integer('line_number').notNull(),
    /** The entry's position in the chain, or null for an entry from before chaining. */
    sequence: integer('sequence'),
    chainHash: text('chain_hash'),
    /** The timestamp as the server wrote it. */
    timestamp: text('timestamp').notNull(),
    /** The same moment, parsed; null when the text could not be read as one. */
    occurredAt: timestamp('occurred_at', { withTimezone: true }),
    planId: text('plan_id').notNull(),
    mode: text('mode').notNull(),
    tool: text('tool').notNull(),
    target: text('target').notNull(),
    value: text('value').notNull(),
    backupPath: text('backup_path').notNull(),
    origin: text('origin').notNull(),
    outcome: text('outcome').notNull(),
    detail: text('detail').notNull(),
    documentation: text('documentation').notNull(),
    /** The project the change was made in, as the trail recorded it; empty before version 3. */
    projectPath: text('project_path').notNull().default(''),
    /** That project's row, or null when the trail did not record one. Never inferred. */
    projectId: integer('project_id').references((): AnyPgColumn => project.id, { onDelete: 'set null' }),
    importedAt: timestamp('imported_at', { withTimezone: true }).notNull().defaultNow()
  },
  (table) => [
    unique('change_station_entry_key').on(table.stationId, table.entryKey),
    index('change_station_occurred_at').on(table.stationId, table.occurredAt),
    index('change_project_occurred_at').on(table.projectId, table.occurredAt)
  ]
);

/**
 * A TIA Portal project on a station, as the server last saw it.
 *
 * @remarks
 * One row per station and path, kept up to date from the project journal: the record with the
 * latest timestamp wins, whatever order the imports run in. A project seen only through a
 * compilation — opened before the server recorded projects — has a row with its path and nothing
 * TIA Portal said about it yet, so its compilations are not lost.
 *
 * The author is what TIA Portal recorded, usually a Windows account. Phase 3 links it to a person,
 * and only once somebody confirms the link (docs/WEB-PLATFORM.md).
 */
export const project = pgTable(
  'project',
  {
    id: integer('id').primaryKey().generatedAlwaysAsIdentity(),
    stationId: integer('station_id')
      .notNull()
      .references(() => station.id),
    tiaPath: text('tia_path').notNull(),
    /** The folder the project is filed in on the web, or null at the top level. */
    folderId: integer('folder_id').references((): AnyPgColumn => folder.id, { onDelete: 'set null' }),
    name: text('name').notNull(),
    tiaAuthor: text('tia_author'),
    tiaCreatedAt: timestamp('tia_created_at', { withTimezone: true }),
    tiaModifiedAt: timestamp('tia_modified_at', { withTimezone: true }),
    tiaModifiedBy: text('tia_modified_by'),
    /** When the server last started working on it, from the journal; null until it has. */
    lastSeenAt: timestamp('last_seen_at', { withTimezone: true }),
    createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow()
  },
  (table) => [unique('project_station_path').on(table.stationId, table.tiaPath), index('project_folder').on(table.folderId)]
);

/**
 * One compilation of a PLC program, from the compilation journal.
 *
 * @remarks
 * Keyed by a hash of its journal line, which makes the import idempotent: the journal has no chain,
 * and the line is the record.
 */
export const compilation = pgTable(
  'compilation',
  {
    id: bigint('id', { mode: 'number' }).primaryKey().generatedAlwaysAsIdentity(),
    stationId: integer('station_id')
      .notNull()
      .references(() => station.id),
    projectId: integer('project_id')
      .notNull()
      .references(() => project.id),
    entryKey: text('entry_key').notNull(),
    lineNumber: integer('line_number').notNull(),
    compiledAt: timestamp('compiled_at', { withTimezone: true }),
    softwarePath: text('software_path').notNull(),
    /** As the server wrote it: Success, Information, Warning or Error, kept as text. */
    severity: text('severity').notNull(),
    errorCount: integer('error_count').notNull(),
    warningCount: integer('warning_count').notNull(),
    importedAt: timestamp('imported_at', { withTimezone: true }).notNull().defaultNow()
  },
  (table) => [
    unique('compilation_station_entry_key').on(table.stationId, table.entryKey),
    index('compilation_project_compiled_at').on(table.projectId, table.compiledAt)
  ]
);

/** One message of a compilation, in the order the compiler produced them. */
export const compilationMessage = pgTable(
  'compilation_message',
  {
    id: bigint('id', { mode: 'number' }).primaryKey().generatedAlwaysAsIdentity(),
    compilationId: bigint('compilation_id', { mode: 'number' })
      .notNull()
      .references(() => compilation.id, { onDelete: 'cascade' }),
    position: integer('position').notNull(),
    severity: text('severity').notNull(),
    path: text('path').notNull(),
    description: text('description').notNull()
  },
  (table) => [unique('compilation_message_position').on(table.compilationId, table.position)]
);
