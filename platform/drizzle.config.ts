import { defineConfig } from 'drizzle-kit';

/**
 * `npm run db:generate` compares `src/db/schema.ts` with the migrations in `drizzle/` and writes the
 * SQL for the difference. The migrations are committed: they are how every other database reaches
 * the same schema.
 */
export default defineConfig({
  dialect: 'postgresql',
  schema: './src/db/schema.ts',
  out: './drizzle'
});
