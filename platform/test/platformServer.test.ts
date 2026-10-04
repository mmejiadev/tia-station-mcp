import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import type { AddressInfo } from 'node:net';
import type { Server } from 'node:http';
import { after, before, beforeEach, describe, it } from 'node:test';
import { createAuth, type PlatformAuth } from '../src/auth/auth.ts';
import { readAuthSettings } from '../src/auth/authSettings.ts';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { createPlatformServer } from '../src/server/platformServer.ts';
import { aPerson } from './people.ts';
import { emptyTestDatabase, openTestDatabase } from './testDatabase.ts';

const Origin = 'http://localhost:5173';

// Placeholder credentials: nothing here signs in through a provider, so they are never sent.
const Settings = readAuthSettings({
  BETTER_AUTH_SECRET: 'test-secret-that-is-at-least-32-characters',
  BETTER_AUTH_URL: Origin,
  GOOGLE_CLIENT_ID: 'test',
  GOOGLE_CLIENT_SECRET: 'test',
  GITHUB_CLIENT_ID: 'test',
  GITHUB_CLIENT_SECRET: 'test'
});

/**
 * The server is thin, and what is tested here is exactly its thinness holding: Better Auth mounted
 * where the providers return to, nothing answered to a stranger, and nothing changed from another
 * site. What the endpoints decide is tested in the people tests.
 *
 * @remarks
 * A real session, created by Better Auth's own adapter, with its cookie signed the way Better Auth
 * signs it — so a signed-in request here is one Better Auth itself accepts, not a stub.
 */
describe('platform server', () => {
  let connection: DatabaseConnection;
  let auth: PlatformAuth;
  let server: Server;
  let base: string;

  before(async () => {
    connection = await openTestDatabase();
    auth = createAuth(connection.database, Settings);
    server = createPlatformServer({ auth, database: connection.database, origin: Origin });
    await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
    base = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
  });

  beforeEach(async () => {
    await emptyTestDatabase(connection);
  });

  after(async () => {
    await new Promise((resolve) => server.close(resolve));
    await connection.close();
  });

  it('mounts Better Auth where the sign-in providers return to', async () => {
    const response = await fetch(`${base}/api/auth/ok`);

    assert.equal(response.status, 200);
  });

  it('answers a stranger with 401', async () => {
    const response = await fetch(`${base}/api/me`);

    assert.equal(response.status, 401);
  });

  it('answers a signed-in person with who they are', async () => {
    const cookie = await signIn('student');

    const response = await fetch(`${base}/api/me`, { headers: { cookie } });
    const body = (await response.json()) as { user: { userId: string } };

    assert.equal(response.status, 200);
    assert.equal(body.user.userId, 'student');
  });

  it('refuses a change sent from another site, even with a valid session', async () => {
    // Cross-site request forgery: the browser attaches the cookie to the other site's request.
    const cookie = await signIn('student');

    const response = await fetch(`${base}/api/me/profile`, {
      method: 'PATCH',
      headers: { cookie, origin: 'https://elsewhere.example', 'content-type': 'application/json' },
      body: JSON.stringify({ jobTitle: 'Forged' })
    });

    assert.equal(response.status, 403);
  });

  it('accepts a change sent from the platform itself', async () => {
    const cookie = await signIn('student');

    const response = await fetch(`${base}/api/me/profile`, {
      method: 'PATCH',
      headers: { cookie, origin: Origin, 'content-type': 'application/json' },
      body: JSON.stringify({ jobTitle: 'Student' })
    });

    assert.equal(response.status, 200);
  });

  it('answers an endpoint that does not exist with 404', async () => {
    const response = await fetch(`${base}/api/nothing-here`);

    assert.equal(response.status, 404);
  });

  /** A person with a session, and the cookie their browser would send. */
  async function signIn(userId: string): Promise<string> {
    await aPerson(connection.database, userId);

    const context = await auth.$context;
    const session = await context.internalAdapter.createSession(userId);
    const signature = createHmac('sha256', Settings.secret).update(session.token).digest('base64');

    return `${context.authCookies.sessionToken.name}=${encodeURIComponent(`${session.token}.${signature}`)}`;
  }
});
