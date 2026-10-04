import { bigint, index, integer, pgTable, text, timestamp, unique } from 'drizzle-orm/pg-core';

/**
 * A machine that runs TIA Portal and the MCP server, and whose files are imported.
 *
 * @remarks
 * Identified by name for now. Phase 3 of docs/WEB-PLATFORM.md gives each one a token, so that a
 * station can only push its own history; until then the importer runs on the machine itself.
 */
export const station = pgTable('station', {
  id: integer('id').primaryKey().generatedAlwaysAsIdentity(),
  name: text('name').notNull().unique(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow()
});

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
    importedAt: timestamp('imported_at', { withTimezone: true }).notNull().defaultNow()
  },
  (table) => [
    unique('change_station_entry_key').on(table.stationId, table.entryKey),
    index('change_station_occurred_at').on(table.stationId, table.occurredAt)
  ]
);
