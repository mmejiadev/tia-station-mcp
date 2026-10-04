import assert from 'node:assert/strict';
import { after, before, beforeEach, describe, it } from 'node:test';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { claimIdentity, confirmIdentity } from '../src/people/identityClaims.ts';
import { aPerson, aStation, addMember, anOrganization } from './people.ts';
import { emptyTestDatabase, openTestDatabase } from './testDatabase.ts';

/**
 * The link from a TIA author to a person, which decides whose name appears on a project. Each rule
 * here has a test that names it and would fail if the rule were removed.
 */
describe('identity claims', () => {
  let connection: DatabaseConnection;

  before(async () => {
    connection = await openTestDatabase();
  });

  beforeEach(async () => {
    await emptyTestDatabase(connection);

    const database = connection.database;

    await anOrganization(database, 'class');
    await aStation(database, 'MANUELA');

    for (const [person, role] of [
      ['student', 'engineer'],
      ['classmate', 'engineer'],
      ['teacher', 'supervisor'],
      ['reader', 'viewer'],
      ['owner', 'admin']
    ] as const) {
      await aPerson(database, person);
      await addMember(database, 'class', person, role);
    }

    await aPerson(database, 'outsider');
  });

  after(async () => {
    await connection.close();
  });

  it('records a member\'s claim', async () => {
    const result = await claim('student');

    assert.equal(result.kind, 'claimed');
  });

  it('refuses a claim from somebody outside the organisation', async () => {
    const result = await claim('outsider');

    assert.equal(result.kind, 'refused');
  });

  it('refuses a claim on a station whose history was never imported', async () => {
    const result = await claimIdentity(connection.database, {
      userId: 'student',
      organizationId: 'class',
      stationName: 'NOWHERE',
      tiaAuthor: 'mamem'
    });

    assert.equal(result.kind, 'refused');
  });

  it('returns the same claim when it is made twice', async () => {
    const first = await claim('student');
    const second = await claim('student');

    assert.deepEqual(second, first);
  });

  it('lets a supervisor confirm a claim', async () => {
    const claimed = await claim('student');

    const result = await confirmIdentity(connection.database, { confirmerId: 'teacher', identityId: idOf(claimed) });

    assert.equal(result.kind, 'confirmed');
  });

  it('refuses a confirmation by somebody whose role does not allow it', async () => {
    const claimed = await claim('student');

    const byReader = await confirmIdentity(connection.database, { confirmerId: 'reader', identityId: idOf(claimed) });
    const byEngineer = await confirmIdentity(connection.database, { confirmerId: 'classmate', identityId: idOf(claimed) });

    assert.deepEqual([byReader.kind, byEngineer.kind], ['refused', 'refused']);
  });

  it('refuses an admin confirming their own claim', async () => {
    // Two pairs of eyes, whatever the role: the same rule the Workshop Mode applies to change plans.
    const claimed = await claim('owner');

    const result = await confirmIdentity(connection.database, { confirmerId: 'owner', identityId: idOf(claimed) });

    assert.equal(result.kind, 'refused');
  });

  it('refuses a confirmation by a supervisor of another organisation', async () => {
    const database = connection.database;

    await anOrganization(database, 'other-class');
    await aPerson(database, 'other-teacher');
    await addMember(database, 'other-class', 'other-teacher', 'supervisor');

    const claimed = await claim('student');
    const result = await confirmIdentity(database, { confirmerId: 'other-teacher', identityId: idOf(claimed) });

    assert.equal(result.kind, 'refused');
  });

  it('keeps an author confirmed as one person from being confirmed as a second', async () => {
    const first = await claim('student');
    const second = await claim('classmate');

    await confirmIdentity(connection.database, { confirmerId: 'teacher', identityId: idOf(first) });
    const result = await confirmIdentity(connection.database, { confirmerId: 'teacher', identityId: idOf(second) });

    assert.equal(result.kind, 'refused');
  });

  function claim(userId: string) {
    return claimIdentity(connection.database, { userId, organizationId: 'class', stationName: 'MANUELA', tiaAuthor: 'mamem' });
  }
});

function idOf(result: { readonly kind: string; readonly identityId?: number }): number {
  assert.equal(result.kind, 'claimed');

  return result.identityId ?? -1;
}
