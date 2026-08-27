import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import { join, resolve } from 'node:path';
import { readAuditTrail } from './auditTrail.ts';
import { ChangeWatcher } from './changeWatcher.ts';
import { DefaultChatModel } from './copilotChat.ts';
import { createCopilot, type Copilot } from './copilotEndpoint.ts';
import { createChatAsker } from './copilotSender.ts';
import { respondTo, type ApiResponse, type ApiSources } from './dashboardApi.ts';
import { evaluateGate } from './gate.ts';
import { gatherEvidence } from './gateEvidence.ts';
import { MetricsReader } from './metricsReader.ts';
import { parseFlags } from './options.ts';
import { repositoryRoot } from './serverLocation.ts';

/**
 * The interface the API listens on, and it is not configurable.
 *
 * @remarks
 * The loopback address, deliberately. What this serves is an audit trail of everything the server
 * has changed and where every backup of it lives, and that belongs to the machine it was recorded
 * on. Making the interface a flag would make exposing it to a classroom network a typo away, so the
 * way to reach it from elsewhere is a tunnel somebody set up on purpose.
 */
const Interface = '127.0.0.1';

const DefaultPort = 4317;

/** The path a browser opens to be told when the store changes. */
const LivePath = '/api/live';

/** Where a question is put to the copilot. The one path here that is not a GET. */
const ChatPath = '/api/chat';

/** Where the dashboard asks whether there is a copilot to talk to at all. */
const CopilotPath = '/api/copilot';

/**
 * The largest request body this will read.
 *
 * @remarks
 * Sixty-four kilobytes, far more than the question and history limits in `copilotChat.ts` allow and
 * far less than anything worth worrying about. Enforced while reading rather than after, so a body
 * that never ends is dropped instead of held in memory until it is.
 */
const MaxBodyBytes = 64 * 1024;

/**
 * How often the store is asked whether it changed.
 *
 * @remarks
 * A second. An iteration of the loop takes tens of seconds and its shortest phase takes
 * milliseconds, so this is fast enough that a screen never looks stuck and slow enough that the
 * query is free. It runs only while somebody is watching.
 */
const PollMilliseconds = 1000;

/**
 * How often a comment is written down an idle stream.
 *
 * @remarks
 * A connection with nothing on it is indistinguishable from a broken one, and something between the
 * browser and here will eventually close it. Twenty seconds is well inside every default.
 */
const HeartbeatMilliseconds = 20_000;

/**
 * The origins a browser may read this from.
 *
 * @remarks
 * The Vite dev server, on both spellings of loopback, because a browser treats them as different
 * origins and which one the dashboard is opened on is not this server's business. A wildcard would
 * let any page the user has open read their audit trail through their own browser.
 */
const AllowedOrigins: readonly string[] = [
  'http://localhost:5173',
  'http://127.0.0.1:5173'
];

/** Where the API reads its evidence from, and where it listens. */
type Options = {
  readonly databasePath: string;
  readonly auditPath: string;
  readonly reviewPath: string;
  readonly port: number;
  readonly chatModel: string;
};

/**
 * Serves what a run recorded, read-only, for the dashboard.
 *
 * @remarks
 * Read-only is a property of what this exposes, not a setting: there is no endpoint here that
 * changes anything, and there is not going to be one. Every write in this project goes through the
 * guard in the MCP server, and a second door into the same project that did not would be exactly
 * the untested branch the governance rules exist to forbid. When the dashboard needs to confirm a
 * change, it will do it by calling the server's own `ApplyChange`.
 *
 * **`/api/chat` is a POST and it is not an exception to that.** It reads the same store the GET
 * endpoints read, sends what it read to a model, and returns the reply. It holds no state, writes
 * nothing, and - the part that matters - the model it asks is given no tools, so there is no path
 * from a sentence somebody types into that box to anything that changes a project. It is a POST
 * because a question and its history do not belong in a URL, not because it does more than read.
 */
