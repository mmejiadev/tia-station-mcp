import assert from 'node:assert/strict';
import { after, before, beforeEach, describe, it } from 'node:test';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { aPerson } from './people.ts';
import { Origin, startTestServer, type TestServer } from './sessions.ts';
import { emptyTestDatabase, openTestDatabase } from './testDatabase.ts';

/**
 * The server is thin, and what is tested here is exactly its thinness holding: Better Auth mounted
 * where the providers return to, nothing answered to a stranger, and nothing changed from another
 * site. What the endpoints decide is tested in the people and workspace tests.
 */
describe('platform server', () => {
  let connection: DatabaseConnection;
  let server: TestServer;

  before(async () => {
    connection = await openTestDatabase();
    server = await startTestServer(connection);
  });

  beforeEach(async () => {
    await emptyTestDatabase(connection);
  });

  after(async () => {
    await server.close();
    await connection.close();
  });

  it('mounts Better Auth where the sign-in providers return to', async () => {
    const response = await fetch(`${server.base}/api/auth/ok`);

    assert.equal(response.status, 200);
  });

  it('answers a stranger with 401', async () => {
    const response = await fetch(`${server.base}/api/platform/me`);

    assert.equal(response.status, 401);
  });

  it('answers a signed-in person with who they are', async () => {
    const cookie = await signIn('student');

    const response = await fetch(`${server.base}/api/platform/me`, { headers: { cookie } });
    const body = (await response.json()) as { user: { userId: string } };

    assert.equal(response.status, 200);
    assert.equal(body.user.userId, 'student');
  });

  it('refuses a change sent from another site, even with a valid session', async () => {
    // Cross-site request forgery: the browser attaches the cookie to the other site's request.
    const cookie = await signIn('student');

    const response = await fetch(`${server.base}/api/platform/me/profile`, {
      method: 'PATCH',
      headers: { cookie, origin: 'https://elsewhere.example', 'content-type': 'application/json' },
      body: JSON.stringify({ jobTitle: 'Forged' })
    });

    assert.equal(response.status, 403);
  });

  it('accepts a change sent from the platform itself', async () => {
    const cookie = await signIn('student');

    const response = await fetch(`${server.base}/api/platform/me/profile`, {
      method: 'PATCH',
      headers: { cookie, origin: Origin, 'content-type': 'application/json' },
      body: JSON.stringify({ jobTitle: 'Student' })
    });

    assert.equal(response.status, 200);
  });

  it('answers a profile field that is not text with 400, not 500', async () => {
    const cookie = await signIn('student');

    const response = await fetch(`${server.base}/api/platform/me/profile`, {
      method: 'PATCH',
      headers: { cookie, origin: Origin, 'content-type': 'application/json' },
      body: JSON.stringify({ jobTitle: 5 })
    });

    assert.equal(response.status, 400);
  });

  it('answers an endpoint that does not exist with 404', async () => {
    const response = await fetch(`${server.base}/api/platform/nothing-here`);

    assert.equal(response.status, 404);
  });

  async function signIn(userId: string): Promise<string> {
    await aPerson(connection.database, userId);

    return server.sessionCookie(userId);
  }
});
