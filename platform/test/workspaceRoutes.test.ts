import assert from 'node:assert/strict';
import { after, before, beforeEach, describe, it } from 'node:test';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { issuePairingCode } from '../src/workspace/stationPairing.ts';
import { linkStation } from '../src/workspace/stations.ts';
import { Origin, startTestServer, type TestServer } from './sessions.ts';
import { emptyTestDatabase, openTestDatabase } from './testDatabase.ts';
import { aWorkspace, type WorkspaceFixture } from './workspaceFixture.ts';

/**
 * The workspace endpoints over HTTP: that each answers with the status its result calls for, and
 * that a body the routes cannot read is refused rather than read as something else.
 */
describe('workspace routes', () => {
  let connection: DatabaseConnection;
  let server: TestServer;
  let fixture: WorkspaceFixture;

  before(async () => {
    connection = await openTestDatabase();
    server = await startTestServer(connection);
  });

  beforeEach(async () => {
    await emptyTestDatabase(connection);
    fixture = await aWorkspace(connection.database);
    const issued = await issuePairingCode(connection.database, 'MANUELA');
    await linkStation(connection.database, {
      userId: 'owner',
      organizationId: 'class',
      stationName: 'MANUELA',
      pairingCode: issued.kind === 'done' ? issued.value.code : ''
    });
  });

  after(async () => {
    await server.close();
    await connection.close();
  });

  it('lists the workspace of the signed-in person', async () => {
    const body = (await get('reader', '/api/platform/workspace')) as { organizations: { projects: { name: string }[] }[] };

    assert.deepEqual(body.organizations.flatMap((organization) => organization.projects.map((item) => item.name)), ['Cell']);
  });

  it('answers 404 to somebody outside the organisation asking for a project', async () => {
    const response = await fetch(`${server.base}/api/platform/projects/${fixture.projectId}`, {
      headers: { cookie: await server.sessionCookie('outsider') }
    });

    assert.equal(response.status, 404);
  });

  it('answers 403 to a viewer creating a folder', async () => {
    const response = await send('reader', 'POST', '/api/platform/folders', { organizationId: 'class', name: 'Clase' });

    assert.equal(response.status, 403);
  });

  it('creates a folder for an engineer and answers 201', async () => {
    const response = await send('student', 'POST', '/api/platform/folders', { organizationId: 'class', name: 'Clase' });

    assert.equal(response.status, 201);
  });

  it('refuses a folder named by text instead of reading it as the top level', async () => {
    // A project must not move to the top without a word because a client sent "5" instead of 5.
    const response = await send('student', 'PATCH', `/api/platform/projects/${fixture.projectId}`, { folderId: '5' });

    assert.equal(response.status, 400);
  });

  it('refuses deleting a folder from another site', async () => {
    const created = (await (await send('student', 'POST', '/api/platform/folders', { organizationId: 'class', name: 'Clase' })).json()) as { id: number };

    const response = await fetch(`${server.base}/api/platform/folders/${created.id}`, {
      method: 'DELETE',
      headers: { cookie: await server.sessionCookie('student'), origin: 'https://elsewhere.example' }
    });

    assert.equal(response.status, 403);
  });

  it('answers a body that is not a JSON object with 400, not 500', async () => {
    const cookie = await server.sessionCookie('student');
    const headers = { cookie, origin: Origin, 'content-type': 'application/json' };

    const asNull = await fetch(`${server.base}/api/platform/folders`, { method: 'POST', headers, body: 'null' });
    const malformed = await fetch(`${server.base}/api/platform/folders`, { method: 'POST', headers, body: '{"name":' });

    assert.deepEqual([asNull.status, malformed.status], [400, 400]);
  });

  it("answers an id beyond the database's range as one that does not exist", async () => {
    const cookie = await server.sessionCookie('reader');

    const response = await fetch(`${server.base}/api/platform/projects/3000000000`, { headers: { cookie } });

    assert.equal(response.status, 404);
  });

  it('answers 400 to an organisation id that does not decode', async () => {
    const response = await send('owner', 'POST', '/api/platform/organizations/%E0/stations', { stationName: 'MANUELA', pairingCode: 'x' });

    assert.equal(response.status, 400);
  });

  it('answers 400 to a folder id in the body beyond the database\'s range', async () => {
    const response = await send('student', 'PATCH', `/api/platform/projects/${fixture.projectId}`, { folderId: 3000000000 });

    assert.equal(response.status, 400);
  });

  it('answers 400 to a cursor it cannot read rather than returning the first page again', async () => {
    const cookie = await server.sessionCookie('reader');

    const response = await fetch(`${server.base}/api/platform/projects/${fixture.projectId}/changes?before=abc`, { headers: { cookie } });

    assert.equal(response.status, 400);
  });

  it('returns a folder unchanged when nothing was asked', async () => {
    const created = (await (await send('student', 'POST', '/api/platform/folders', { organizationId: 'class', name: 'Clase' })).json()) as {
      id: number;
    };

    const response = await send('student', 'PATCH', `/api/platform/folders/${created.id}`, {});

    assert.equal(response.status, 200);
  });

  async function get(userId: string, path: string): Promise<unknown> {
    const response = await fetch(`${server.base}${path}`, { headers: { cookie: await server.sessionCookie(userId) } });

    assert.equal(response.status, 200);

    return response.json();
  }

  async function send(userId: string, method: string, path: string, body: unknown): Promise<Response> {
    return fetch(`${server.base}${path}`, {
      method,
      headers: { cookie: await server.sessionCookie(userId), origin: Origin, 'content-type': 'application/json' },
      body: JSON.stringify(body)
    });
  }
});
