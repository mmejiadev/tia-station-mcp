import { existsSync } from 'node:fs';
import { defineConfig } from 'drizzle-kit';

// The repository's .env, which the other scripts read with --env-file; drizzle-kit loads this file
// itself, so it reads the .env here. Absent, the environment is used as it is.
//
// Relative to the working directory, like `schema` and `out` below: drizzle-kit resolves those
// against it too, so this configuration only works run from platform/ — measured 2026-10-06, run from
// the repository root it finds no schema — and the npm scripts are where it is run from.
const EnvironmentFile = '../.env';

if (existsSync(EnvironmentFile)) {
  process.loadEnvFile(EnvironmentFile);
}

const databaseUrl = process.env['DATABASE_URL'];

/**
 * `npm run db:generate` compares `src/db/schema.ts` with the migrations in `drizzle/` and writes the
 * SQL for the difference. The migrations are committed: they are how every other database reaches
 * the same schema.
 *
 * `npm run db:studio` opens Drizzle Studio on the database `DATABASE_URL` names, to look at the
 * tables in a browser. Generating needs no database, so the address is only given when there is one.
 */
export default defineConfig({
  dialect: 'postgresql',
  schema: './src/db/schema.ts',
  out: './drizzle',
  ...(databaseUrl === undefined ? {} : { dbCredentials: { url: databaseUrl } })
});
