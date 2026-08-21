import assert from 'node:assert/strict';
import { after, before, describe, it } from 'node:test';
import { McpServerConnection } from '../src/mcpClient.ts';
import { resolveServerExecutable } from '../src/serverLocation.ts';

/**
 * The harness can talk to the server.
 *
 * Nothing else in phase 3 matters until this passes, which is why it is the first thing written. It
 * needs no TIA Portal: starting the server, listing its tools and asking which mode the session is
 * in never reaches the Openness API. A test here that needed a portal would be a test nobody could
 * run while writing the loop.
 */
describe('McpServerConnection', () => {
  let connection: McpServerConnection;

  before(async () => {
    connection = await McpServerConnection.open({ executable: resolveServerExecutable() });
  });

  after(async () => {
    // Always, even if a test failed. The server is a child process holding a pipe, and one left
    // behind per failed run is the Windows equivalent of the orphan portal this repository has
    // chased before.
    await connection?.close();
  });

  it('lists the tools the server actually exposes', async () => {
    const names = await connection.listToolNames();

    // Three of them, chosen because they are the three the loop depends on and they come from
    // different parts of the server: one read, one guarded write, one that needs no project at all.
    assert.ok(names.includes('GetOperationMode'), `GetOperationMode is missing from: ${names.join(', ')}`);
    assert.ok(names.includes('WriteScl'), 'WriteScl is missing');
    assert.ok(names.includes('ExpandCellScl'), 'ExpandCellScl is missing');

    // A count, not a list. The exact number changes every time a tool is added, and asserting it
    // would make this test fail for the one reason that is never a defect.
    assert.ok(names.length > 40, `expected a full toolset, got ${names.length}`);
  });

  it('reports which mode the session is in, and it is Study', async () => {
    // The safety default, asserted from the outside for the first time. Everything the harness will
    // do assumes Study Mode: it drives PLCSIM, it confirms its own changes, and it must never find
    // itself in the mode that commands physical hardware.
    const result = await connection.callTool('GetOperationMode');

    assert.equal(result.isError, false, `${result.text}\n--- server log ---\n${connection.serverDiagnostics()}`);
    assert.match(result.text, /Study/);
  });

  it('reports a governance refusal as a response, not as a failed call', async () => {
    // The distinction the whole loop is built on, and the reason callTool returns a result instead
    // of throwing. No policy is configured, so the guard refuses before anything is touched, and the
    // refusal arrives as an ordinary response carrying the reason and a plan id. A harness that read
    // this as a failed call would retry a change it must not retry.
    //
    // This tool and not WriteScl, deliberately: measured, WriteScl with no project open throws
    // before the guard is ever consulted, which is the other case, below. A refusal has to come
    // from a tool whose guard runs first.
    const result = await connection.callTool('CreateSimulationInstance', {
      instanceName: 'HarnessSmokeTest',
      ipAddress: '192.168.0.99'
    });

    assert.equal(result.isError, false, `the call itself should succeed: ${result.text}`);

    // The machine-readable half, which is what the loop will branch on. Matching the sentence would
    // tie the harness to wording that is free to improve.
    const meta = (result.payload as { meta?: { success?: unknown; outcome?: unknown } } | undefined)?.meta;

    assert.equal(meta?.success, false, `expected a refusal, got: ${result.text}`);
    assert.equal(meta?.outcome, 'Refused', `expected outcome Refused, got: ${result.text}`);

    // And nothing happened: a refused create must not leave a virtual controller behind.
    const instances = await connection.callTool('ListSimulationInstances');

    assert.ok(
      !instances.text.includes('HarnessSmokeTest'),
      `the refused instance was created anyway: ${instances.text}`
    );
  });

  it('reports a call the server cannot even attempt as an error', async () => {
    // The other side of the same distinction. Writing SCL with no project open is not a policy
    // decision, it is an invalid state, and the server throws, so isError is true and the loop must
    // not read it as "the guard said no". Measured rather than assumed: this is exactly the case
    // that was first mistaken for a refusal.
    const result = await connection.callTool('WriteScl', {
      softwarePath: 'NoSuchPlc',
      scl: 'FUNCTION_BLOCK "FB_Nothing" BEGIN END_FUNCTION_BLOCK'
    });

    assert.equal(result.isError, true, `expected a failed call, got: ${result.text}`);
  });
});