function main(): void {
  const options = parseOptions(process.argv.slice(2));
  const reader = MetricsReader.open(options.databasePath);
  const sources: ApiSources = {
    reader,
    readAudit: () => readAuditTrail(options.auditPath),
    evaluateGate: () => evaluateGate(gatherEvidence(reader, options))
  };

  const copilot = createCopilot(sources, options.chatModel, () => createChatAsker(options.chatModel));
  const live = startWatching(reader);
  const server = createServer((request, response) => serve(request, response, sources, live, copilot));

  // Without this an occupied port arrives as an unhandled 'error' event: a stack trace with
  // EADDRINUSE buried in it, which says nothing about the copy of this server already running.
  server.on('error', (error: NodeJS.ErrnoException) => {
    if (error.code === 'EADDRINUSE') {
      console.error(
        `Port ${options.port} is already taken, most likely by another copy of this server. Stop it, ` +
          'or start this one with --port and a different number.'
      );
      process.exitCode = 1;

      return;
    }

    throw error;
  });

  server.listen(options.port, Interface, () => {
    const copilotState = copilot.status();

    console.log(`Reading ${options.databasePath}`);
    console.log(`Listening on http://${Interface}:${options.port}`);

    // Said at startup rather than left for the first question to discover. The copilot is the one
    // part of this server that needs something the machine may not have, and the person who would
    // fix that is the one reading this line.
    console.log(
      copilotState.available
        ? `Copilot ready, asking ${copilotState.model}.`
        : `Copilot unavailable: ${copilotState.reason}`
    );
  });

  // Without this the store stays open and, on Windows, the file stays locked against the next run.
  process.on('SIGINT', () => {
    server.close();
    reader.close();
  });
}

/**
 * Turns one request into one response.
 *
 * @remarks
 * A failed read is reported as a failure and never as an empty result. A dashboard that showed an
 * empty audit table because the trail could not be read would be claiming that nothing was changed,
 * which is the worst thing this API could say.
 */
function serve(
  request: IncomingMessage,
  response: ServerResponse,
  sources: ApiSources,
  live: ChangeWatcher,
  copilot: Copilot
): void {
  allowDashboardOrigin(request, response);

  const url = new URL(request.url ?? '/', `http://${Interface}`);

  if (request.method === 'OPTIONS') {
    answerPreflight(response);

    return;
  }

  // One path takes a POST and every other path takes a GET. Written as an exact match rather than
  // as "POST is allowed now": a second endpoint that accepted a body would have to be added here on
  // purpose, in front of this comment, which is the reason the check is shaped this way.
  if (request.method === 'POST') {
    if (url.pathname !== ChatPath) {
      send(response, {
        status: 405,
        body: { error: `${ChatPath} is the only path that takes a POST. ${url.pathname} does not.` }
      });

      return;
    }

    askCopilot(request, response, copilot);

    return;
  }

  if (request.method !== 'GET') {
    send(response, {
      status: 405,
      body: { error: `This API answers GET, and POST on ${ChatPath}. Not ${request.method}.` }
    });

    return;
  }

  if (url.pathname === LivePath) {
    stream(response, live);

    return;
  }

  if (url.pathname === CopilotPath) {
    send(response, { status: 200, body: copilot.status() });

    return;
  }

  try {
    send(response, respondTo(url.pathname, url.searchParams, sources));
  } catch (error) {
    const reason = error instanceof Error ? error.message : String(error);

    console.error(`${url.pathname} failed: ${reason}`);
    send(response, { status: 500, body: { error: reason } });
  }
}

/**
 * Reads the question off the wire and answers it.
 *
 * @remarks
 * A failed call to the model is reported as a failure and never as an empty answer, for the same
 * reason an unreadable audit trail is never served as an empty table: a blank reply in a chat reads
 * as the copilot having nothing to say, which is a different and far more misleading statement than
 * "the request to the model failed".
 */
function askCopilot(request: IncomingMessage, response: ServerResponse, copilot: Copilot): void {
  readBody(request)
    .then((body) => copilot.ask(body))
    .then((answer) => send(response, answer))
    .catch((error: unknown) => {
      const reason = error instanceof Error ? error.message : String(error);

      console.error(`${ChatPath} failed: ${reason}`);
      send(response, { status: 502, body: { error: reason } });
    });
}

/**
 * Collects a request body, refusing one that is too large.
 *
 * @returns The parsed JSON.
 * @remarks
 * The size is checked as the chunks arrive. Checking afterwards would mean having already held
 * whatever was sent, which is not a check at all.
 */
async function readBody(request: IncomingMessage): Promise<unknown> {
  const chunks: Buffer[] = [];
  let size = 0;

  for await (const chunk of request) {
    const piece = chunk as Buffer;

    size += piece.length;

    if (size > MaxBodyBytes) {
      throw new Error(`A request body may be at most ${MaxBodyBytes} bytes.`);
    }

    chunks.push(piece);
  }

  const text = Buffer.concat(chunks).toString('utf8');

  try {
    return JSON.parse(text) as unknown;
  } catch {
    throw new Error('The request body is not valid JSON.');
  }
}

/**
 * Answers the browser's preflight for the one path that takes a POST.
 *
 * @remarks
 * A JSON body makes the browser ask permission first, and without an answer the question never
 * leaves the page. This grants exactly the method and the header the chat needs; the origin check
 * in `allowDashboardOrigin` still decides who is being answered at all.
 */
