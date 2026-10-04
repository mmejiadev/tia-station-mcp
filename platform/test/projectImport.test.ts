import assert from 'node:assert/strict';
import { after, before, beforeEach, describe, it } from 'node:test';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { project } from '../src/db/schema.ts';
import { importProjects } from '../src/import/projectImport.ts';
import { emptyTestDatabase, openTestDatabase, readGolden } from './testDatabase.ts';

/**
 * Who made each project and when it was last changed, read from the journal the C# server writes.
 * The author is what phase 3 links to a person, so it has to arrive intact.
 */
describe('project import', () => {
  const golden = readGolden('projects-golden.jsonl');
  let connection: DatabaseConnection;

  before(async () => {
    connection = await openTestDatabase();
  });

  beforeEach(async () => {
    await emptyTestDatabase(connection);
  });

  after(async () => {
    await connection.close();
  });

  it('records who made the project and when', async () => {
    await importProjects(connection.database, { stationName: 'PC-1', lines: golden });

    const [row] = await connection.database.select().from(project);

    assert.equal(row?.name, 'Cell');
    assert.equal(row?.tiaAuthor, 'mamem');
    assert.equal(row?.tiaCreatedAt?.toISOString(), '2026-09-01T08:00:00.000Z');
    assert.equal(row?.tiaPath, 'C:\\Projects\\Cell\\Cell.ap20');
  });

  it('keeps one row per project however often it is imported', async () => {
    await importProjects(connection.database, { stationName: 'PC-1', lines: golden });
    await importProjects(connection.database, { stationName: 'PC-1', lines: golden });

    assert.equal(await connection.database.$count(project), 1);
  });

  it('keeps the latest record when an older one is imported after it', async () => {
    // Imports can run in any order; the history must not move backwards because one did.
    const newer = recordAt('2026-10-04T09:00:00+00:00', 'teacher');
    const older = recordAt('2026-10-01T09:00:00+00:00', 'student');

    await importProjects(connection.database, { stationName: 'PC-1', lines: [newer] });
    await importProjects(connection.database, { stationName: 'PC-1', lines: [older] });

    const [row] = await connection.database.select().from(project);

    assert.equal(row?.tiaModifiedBy, 'teacher');
  });

  it('refuses a record with no path, naming its line', async () => {
    const result = await importProjects(connection.database, {
      stationName: 'PC-1',
      lines: ['{"timestamp":"2026-10-03T12:00:00+00:00","event":"Opened"}']
    });

    assert.equal(result.kind === 'refused' && result.line, 1);
    assert.equal(await connection.database.$count(project), 0);
  });

  function recordAt(timestamp: string, lastModifiedBy: string): string {
    const record = JSON.parse(golden[0] ?? '{}') as Record<string, unknown>;

    return JSON.stringify({ ...record, timestamp, lastModifiedBy });
  }
});
