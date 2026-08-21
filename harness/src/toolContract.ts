import type { McpServerConnection } from './mcpClient.ts';

/**
 * Every tool the harness calls, and the arguments it sends.
 *
 * @remarks
 * Written down so it can be checked, and it exists because of the failure it would have prevented:
 * the loop sent `scl` to `WriteScl`, whose parameter is `sclCode`, and `projectPath` to
 * `OpenProject`, whose parameter is `path`. Both were guesses. Both arrived as "An error occurred
 * invoking 'WriteScl'" — the protocol carries no detail — so finding the first one took a full run,
 * the server's log turned on, and a stack trace.
 *
 * The check below runs against the server's own schemas, in a second, without TIA Portal. A name
 * this harness gets wrong is now a sentence naming it rather than a minute of work ending in
 * nothing.
 */
export const HarnessToolUsage: Readonly<Record<string, readonly string[]>> = {
  Connect: [],
  Disconnect: [],
  RetrieveProject: ['archivePath', 'targetDirectory'],
  OpenProject: ['path'],
  UseTcpIpNetworkMode: [],
  ExpandCellScl: ['cellPath', 'patternDirectory', 'includeEntryPoint'],
  WriteScl: ['softwarePath', 'sclCode'],
  EnableSimulationSupport: [],
  CompileSoftware: ['softwarePath'],
  CompileHardware: ['deviceItemPath'],
  DownloadToSimulation: ['softwarePath'],
  CreateSimulationInstance: ['instanceName', 'ipAddress', 'subnetMask', 'cpuType'],
  StartSimulationInstance: ['instanceName'],
  DeleteSimulationInstance: ['instanceName'],
  ListSimulationInstances: [],
  ListSimulationTags: ['instanceName', 'nameFilter', 'limit'],
  ReadSimulationTags: ['instanceName', 'tagNames'],
  WriteSimulationTag: ['instanceName', 'tagName', 'value']
};

/**
 * Checks the harness's calls against what the server says its tools take.
 *
 * @param connection The server to ask.
 * @returns One sentence per disagreement found, empty when there are none.
 * @remarks
 * Three things are checked, and the third is the one that catches a change nobody told the harness
 * about:
 *
 * 1. The tool exists.
 * 2. Every argument the harness sends is a parameter the tool has.
 * 3. Every **required** parameter of the tool is one the harness sends. A parameter that becomes
 *    required later would otherwise break the loop on the first call, mid-run, with no detail.
 *
 * An optional parameter the harness does not send is not a breach. Most tools have several, and
 * demanding them all would make this fail every time the server grew a convenience.
 */
export async function findContractBreaches(connection: McpServerConnection): Promise<string[]> {
  const schemas = await connection.listToolSchemas();
  const breaches: string[] = [];

  for (const [tool, sent] of Object.entries(HarnessToolUsage)) {
    const schema = schemas.get(tool);

    if (schema === undefined) {
      breaches.push(`${tool}: the server has no such tool`);

      continue;
    }

    for (const argument of sent) {
      if (!schema.properties.includes(argument)) {
        breaches.push(
          `${tool}: the harness sends '${argument}', which is not a parameter. It takes: ` +
            schema.properties.join(', ')
        );
      }
    }

    for (const required of schema.required) {
      if (!sent.includes(required)) {
        breaches.push(`${tool}: '${required}' is required and the harness does not send it`);
      }
    }
  }

  return breaches;
}
