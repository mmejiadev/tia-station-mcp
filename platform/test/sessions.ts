import { createHmac } from 'node:crypto';
import type { AddressInfo } from 'node:net';
import type { Server } from 'node:http';
import { createAuth, type PlatformAuth } from '../src/auth/auth.ts';
import { readAuthSettings } from '../src/auth/authSettings.ts';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { createPlatformServer } from '../src/server/platformServer.ts';

/** The web's origin in the tests: the one the server accepts changes from. */
export const Origin = 'http://localhost:5173';

// Placeholder credentials: nothing here signs in through a provider, so they are never sent.
const Settings = readAuthSettings({
  BETTER_AUTH_SECRET: 'test-secret-that-is-at-least-32-characters',
  BETTER_AUTH_URL: Origin,
  GOOGLE_CLIENT_ID: 'test',
  GOOGLE_CLIENT_SECRET: 'test',
  GITHUB_CLIENT_ID: 'test',
  GITHUB_CLIENT_SECRET: 'test'
});

/** A running platform server and the means to sign people into it. */
export type TestServer = {
  readonly base: string;
  readonly auth: PlatformAuth;
  /** The cookie a person's browser would send, for a person who already exists. */
  readonly sessionCookie: (userId: string) => Promise<string>;
  readonly close: () => Promise<void>;
};

/**
 * Starts the platform on a free port over the test database.
 *
 * @param connection The test database.
 * @returns The server.
 * @remarks
 * Sessions are real: created by Better Auth's own adapter, with the cookie signed the way Better
 * Auth signs it, so a signed-in request in a test is one Better Auth itself accepts.
 */
export async function startTestServer(connection: DatabaseConnection): Promise<TestServer> {
  const auth = createAuth(connection.database, Settings);
  const server: Server = createPlatformServer({ auth, database: connection.database, origin: Origin });

  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));

  return {
    base: `http://127.0.0.1:${(server.address() as AddressInfo).port}`,
    auth,
    sessionCookie: async (userId) => {
      const context = await auth.$context;
      const session = await context.internalAdapter.createSession(userId);
      const signature = createHmac('sha256', Settings.secret).update(session.token).digest('base64');

      return `${context.authCookies.sessionToken.name}=${encodeURIComponent(`${session.token}.${signature}`)}`;
    },
    close: () => new Promise((resolve) => server.close(() => resolve()))
  };
}
