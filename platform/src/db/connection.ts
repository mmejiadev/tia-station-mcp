import { drizzle, type NodePgDatabase } from 'drizzle-orm/node-postgres';
import { migrate } from 'drizzle-orm/node-postgres/migrator';
import { fileURLToPath } from 'node:url';
import pg from 'pg';
import * as schema from './schema.ts';

/** The database, typed by the schema. */
export type PlatformDatabase = NodePgDatabase<typeof schema>;

/** A transaction on the database, as `database.transaction` hands it to its callback. */
export type PlatformTransaction = Parameters<Parameters<PlatformDatabase['transaction']>[0]>[0];

/** A database and the means to let go of it. */
export type DatabaseConnection = {
  readonly database: PlatformDatabase;
  /** Closes every connection of the pool. A process that skips it does not exit. */
  readonly close: () => Promise<void>;
};

const MigrationsFolder = fileURLToPath(new URL('../../drizzle', import.meta.url));

/**
 * Opens a pool of connections to a database.
 *
 * @param url A PostgreSQL connection string.
 * @returns The database and its close function.
 * @remarks
 * A missing or empty URL throws here, with the variable to set, rather than letting `pg` fall back
 * to its defaults — which would connect to whatever listens on the standard port, and on the machine
 * this was written on that is a different PostgreSQL from the one in Docker.
 */
export function openDatabase(url: string | undefined): DatabaseConnection {
  if (url === undefined || url.trim().length === 0) {
    throw new Error('No database URL. Set DATABASE_URL in .env - copy .env.example at the repository root.');
  }

  const pool = new pg.Pool({ connectionString: url });

  return {
    database: drizzle(pool, { schema }),
    close: () => pool.end()
  };
}

/**
 * Brings a database's schema up to date with the migrations in `drizzle/`.
 *
 * @param database The database to migrate.
 * @remarks
 * Safe to run on every start: a migration already applied is skipped.
 */
export async function migrateDatabase(database: PlatformDatabase): Promise<void> {
  await migrate(database, { migrationsFolder: MigrationsFolder });
}
