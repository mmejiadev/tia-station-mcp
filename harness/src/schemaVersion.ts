import type { DatabaseSync } from 'node:sqlite';

/**
 * The version of the recorded schema this harness understands.
 *
 * @remarks
 * It lives here rather than in `telemetry.ts` because there are now two things that open the store:
 * the run that writes it and the dashboard API that reads it. Two copies of this number would agree
 * right up until one of them was changed, and the failure that follows is the one the check exists
 * to prevent — numbers computed from columns that mean something else.
 *
 * Version 2 added `token_usage`. It changed nothing that was already there.
 */
export const SchemaVersion = 2;

/**
 * The versions this one can be reached from without reinterpreting anything already recorded.
 *
 * @remarks
 * A store is migrated forward **only** when every difference is a new table or a new column that
 * old rows are allowed not to have. Version 1 to 2 is that: `token_usage` did not exist, so no
 * version 1 run has one, and a run without a cost is honestly a run whose cost was never measured.
 *
 * A change that gives an existing column a new meaning does not belong here and must not be added
 * to this list. Refusing is the only answer that cannot mislead, and it is why the thirty-nine runs
 * already recorded keep their numbers instead of being read as something else.
 */
const MigratableFrom: readonly number[] = [1];

/**
 * Stamps the store with the schema version, migrates one that can be, or refuses one that cannot.
 *
 * @param database An open store whose tables already exist.
 * @remarks
 * The tables are created before this runs, so a store being migrated forward already has the new
 * ones by the time its version is looked at. This function's job is to decide whether that was
 * legitimate.
 */
export function verifySchemaVersion(database: DatabaseSync): void {
  const existing = database.prepare('SELECT version FROM schema_version').get() as { version: number } | undefined;

  if (existing === undefined) {
    database.prepare('INSERT INTO schema_version (version) VALUES (?)').run(SchemaVersion);

    return;
  }

  if (existing.version === SchemaVersion) {
    return;
  }

  if (MigratableFrom.includes(existing.version)) {
    database.prepare('UPDATE schema_version SET version = ?').run(SchemaVersion);

    return;
  }

  throw new Error(
    `This store was written with schema version ${existing.version} and this harness expects ` +
      `${SchemaVersion}. Point --database at a new file rather than mixing them: the columns of ` +
      'one version do not mean the same thing in another.'
  );
}
