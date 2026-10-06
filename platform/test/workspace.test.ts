import assert from 'node:assert/strict';
import { eq } from 'drizzle-orm';
import { after, before, beforeEach, describe, it } from 'node:test';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { folder, project, tiaIdentity } from '../src/db/schema.ts';
import { createFolder, deleteFolder, fileProject, updateFolder } from '../src/workspace/folders.ts';
import { listChanges, listCompilations, readProject } from '../src/workspace/projectRead.ts';
import { issuePairingCode } from '../src/workspace/stationPairing.ts';
import { linkStation } from '../src/workspace/stations.ts';
import { readWorkspace } from '../src/workspace/workspaceRead.ts';
import { addMember } from './people.ts';
import { emptyTestDatabase, openTestDatabase } from './testDatabase.ts';
import { aWorkspace, type WorkspaceFixture } from './workspaceFixture.ts';

/**
 * Who sees which project, and who may organise them. Every rule has a test that names it: a project
 * shown to the wrong person is the failure this layer exists to prevent.
 */
describe('workspace', () => {
  let connection: DatabaseConnection;
  let fixture: WorkspaceFixture;

  before(async () => {
    connection = await openTestDatabase();
  });

  beforeEach(async () => {
    await emptyTestDatabase(connection);
    fixture = await aWorkspace(connection.database);
  });

  after(async () => {
    await connection.close();
  });

  describe('seeing projects', () => {
    it('shows nobody the projects of a station no admin has linked', async () => {
      const workspace = await readWorkspace(connection.database, 'owner');

      assert.deepEqual(workspace.flatMap((organization) => organization.projects), []);
    });

    it('shows a linked station\'s projects to every member, a viewer included', async () => {
      await link();

      const workspace = await readWorkspace(connection.database, 'reader');

      assert.deepEqual(workspace.find((organization) => organization.id === 'class')?.projects.map((item) => item.name), ['Cell']);
    });

    it('shows a project only under the organisation its station is linked to', async () => {
      // The workspace is read for all of a person's organisations at once: this is the guard on that.
      await link();
      await addMember(connection.database, 'other-class', 'reader', 'viewer');

      const workspace = await readWorkspace(connection.database, 'reader');
      const byOrganization = Object.fromEntries(workspace.map((item) => [item.id, item.projects.map((row) => row.name)]));

      assert.deepEqual(byOrganization, { class: ['Cell'], 'other-class': [] });
    });

    it('shows them to nobody outside the organisation', async () => {
      await link();

      const outsider = await readWorkspace(connection.database, 'outsider');
      const other = await readWorkspace(connection.database, 'other-owner');

      assert.deepEqual([outsider.flatMap((item) => item.projects), other.flatMap((item) => item.projects)], [[], []]);
    });

    it('answers "not found" to an outsider asking for the project, not "forbidden"', async () => {
      // "Forbidden" would tell a stranger the project exists.
      await link();

      const result = await readProject(connection.database, { userId: 'other-owner', projectId: fixture.projectId });

      assert.equal(result.kind === 'refused' && result.refusal, 'not-found');
    });

    it('takes the status from the latest compilation, not the worst', async () => {
      await link();

      const result = await readProject(connection.database, { userId: 'reader', projectId: fixture.projectId });

      assert.equal(result.kind === 'done' && result.value.status, 'warning');
    });

    it('names the person confirmed as the project\'s TIA author', async () => {
      await link();
      await connection.database.insert(tiaIdentity).values({
        organizationId: 'class',
        userId: 'student',
        stationId: fixture.stationId,
        tiaAuthor: 'mamem',
        status: 'confirmed',
        confirmedBy: 'teacher'
      });

      const result = await readProject(connection.database, { userId: 'reader', projectId: fixture.projectId });

      assert.equal(result.kind === 'done' && result.value.author?.name, 'student');
    });
  });

  describe('linking a station', () => {
    it('is refused to anybody but an admin, even with a valid code', async () => {
      const result = await linkStation(connection.database, { ...asks('teacher', 'class'), pairingCode: await aCode() });

      assert.equal(result.kind === 'refused' && result.refusal, 'forbidden');
    });

    it('is refused to an admin without the code the station issued', async () => {
      // Anybody can create an organisation and be its admin: the role proves nothing about a station.
      await aCode();

      const result = await linkStation(connection.database, { ...asks('other-owner', 'other-class'), pairingCode: 'AAAA-AAAA' });

      assert.equal(result.kind, 'refused');
    });

    it('is refused once the code has expired', async () => {
      const code = await aCode();
      const later = new Date(Date.now() + 31 * 60 * 1000);

      const result = await linkStation(connection.database, { ...asks('owner', 'class'), pairingCode: code }, later);

      assert.equal(result.kind, 'refused');
    });

    it('uses a code once', async () => {
      const code = await aCode();
      await linkStation(connection.database, { ...asks('owner', 'class'), pairingCode: code });

      const again = await linkStation(connection.database, { ...asks('owner', 'class'), pairingCode: code });

      assert.equal(again.kind, 'refused');
    });

    it('accepts the code typed in lower case, with or without its dash', async () => {
      const code = await aCode();

      const result = await linkStation(connection.database, {
        ...asks('owner', 'class'),
        pairingCode: code.replace('-', '').toLowerCase()
      });

      assert.equal(result.kind, 'done');
    });

    it('answers a station that does not exist exactly as a wrong code', async () => {
      // A different answer would let anybody with an organisation list other people's machines.
      const missing = await linkStation(connection.database, {
        ...asks('owner', 'class'),
        stationName: 'NOWHERE',
        pairingCode: 'AAAA-AAAA'
      });
      const wrong = await linkStation(connection.database, { ...asks('owner', 'class'), pairingCode: 'AAAA-AAAA' });

      assert.deepEqual(missing, wrong);
    });

    it('does not take a station from the organisation it belongs to, even with a fresh code', async () => {
      await link();

      const result = await linkStation(connection.database, { ...asks('other-owner', 'other-class'), pairingCode: await aCode() });

      assert.equal(result.kind, 'refused');
    });
  });

  describe('history', () => {
    it('lists a project\'s changes, filtered by outcome', async () => {
      await link();

      const result = await listChanges(connection.database, { userId: 'reader', projectId: fixture.projectId, outcome: 'Refused' });

      assert.deepEqual(result.kind === 'done' && result.value.map((item) => item.outcome), ['Refused']);
    });

    it('lists compilations newest first, each with its messages', async () => {
      await link();

      const result = await listCompilations(connection.database, { userId: 'reader', projectId: fixture.projectId });

      assert.deepEqual(
        result.kind === 'done' && result.value.map((item) => [item.errorCount, item.messages.length]),
        [
          [0, 0],
          [2, 1]
        ]
      );
    });
  });

  describe('folders', () => {
    it('refuses a viewer creating one', async () => {
      const result = await createFolder(connection.database, { userId: 'reader', organizationId: 'class', parentId: null, name: 'Clase 0965' });

      assert.equal(result.kind === 'refused' && result.refusal, 'forbidden');
    });

    it('lets an engineer create one inside another', async () => {
      const parent = await aFolder('Grau Superior', null);

      const result = await createFolder(connection.database, { userId: 'student', organizationId: 'class', parentId: parent, name: '  0965  ' });

      assert.deepEqual(result.kind === 'done' && [result.value.name, result.value.parentId], ['0965', parent]);
    });

    it('refuses moving a folder inside a folder within it', async () => {
      // The tree would loop, and the sidebar that walks it would never finish.
      const top = await aFolder('Top', null);
      const inner = await aFolder('Inner', top);

      const result = await updateFolder(connection.database, { userId: 'student', folderId: top, parentId: inner });

      assert.equal(result.kind === 'refused' && result.refusal, 'invalid');
    });

    it('refuses nesting folders past the depth limit instead of trusting the tree', async () => {
      // The first version stopped walking at the limit and allowed the move: the bound failed open.
      let parent: number | null = null;

      for (let level = 0; level < 32; level++) {
        parent = await aFolder(`Level ${level}`, parent);
      }

      const result = await createFolder(connection.database, { userId: 'owner', organizationId: 'class', parentId: parent, name: 'Too deep' });

      assert.equal(result.kind === 'refused' && result.refusal, 'invalid');
    });

    it('refuses moving a folder whose subtree would then nest past the limit', async () => {
      let deepest: number | null = null;

      for (let level = 0; level < 31; level++) {
        deepest = await aFolder(`Level ${level}`, deepest);
      }

      const moving = await aFolder('Moving', null);
      await aFolder('Inside moving', moving);

      const result = await updateFolder(connection.database, { userId: 'owner', folderId: moving, parentId: deepest });

      assert.equal(result.kind === 'refused' && result.refusal, 'invalid');
    });

    it('refuses filing a project in another organisation\'s folder', async () => {
      await link();
      const foreign = await createFolder(connection.database, { userId: 'other-owner', organizationId: 'other-class', parentId: null, name: 'Theirs' });

      const result = await fileProject(connection.database, {
        userId: 'owner',
        projectId: fixture.projectId,
        folderId: foreign.kind === 'done' ? foreign.value.id : -1
      });

      assert.equal(result.kind === 'refused' && result.refusal, 'invalid');
    });

    it('returns a deleted folder\'s projects to the top level, and deletes the folders inside it', async () => {
      await link();
      const top = await aFolder('Top', null);
      await aFolder('Inner', top);
      await fileProject(connection.database, { userId: 'student', projectId: fixture.projectId, folderId: top });

      await deleteFolder(connection.database, { userId: 'student', folderId: top });

      const [row] = await connection.database.select().from(project).where(eq(project.id, fixture.projectId));
      assert.equal(row?.folderId, null);
      assert.equal(await connection.database.$count(folder), 0);
    });
  });

  async function link(): Promise<void> {
    const result = await linkStation(connection.database, { ...asks('owner', 'class'), pairingCode: await aCode() });

    assert.equal(result.kind, 'done');
  }

  async function aCode(): Promise<string> {
    const issued = await issuePairingCode(connection.database, 'MANUELA');

    assert.equal(issued.kind, 'done');

    return issued.kind === 'done' ? issued.value.code : '';
  }

  function asks(userId: string, organizationId: string) {
    return { userId, organizationId, stationName: 'MANUELA' };
  }

  async function aFolder(name: string, parentId: number | null): Promise<number> {
    const result = await createFolder(connection.database, { userId: 'owner', organizationId: 'class', parentId, name });

    assert.equal(result.kind, 'done');

    return result.kind === 'done' ? result.value.id : -1;
  }
});