function answerPreflight(response: ServerResponse): void {
  response.writeHead(204, {
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type',
    'Access-Control-Max-Age': '600'
  });
  response.end();
}

/**
 * Watches the store and keeps a single poll running for however many browsers are connected.
 *
 * @remarks
 * One timer, not one per connection. `unref` so the timer never keeps the process alive on its own:
 * an API server that cannot be closed because it is watching for changes nobody is waiting for is a
 * process somebody has to find in Task Manager.
 */
function startWatching(reader: MetricsReader): ChangeWatcher {
  const watcher = new ChangeWatcher(() => reader.changeToken());
  let lastFailure = '';

  const timer = setInterval(() => {
    watcher.poll((reason) => {
      // Reported once per distinct message rather than every second, so a store that stays broken
      // says so without burying everything else in the log.
      if (reason === lastFailure) {
        return;
      }

      lastFailure = reason;
      console.error(`The live watch failed: ${reason}`);
    });
  }, PollMilliseconds);

  timer.unref();

  return watcher;
}

/**
 * Holds one connection open and writes to it whenever the store changes.
 *
 * @remarks
 * Server-sent events rather than a WebSocket, which is what the roadmap deferred into this phase.
 * The traffic goes one way — the browser has nothing to tell the server that a GET does not already
 * say — and this is plain HTTP, so it needs no second protocol, no dependency, and it reconnects on
 * its own when the API is restarted, which happens rather a lot while the API is being written.
 *
 * The event carries the token and nothing else. What is on the screen is re-read from the endpoints
 * that already serve it, so there is exactly one path that produces a number.
 */
function stream(response: ServerResponse, live: ChangeWatcher): void {
  response.writeHead(200, {
    'Content-Type': 'text/event-stream; charset=utf-8',
    'Cache-Control': 'no-cache, no-transform',
    Connection: 'keep-alive'
  });

  response.write(`event: watching\ndata: {"pollMilliseconds":${PollMilliseconds}}\n\n`);

  const stopListening = live.subscribe((token) => {
    response.write(`event: changed\ndata: ${JSON.stringify({ token })}\n\n`);
  });

  const heartbeat = setInterval(() => response.write(': still here\n\n'), HeartbeatMilliseconds);

  heartbeat.unref();

  // Both, always. A listener left behind writes to a closed socket every time anything changes, and
  // a heartbeat left behind does it every twenty seconds for the life of the process.
  response.on('close', () => {
    stopListening();
    clearInterval(heartbeat);
  });
}

/** Lets the dashboard's dev server read this, and nothing else. */
function allowDashboardOrigin(request: IncomingMessage, response: ServerResponse): void {
  const origin = request.headers.origin;

  if (origin === undefined || !AllowedOrigins.includes(origin)) {
    return;
  }

  response.setHeader('Access-Control-Allow-Origin', origin);
  response.setHeader('Vary', 'Origin');
}

function send(response: ServerResponse, answer: ApiResponse): void {
  const body = JSON.stringify(answer.body, undefined, 2);

  response.writeHead(answer.status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body)
  });
  response.end(body);
}

function parseOptions(args: readonly string[]): Options {
  const values = parseFlags(
    args,
    'Usage: node src/apiServer.ts [--database <file>] [--audit <file>] [--review <file>] ' +
      '[--port <n>] [--chat-model <id>]. Every flag takes a value.'
  );

  // The same defaults a run writes to, so serving the evidence needs no arguments on the machine
  // that produced it.
  const harnessRoot = join(repositoryRoot(), '.tia-mcp', 'harness');

  return {
    databasePath: resolve(values.get('--database') ?? join(harnessRoot, 'metrics.db')),
    auditPath: resolve(values.get('--audit') ?? join(harnessRoot, 'audit.jsonl')),
    reviewPath: resolve(values.get('--review') ?? join(repositoryRoot(), 'docs', 'workshop-review.md')),
    port: readPort(values.get('--port')),
    chatModel: readChatModel(values.get('--chat-model'))
  };
}

/**
 * Which model the copilot asks.
 *
 * @remarks
 * An empty value is refused rather than defaulted. `--chat-model` at the end of a command line takes
 * whatever follows as its value, and quietly falling back to the default would answer at whatever
 * that model costs while the person believes they asked for the cheap one.
 */
function readChatModel(value: string | undefined): string {
  if (value === undefined) {
    return DefaultChatModel;
  }

  if (value.trim().length === 0) {
    throw new Error('--chat-model takes a model identifier, not an empty string.');
  }

  return value;
}

function readPort(value: string | undefined): number {
  if (value === undefined) {
    return DefaultPort;
  }

  const port = Number(value);

  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`--port takes a whole number between 1 and 65535, not '${value}'.`);
  }

  return port;
}

main();
