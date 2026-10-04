import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { after, before, beforeEach, describe, it } from 'node:test';
import { sql } from 'drizzle-orm';
import { migrateDatabase, openDatabase, type DatabaseConnection } from '../src/db/connection.ts';
import { change } from '../src/db/schema.ts';
import { importAuditTrail } from '../src/import/auditImport.ts';

/**
 * The importer is what the web's change history is built from, so the ways it can be wrong are the
 * ways that history can lie: an edited trail shown as clean, an entry shown twice, one left out.
 *
 * @remarks
 * Against a real PostgreSQL — TEST_DATABASE_URL, the database `docker compose up` creates for the
 * tests — because idempotence is the database's unique constraint doing its job, and a fake would
 * test the fake. A missing URL fails the run rather than skipping it: a suite that passes by not
 * running is how a guarantee quietly stops being checked.
 */
describe('audit import', () => {
  const golden = readTrail('audit-chain-golden-v2.jsonl');
  let connection: DatabaseConnection;

  before(async () => {
    connection = openDatabase(process.env['TEST_DATABASE_URL']);
    await migrateDatabase(connection.database);
  });

  beforeEach(async () => {
    await connection.database.execute(sql`TRUNCATE TABLE change, station RESTART IDENTITY CASCADE`);
  });

  after(async () => {
    await connection.close();
  });

  it('imports every entry of an intact trail', async () => {
    const result = await importAuditTrail(connection.database, { stationName: 'PC-1', lines: golden });

    assert.equal(result.kind, 'imported');
    assert.equal(result.kind === 'imported' && result.inserted, 3);
    assert.equal(await countChanges(), 3);
  });

  it('imports nothing the second time', async () => {
    await importAuditTrail(connection.database, { stationName: 'PC-1', lines: golden });

    const second = await importAuditTrail(connection.database, { stationName: 'PC-1', lines: golden });

    assert.deepEqual(second, { kind: 'imported', read: 3, inserted: 0, alreadyPresent: 3, unchained: 0 });
    assert.equal(await countChanges(), 3);
  });

  it('brings in only the new entries of a trail that grew', async () => {
    await importAuditTrail(connection.database, { stationName: 'PC-1', lines: golden.slice(0, 2) });

    const grown = await importAuditTrail(connection.database, { stationName: 'PC-1', lines: golden });

    assert.equal(grown.kind === 'imported' && grown.inserted, 1);
  });

  it('refuses an edited entry whole, naming its line', async () => {
    // Importing the entries before the edit would show an edited history as a shorter, clean one.
    const edited = golden.map((line, index) => (index === 1 ? line.replace('"detail":""', '"detail":"edited"') : line));

    const result = await importAuditTrail(connection.database, { stationName: 'PC-1', lines: edited });

    assert.equal(result.kind, 'refused');
    assert.equal(result.kind === 'refused' && result.line, 2);
    assert.equal(await countChanges(), 0);
  });

  it('refuses a trail with a line that is not an entry', async () => {
    const corrupt = [...golden.filter((line) => line.trim().length > 0), 'this is not JSON'];

    const result = await importAuditTrail(connection.database, { stationName: 'PC-1', lines: corrupt });

    assert.equal(result.kind === 'refused' && result.line, 4);
    assert.equal(await countChanges(), 0);
  });

  it('imports the entries from before chaining and counts them', async () => {
    const result = await importAuditTrail(connection.database, {
      stationName: 'PC-1',
      lines: readTrail('audit-chain-golden.jsonl')
    });

    assert.equal(result.kind === 'imported' && result.unchained, 1);
    assert.equal(await countChanges(), 4);
  });

  it('keeps the same entries of two stations apart', async () => {
    await importAuditTrail(connection.database, { stationName: 'PC-1', lines: golden });

    const other = await importAuditTrail(connection.database, { stationName: 'PC-2', lines: golden });

    assert.equal(other.kind === 'imported' && other.inserted, 3);
  });

  it('reads the moment of a timestamp the server wrote', async () => {
    // Seven decimals and an escaped plus sign: what System.Text.Json writes for a DateTimeOffset.
    await importAuditTrail(connection.database, { stationName: 'PC-1', lines: golden });

    const first = await connection.database.query.change.findFirst({ where: (table, { eq }) => eq(table.sequence, 1) });

    assert.equal(first?.occurredAt?.toISOString(), '2026-09-05T12:00:00.000Z');
  });

  it('throws when no station is named', async () => {
    await assert.rejects(importAuditTrail(connection.database, { stationName: ' ', lines: golden }));
  });

  async function countChanges(): Promise<number> {
    return connection.database.$count(change);
  }
});

/** One of the harness's golden trails, whose hashes were computed by the C# server's own code. */
function readTrail(name: string): string[] {
  return readFileSync(new URL(`../../harness/test/assets/${name}`, import.meta.url), 'utf8').split('\n');
}
