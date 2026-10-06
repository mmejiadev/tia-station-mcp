import { index, integer, pgTable, text, timestamp, type AnyPgColumn } from 'drizzle-orm/pg-core';
import { organization } from './auth.ts';

/**
 * A folder of projects in an organisation, nested to any depth.
 *
 * @remarks
 * The web's own organisation of projects — nothing in TIA Portal corresponds to it, and nothing here
 * is written back. Deleting a folder deletes the folders inside it; the projects in them are not
 * deleted, they return to the top level (`project.folder_id` is set to null).
 */
export const folder = pgTable(
  'folder',
  {
    id: integer('id').primaryKey().generatedAlwaysAsIdentity(),
    organizationId: text('organization_id')
      .notNull()
      .references(() => organization.id, { onDelete: 'cascade' }),
    parentId: integer('parent_id').references((): AnyPgColumn => folder.id, { onDelete: 'cascade' }),
    name: text('name').notNull(),
    createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow()
  },
  (table) => [index('folder_organization_parent').on(table.organizationId, table.parentId)]
);
