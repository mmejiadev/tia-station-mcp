import type { McpServerConnection } from './mcpClient.ts';
import type { AcceptanceStep, Specification } from './specification.ts';

/** Whether the running cell did what the specification says, and what happened if not. */
export type VerificationResult = {
  readonly passed: boolean;
  /** One line, for a report and for the telemetry. Empty when it passed. */
  readonly detail: string;
};

const PollIntervalMilliseconds = 50;

/**
 * Runs a specification's acceptance steps against a running virtual controller.
 *
 * @param connection The server.
 * @param specification The case being checked.
 * @returns Whether it passed, and why not if it did not.
 * @remarks
 * This is the half of the loop that says whether generated code *works*, as opposed to whether it
 * compiles. A coordinator that hands a piece to the wrong station compiles perfectly, so without
 * this the harness would be measuring syntax.
 *
 * A step that fails stops the run of steps. Continuing would produce a second failure caused by the
 * first — "the piece never arrived" followed by "and it is not at station two" — and a report with
 * two entries for one fault is a report that overstates what went wrong.
 */
export async function verify(
  connection: McpServerConnection,
  specification: Specification
): Promise<VerificationResult> {
  const instanceName = specification.controller.name;

  for (const [index, step] of specification.acceptance.entries()) {
    const failure = await runStep(connection, instanceName, step);

    if (failure !== undefined) {
      return { passed: false, detail: `step ${index + 1} (${step.action} ${step.tag}): ${failure}` };
    }
  }

  return { passed: true, detail: '' };
}

/** Runs one step, and reports what went wrong or nothing at all. */
async function runStep(
  connection: McpServerConnection,
  instanceName: string,
  step: AcceptanceStep
): Promise<string | undefined> {
  if (step.action === 'write') {
    return await writeTag(connection, instanceName, step.tag, step.value);
  }

  if (step.action === 'waitFor') {
    return await waitForTag(connection, instanceName, step.tag, step.equals, step.timeoutMilliseconds);
  }

  const actual = await readTag(connection, instanceName, step.tag);

  if (step.equals !== undefined) {
    return actual === step.equals ? undefined : `expected ${step.equals}, holds ${actual}`;
  }

  return actual === step.notEquals ? `expected anything but ${step.notEquals}` : undefined;
}

async function writeTag(
  connection: McpServerConnection,
  instanceName: string,
  tag: string,
  value: string
): Promise<string | undefined> {
  const result = await connection.callTool('WriteSimulationTag', { instanceName, tagName: tag, value });

  if (result.isError) {
    return `the write failed: ${result.text}`;
  }

  // A refusal arrives as a normal response, so a write that was governed away would otherwise look
  // like it happened, and every later step would fail on a cell nobody had started.
  const meta = (result.payload as { meta?: { success?: unknown } } | undefined)?.meta;

  if (meta?.success !== true) {
    return `the write was not applied: ${result.text}`;
  }

  return undefined;
}

/**
 * Waits for a tag to reach a value.
 *
 * @remarks
 * Polling, and there is no alternative worth having: PLCSIM offers no subscription this server
 * exposes, and the interesting states of a cell last tens of scans, which is milliseconds. What the
 * timeout buys is that a cell which never gets there is a measurement rather than a hung run.
 */
async function waitForTag(
  connection: McpServerConnection,
  instanceName: string,
  tag: string,
  expected: string,
  timeoutMilliseconds: number
): Promise<string | undefined> {
  const deadline = Date.now() + timeoutMilliseconds;
  let actual = '';

  while (Date.now() < deadline) {
    actual = await readTag(connection, instanceName, tag);

    if (actual === expected) {
      return undefined;
    }

    await sleep(PollIntervalMilliseconds);
  }

  return `waited ${timeoutMilliseconds} ms for ${expected}, last saw ${actual}`;
}

/**
 * Reads one tag as text.
 *
 * @remarks
 * Compared as text on both sides, so a specification writes '17' and 'true' and does not have to
 * know that one arrives as a number and the other as a boolean. The alternative — typing the
 * expected value in the specification — would make every case carry the PLC type of every tag it
 * touches, which the tag list already knows.
 */
async function readTag(connection: McpServerConnection, instanceName: string, tag: string): Promise<string> {
  const result = await connection.callTool('ReadSimulationTags', { instanceName, tagNames: [tag] });

  if (result.isError) {
    return `<unreadable: ${result.text}>`;
  }

  const items = (result.payload as { items?: { value?: unknown }[] } | undefined)?.items;
  const value = items?.[0]?.value;

  return value === undefined ? '<missing>' : String(value);
}

function sleep(milliseconds: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, milliseconds);
  });
}
