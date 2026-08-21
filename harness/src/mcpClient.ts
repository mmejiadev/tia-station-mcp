import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

/**
 * Where the server is and how it should be configured. Nothing here has a default that points at a
 * path on one machine: the executable is passed in, because a hardcoded path is the rule this
 * repository breaks least willingly.
 */
export type ServerOptions = {
  /** Full path to TiaMcpServer.exe. */
  readonly executable: string;
  /** Path to the write policy. Without one the server denies every write, correctly. */
  readonly policyPath?: string;
  /** Where the audit trail is appended. */
  readonly auditPath?: string;
  /** Where copies of overwritten state are kept. */
  readonly backupRoot?: string;
  /** How long one tool call may take. Defaults to {@link DefaultRequestTimeoutMilliseconds}. */
  readonly requestTimeoutMilliseconds?: number;
  /**
   * Turns the server's logging on, to stderr. Omit for none.
   *
   * @remarks
   * Worth turning on far more often than it looks: the protocol carries almost nothing about why a
   * tool failed — measured, a failing call arrives as "An error occurred" with the reason nowhere in
   * the response — so with logging off there is no way at all to find out. It comes back from
   * {@link McpServerConnection.serverDiagnostics}.
   *
   * Only stderr is offered, and that is not a simplification: the server's `--logging` flag selects
   * a *destination* rather than a level — 1 stderr, 2 debug output, 3 the Windows event log — and
   * under stdio the other two are invisible to the client that spawned it. Passing 0, which is what
   * a flag called "logging" invites, turns logging off entirely. That cost a run to find out.
   */
  readonly serverLogging?: 'stderr';
};

/**
 * How long one tool call may take before the client gives up.
 *
 * @remarks
 * Ten minutes, and the SDK's own default of sixty seconds is what made this constant exist: the
 * first end-to-end run died on `MCP error -32001: Request timed out` before it had done anything.
 * Nothing about this workload fits in a minute. Connecting to TIA Portal alone takes about
 * forty-five seconds, retrieving a project about thirty, and a compile followed by a download to a
 * virtual controller is minutes. A timeout shorter than the work turns every measurement into a
 * measurement of the timeout.
 *
 * It is still a timeout and not infinity, because a hung Openness call has to end the run rather
 * than hold it forever — that is the concurrency hazard the server's OpennessGate exists for.
 */
export const DefaultRequestTimeoutMilliseconds = 600_000;

/** The server's `--logging` value that sends its log to stderr. */
const StderrLoggingSelector = '1';

/** What one tool takes. */
export type ToolSchema = {
  readonly properties: readonly string[];
  readonly required: readonly string[];
};

/** What a tool call came back with. */
export type ToolResult = {
  /** Whether the server reported the call itself as an error. */
  readonly isError: boolean;
  /** The text the tool returned, joined. Every tool in this server answers with text. */
  readonly text: string;
  /** The parsed payload when the text is JSON, otherwise undefined. */
  readonly payload: unknown;
};

/**
 * The harness's connection to the TIA MCP server.
 *
 * Deliberately thin: it owns the process, the transport and the translation from a tool result into
 * something the loop can branch on, and nothing else. No retries, no knowledge of what any
 * individual tool means. The loop decides what to do about a failure, because only the loop knows
 * whether the failure was the point.
 */
export class McpServerConnection {
  private readonly client: Client;
  private readonly transport: StdioClientTransport;
  private readonly serverLog: string[] = [];
  private readonly requestTimeoutMilliseconds: number;

  private constructor(
    client: Client,
    transport: StdioClientTransport,
    serverLog: string[],
    requestTimeoutMilliseconds: number
  ) {
    this.client = client;
    this.transport = transport;
    this.serverLog = serverLog;
    this.requestTimeoutMilliseconds = requestTimeoutMilliseconds;
  }

  /**
   * Starts the server and completes the MCP handshake.
   *
   * @param options Where the executable is and how to configure it.
   * @returns A connection, already initialised.
   */
  static async open(options: ServerOptions): Promise<McpServerConnection> {
    const transport = new StdioClientTransport({
      command: options.executable,
      args: buildArguments(options),

      // Piped, not inherited. Under stdio a server may only log to stderr, since stdout carries the
      // protocol — so this is where diagnostics appear when there are any. Measured: with logging
      // off, which is the default, the stream stays empty, so an empty log here means "nothing was
      // logged" and never "nothing went wrong". Pass --logging to the server to fill it.
      stderr: 'pipe'
    });

    const serverLog: string[] = [];
    transport.stderr?.on('data', (chunk: Buffer) => {
      serverLog.push(chunk.toString('utf8'));
    });

    const client = new Client({ name: 'tia-station-harness', version: '0.1.0' });

    await client.connect(transport);

    return new McpServerConnection(
      client,
      transport,
      serverLog,
      options.requestTimeoutMilliseconds ?? DefaultRequestTimeoutMilliseconds
    );
  }

  /** The names of every tool the server exposes. */
  async listToolNames(): Promise<string[]> {
    const listed = await this.client.listTools();

    return listed.tools.map((tool) => tool.name);
  }

