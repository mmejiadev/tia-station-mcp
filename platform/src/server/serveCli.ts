import { createAuth } from '../auth/auth.ts';
import { readAuthSettings } from '../auth/authSettings.ts';
import { migrateDatabase, openDatabase } from '../db/connection.ts';
import { createPlatformServer } from './platformServer.ts';

/**
 * `npm run serve`: migrates the database and serves the platform on 127.0.0.1:4318.
 *
 * @remarks
 * The loopback address, deliberately, like the harness API: the dashboard's development server
 * forwards `/api/auth` and `/api/me` here, so the browser talks to one origin and this server is
 * reachable from nowhere else. Serving others on a network is the deployment question in
 * docs/WEB-PLATFORM.md, and it is answered by a proper HTTPS front, not by changing this address.
 */
const Host = '127.0.0.1';
const Port = 4318;

const settings = readAuthSettings(process.env);
const connection = openDatabase(process.env['DATABASE_URL']);

await migrateDatabase(connection.database);

const server = createPlatformServer({
  auth: createAuth(connection.database, settings),
  database: connection.database,
  origin: settings.baseUrl
});

server.listen(Port, Host, () => {
  console.log(`Platform serving on http://${Host}:${Port}, for the web at ${settings.baseUrl}.`);
});

const stop = (): void => {
  server.close(() => void connection.close());
};

process.on('SIGINT', stop);
process.on('SIGTERM', stop);
