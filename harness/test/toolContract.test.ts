import assert from 'node:assert/strict';
import { after, before, describe, it } from 'node:test';
import { McpServerConnection } from '../src/mcpClient.ts';
import { resolveServerExecutable, serverExecutableAbsence } from '../src/serverLocation.ts';
import { findContractBreaches, HarnessToolUsage } from '../src/toolContract.ts';

/**
 * The harness calls the tools the server actually has, with the arguments they actually take.
 *
 * This test exists because of two guesses that cost a run each: `scl` where `WriteScl` takes
 * `sclCode`, and `projectPath` where `OpenProject` takes `path`. Both arrived as "An error occurred
 * invoking 'WriteScl'" — the protocol carries no detail — so each took a full download-length run,
 * the server's logging turned on, and a stack trace to identify.
 *
 * It needs no TIA Portal: listing tools never reaches the Openness API. A second here replaces a
 * minute and a half of nothing.
 *
 * It does need the server to be built, which a hosted runner cannot do, so it says so and skips
 * rather than failing there. See `serverExecutableAbsence`.
 */
describe('the harness and the server agree', { skip: serverExecutableAbsence() }, () => {
  let connection: McpServerConnection;

  before(async () => {
    connection = await McpServerConnection.open({ executable: resolveServerExecutable() });
  });

  after(async () => {
    await connection?.close();
  });

  it('sends only arguments the tools have, and every argument they require', async () => {
    const breaches = await findContractBreaches(connection);

    assert.deepEqual(breaches, [], `\n  ${breaches.join('\n  ')}`);
  });

  it('names a tool that does not exist rather than failing on the call', async () => {
    // The check has to be able to fail, and this is what proves it does. A contract check that
    // always passes is worse than none: it says the calls are right when nothing looked.
    const schemas = await connection.listToolSchemas();

    assert.ok(schemas.has('WriteScl'), 'WriteScl should exist');
    assert.ok(!schemas.has('NoSuchTool'), 'the server should not have an invented tool');

    // And the shape the check relies on is really there.
    assert.deepEqual([...(schemas.get('WriteScl')?.required ?? [])].sort(), ['sclCode', 'softwarePath']);
  });

  it('covers every tool the harness calls', async () => {
    // A tool called from the loop but missing from the usage table would never be checked, which
    // would leave exactly the hole this file was written to close.
    const names = await connection.listToolNames();

    for (const tool of Object.keys(HarnessToolUsage)) {
      assert.ok(names.includes(tool), `${tool} is in the usage table but not on the server`);
    }
  });
});
