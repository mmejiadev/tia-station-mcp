import { sql } from 'drizzle-orm';
import { integer, pgTable, text, timestamp, unique, uniqueIndex } from 'drizzle-orm/pg-core';
import { organization, user } from './auth.ts';
import { station } from './history.ts';

/**
 * What a person says about themselves as an engineer.
 *
 * @remarks
 * Every field is optional, on purpose: data minimisation (GDPR, article 5) asks for no more personal
 * data than the purpose needs, and the purpose — telling colleagues who did the work — is served by
 * the name and e-mail the sign-in already gives. Nothing here is required to use the platform.
 */
export const profile = pgTable('profile', {
  userId: text('user_id')
    .primaryKey()
    .references(() => user.id, { onDelete: 'cascade' }),
  /** Engineer, technician, student, teacher… */
  jobTitle: text('job_title'),
  specialty: text('specialty'),
  company: text('company'),
  bio: text('bio'),
  languages: text('languages'),
  certifications: text('certifications'),
  updatedAt: timestamp('updated_at', { withTimezone: true })
    .notNull()
    .defaultNow()
    .$onUpdate(() => new Date())
});

/**
 * "I am this TIA Portal author on that station": the link from a project to a person.
 *
 * @remarks
 * A claim until a supervisor or an admin of the organisation confirms it, and only a confirmed one
 * puts a person's name on projects. Without the confirmation anybody could claim anybody's work.
 *
 * **One confirmed person per author and station**, enforced by the database rather than by the code
 * that confirms: two confirmations racing each other must not both succeed. Several people may
 * claim the same author meanwhile — that is a question for the supervisor, not an error.
 */
export const tiaIdentity = pgTable(
  'tia_identity',
  {
    id: integer('id').primaryKey().generatedAlwaysAsIdentity(),
    organizationId: text('organization_id')
      .notNull()
      .references(() => organization.id, { onDelete: 'cascade' }),
    userId: text('user_id')
      .notNull()
      .references(() => user.id, { onDelete: 'cascade' }),
    stationId: integer('station_id')
      .notNull()
      .references(() => station.id),
    tiaAuthor: text('tia_author').notNull(),
    /** 'claimed' or 'confirmed', as text; anything else is treated as unconfirmed. */
    status: text('status').notNull().default('claimed'),
    claimedAt: timestamp('claimed_at', { withTimezone: true }).notNull().defaultNow(),
    confirmedBy: text('confirmed_by').references(() => user.id, { onDelete: 'set null' }),
    confirmedAt: timestamp('confirmed_at', { withTimezone: true })
  },
  (table) => [
    unique('tia_identity_claim').on(table.organizationId, table.stationId, table.tiaAuthor, table.userId),
    uniqueIndex('tia_identity_one_confirmed')
      .on(table.organizationId, table.stationId, table.tiaAuthor)
      .where(sql`${table.status} = 'confirmed'`)
  ]
);
