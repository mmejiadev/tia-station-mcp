import assert from 'node:assert/strict';
import { after, before, beforeEach, describe, it } from 'node:test';
import type { DatabaseConnection } from '../src/db/connection.ts';
import { compilation, compilationMessage, project } from '../src/db/schema.ts';
import { importCompilations } from '../src/import/compilationImport.ts';
import { importProjects } from '../src/import/projectImport.ts';
import { emptyTestDatabase, openTestDatabase, readGolden } from './testDatabase.ts';

/**
 * The compilation history the web shows, read from the journal the C# server writes. The golden
 * file is the contract: `JournalContractTests` asserts the server writes it byte for byte.
 */
describe('compilation import', () => {
  const golden = readGolden('compilations-golden.jsonl');
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

  it('imports every compilation with its counts', async () => {
    const result = await importCompilations(connection.database, { stationName: 'PC-1', lines: golden });

    assert.deepEqual(result, { kind: 'imported', read: 2, inserted: 2 });

    const rows = await connection.database.select().from(compilation).orderBy(compilation.lineNumber);

    assert.deepEqual(
      rows.map((row) => [row.severity, row.errorCount, row.warningCount]),
      [
        ['Error', 1, 0],
        ['Success', 0, 0]
      ]
    );
  });

  it('keeps every message of a compilation', async () => {
    await importCompilations(connection.database, { stationName: 'PC-1', lines: golden });

    const messages = await connection.database.select().from(compilationMessage);

    assert.deepEqual(
      messages.map((message) => [message.position, message.severity, message.path, message.description]),
      [[0, 'Error', 'Main', 'Tag not defined']]
    );
  });

  it('imports nothing the second time', async () => {
    await importCompilations(connection.database, { stationName: 'PC-1', lines: golden });

    const second = await importCompilations(connection.database, { stationName: 'PC-1', lines: golden });

    assert.deepEqual(second, { kind: 'imported', read: 2, inserted: 0 });
    assert.equal(await connection.database.$count(compilationMessage), 1);
  });

  it('creates the project of a compilation it has not seen, named after its file', async () => {
    // A project opened before the server recorded projects still has its compilations shown.
    await importCompilations(connection.database, { stationName: 'PC-1', lines: golden });

    const projects = await connection.database.select().from(project);

    assert.deepEqual(
      projects.map((row) => [row.name, row.tiaAuthor]),
      [['Cell', null]]
    );
  });

  it('files the compilation under the project the project journal described', async () => {
    await importProjects(connection.database, { stationName: 'PC-1', lines: readGolden('projects-golden.jsonl') });

    await importCompilations(connection.database, { stationName: 'PC-1', lines: golden });

    assert.equal(await connection.database.$count(project), 1);
  });

  it('refuses a journal with a line that is not a compilation, naming it', async () => {
    const corrupt = [...golden.filter((line) => line.trim().length > 0), '{"softwarePath":"PLC_0"}'];

    const result = await importCompilations(connection.database, { stationName: 'PC-1', lines: corrupt });

    assert.equal(result.kind === 'refused' && result.line, 3);
    assert.equal(await connection.database.$count(compilation), 0);
  });
});
