import { migrateDatabase, openDatabase } from './connection.ts';

/** `npm run db:migrate`: applies the pending migrations to DATABASE_URL. */
const connection = openDatabase(process.env['DATABASE_URL']);

try {
  await migrateDatabase(connection.database);
  console.log('The database schema is up to date.');
} finally {
  await connection.close();
}