  /**
   * The parameters of every tool, by name.
   *
   * @remarks
   * Read from the server rather than written down anywhere here, which is the point: this is what
   * lets the harness's own calls be checked against the truth instead of against a memory of it.
   */
  async listToolSchemas(): Promise<Map<string, ToolSchema>> {
    const listed = await this.client.listTools();
    const schemas = new Map<string, ToolSchema>();

    for (const tool of listed.tools) {
      const schema = tool.inputSchema as { properties?: Record<string, unknown>; required?: unknown };

      schemas.set(tool.name, {
        properties: Object.keys(schema.properties ?? {}),
        required: Array.isArray(schema.required) ? schema.required.filter((name): name is string => typeof name === 'string') : []
      });
    }

    return schemas;
  }

  /**
   * Calls one tool.
   *
   * @param name The tool name, as listToolNames reports it.
   * @param args The tool's arguments.
   * @param timeoutMilliseconds How long to wait, when this call's own duration is known.
   * @returns What came back, with the text parsed as JSON when it is JSON.
   * @remarks
   * A tool that refuses a write is **not** an error here, and that distinction is the reason this
   * method returns a result instead of throwing. The governance layer answers a refusal as a normal
   * response carrying the reason, so a caller that treated it as a failure would retry something it
   * must not retry. Only a broken call — no such tool, malformed arguments — sets isError.
   */
  async callTool(
    name: string,
    args: Record<string, unknown> = {},
    timeoutMilliseconds: number = this.requestTimeoutMilliseconds
  ): Promise<ToolResult> {
    try {
      const result = await this.client.callTool({ name, arguments: args }, undefined, {
        timeout: timeoutMilliseconds
      });
      const text = joinTextContent(result.content);

      return {
        isError: result.isError === true,
        text,
        payload: parseJsonOrUndefined(text)
      };
    } catch (error) {
      // A failing tool arrives in one of two shapes and this method used to handle one of them: a
      // result with isError set, or a JSON-RPC error, which the SDK raises as an exception.
      // Measured — GetProject took the second path and killed the run outright. Both are the same
      // thing to a caller: the tool did not do the work. Turning it into a result is what lets the
      // loop record an outcome and carry on to the next specification instead of dying.
      return { isError: true, text: describeCallFailure(name, error), payload: undefined };
    }
  }

  /**
   * Everything the server has written to stderr so far, which is empty unless logging is on.
   *
   * @remarks
   * Returned as one string because that is how it will be read: pasted into a failure message. It
   * is not parsed, on purpose — the day the log format changes, a harness that parsed it would fail
   * to report the very thing it was trying to report.
   */
  serverDiagnostics(): string {
    return this.serverLog.join('');
  }

  /** Shuts the server down and releases the process. */
  async close(): Promise<void> {
    await this.client.close();
    await this.transport.close();
  }
}

/**
 * Builds the server's command line.
 *
 * @remarks
 * Only the options that were given are passed. The server has its own defaults for the policy, the
 * audit trail and the backup root, and passing an empty string for one would override a sound
 * default with nothing — which for the policy means denying every write, for a reason nobody could
 * see from here.
 */
function buildArguments(options: ServerOptions): string[] {
  const args: string[] = [];

  if (options.serverLogging === 'stderr') {
    args.push('--logging', StderrLoggingSelector);
  }

  if (options.policyPath !== undefined) {
    args.push('--policy', options.policyPath);
  }

  if (options.auditPath !== undefined) {
    args.push('--audit', options.auditPath);
  }

  if (options.backupRoot !== undefined) {
    args.push('--backups', options.backupRoot);
  }

  return args;
}

/**
 * Describes a call that failed as an exception rather than as a result.
 *
 * @remarks
 * The name is included because the error itself often does not carry it, and a run of several tools
 * would otherwise report a failure with no way to tell which one.
 */
function describeCallFailure(name: string, error: unknown): string {
  const message = error instanceof Error ? error.message : String(error);
  const code = (error as { code?: unknown } | undefined)?.code;

  return code === undefined ? `${name} failed: ${message}` : `${name} failed (${String(code)}): ${message}`;
}

/** Joins the text blocks of a tool result, ignoring any other content type. */
function joinTextContent(content: unknown): string {
  if (!Array.isArray(content)) {
    return '';
  }

  return content
    .filter((block): block is { type: 'text'; text: string } => isTextBlock(block))
    .map((block) => block.text)
    .join('\n');
}

function isTextBlock(block: unknown): boolean {
  if (typeof block !== 'object' || block === null) {
    return false;
  }

  const candidate = block as { type?: unknown; text?: unknown };

  return candidate.type === 'text' && typeof candidate.text === 'string';
}

/**
 * Parses the text as JSON, or reports that it is not JSON.
 *
 * @remarks
 * Undefined rather than a thrown error: some tools answer with a sentence and some with a document,
 * and a caller asking for the payload of the first has not made a mistake.
 */
function parseJsonOrUndefined(text: string): unknown {
  if (text.length === 0) {
    return undefined;
  }

  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}
