import assert from 'node:assert/strict';
import { after, before, beforeEach, describe, it } from 'node:test';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { readProfile, updateProfile } from '../src/people/profiles.ts';
import { aPerson } from './people.ts';
import { emptyTestDatabase, openTestDatabase } from './testDatabase.ts';

/** What a person says about themselves: optional, theirs to change, and bounded. */
describe('profiles', () => {
  let connection: DatabaseConnection;

  before(async () => {
    connection = await openTestDatabase();
  });

  beforeEach(async () => {
    await emptyTestDatabase(connection);
    await aPerson(connection.database, 'student');
  });

  after(async () => {
    await connection.close();
  });

  it('is empty, not missing, before anything is written', async () => {
    const profile = await readProfile(connection.database, 'student');

    assert.equal(profile.jobTitle, null);
  });

  it('changes only the fields that were sent', async () => {
    await updateProfile(connection.database, 'student', { jobTitle: 'Student', company: 'Institut' });

    const result = await updateProfile(connection.database, 'student', { specialty: 'Automation' });

    assert.equal(result.kind === 'updated' && result.profile.jobTitle, 'Student');
    assert.equal(result.kind === 'updated' && result.profile.specialty, 'Automation');
  });

  it('clears a field sent empty', async () => {
    await updateProfile(connection.database, 'student', { company: 'Institut' });

    const result = await updateProfile(connection.database, 'student', { company: '   ' });

    assert.equal(result.kind === 'updated' && result.profile.company, null);
  });

  it('refuses a field longer than its limit, and changes nothing', async () => {
    const result = await updateProfile(connection.database, 'student', { jobTitle: 'x'.repeat(201) });

    assert.equal(result.kind, 'refused');
    assert.equal((await readProfile(connection.database, 'student')).jobTitle, null);
  });
});
