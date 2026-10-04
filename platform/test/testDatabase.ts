import { readFileSync } from 'node:fs';
import { sql } from 'drizzle-orm';
import { migrateDatabase, openDatabase, type DatabaseConnection } from '../src/db/connection.ts';

/**
 * Opens the test database and brings its schema up to date.
 *
 * @returns The connection.
 * @remarks
 * TEST_DATABASE_URL, never DATABASE_URL: the tests empty every table they touch. A missing URL
 * throws rather than skipping, because a suite that passes by not running is how a guarantee
 * quietly stops being checked.
 */
export async function openTestDatabase(): Promise<DatabaseConnection> {
  const connection = openDatabase(process.env['TEST_DATABASE_URL']);

  await migrateDatabase(connection.database);

  return connection;
}

/**
 * Empties every table, so no test sees another's rows.
 *
 * @param connection The test database.
 */
export async function emptyTestDatabase(connection: DatabaseConnection): Promise<void> {
  await connection.database.execute(
    sql`TRUNCATE TABLE tia_identity, profile, invitation, member, organization, verification, account, session, "user", compilation_message, compilation, project, change, station RESTART IDENTITY CASCADE`
  );
}

/**
 * One of the platform's golden journals, which the C# server's tests assert it writes byte for byte.
 *
 * @param name The file under `test/assets/`.
 * @returns Its lines.
 */
export function readGolden(name: string): string[] {
  return readFileSync(new URL(`./assets/${name}`, import.meta.url), 'utf8').split('\n');
}
