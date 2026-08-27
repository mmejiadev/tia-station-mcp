import type { AuditEntry, AuditReadResult } from './auditTrail.ts';
import type { GateVerdict } from './gate.ts';
import type {
  IterationPhase,
  PhaseDuration,
  RecordedIteration,
  RecordedRun,
  SpecificationStatistics
} from './metricsReader.ts';

/**
 * What the endpoints below need to be able to read.
 *
 * @remarks
 * Named as what is required rather than as who provides it. `MetricsReader` satisfies it, and so
 * does a stand-in in a test, which is what lets every routing rule here — an unknown filter, a run
 * that does not exist, a bad identifier — be stated without a database on disk.
 */
export type DashboardStore = {
  runs(): RecordedRun[];
  run(runId: number): RecordedRun | undefined;
  iterationsOf(runId: number): RecordedIteration[];
  phaseDurations(runId?: number): PhaseDuration[];
  phasesOfIteration(iterationId: number): IterationPhase[];
  specificationStatistics(): SpecificationStatistics[];
};

/** What an endpoint answered, before anything has been written to a socket. */
export type ApiResponse = {
  readonly status: number;
  readonly body: unknown;
};

/**
 * Where an endpoint gets its evidence.
 *
 * @remarks
 * Passed in rather than opened here, so the routing can be tested without a database, a trail on
 * disk or a port. The two functions are read afresh on every request on purpose: a dashboard open
 * while a run is going must not show what the trail said when the server started.
 */
export type ApiSources = {
  readonly reader: DashboardStore;
  readonly readAudit: () => AuditReadResult;
  readonly evaluateGate: () => GateVerdict;
};

/**
 * What each endpoint answers with.
 *
 * @remarks
 * Exported because the dashboard imports them rather than describing the same payloads a second
 * time. A front end with its own copy of these shapes is a front end that keeps compiling after the
 * API stops sending what it says here, and then renders undefined into a metric.
 */
export type RunsResponse = {
  readonly runs: readonly RecordedRun[];
};

/** One run, everything recorded inside it. */
export type RunDetailResponse = {
  readonly run: RecordedRun;
  readonly iterations: readonly RecordedIteration[];
  readonly phases: readonly PhaseDuration[];
};

/** The numbers phase 3 exists to produce, each with the count behind it. */
export type MetricsResponse = {
  readonly specifications: readonly SpecificationStatistics[];
  readonly phases: readonly PhaseDuration[];
  readonly sampleSize: {
    readonly runs: number;
    readonly specificationAttempts: number;
  };
};

/** The audit trail, filtered, and how much of it there was before and after filtering. */
export type AuditResponse = {
  /** The most recent `limit` of the entries that matched, oldest of those first. */
  readonly entries: readonly AuditEntry[];
  /** How many entries matched the filters, whether or not they are all here. */
  readonly matched: number;
  /** How many entries the trail holds in total. */
  readonly total: number;
  /** How many were asked for, so a view can say that it is showing the end of a longer list. */
  readonly limit: number;
  readonly unreadableLines: readonly number[];
};

/**
 * The phases one iteration has finished, in order.
 *
 * @remarks
 * `finished` is the word that matters. A phase is recorded when it ends, so the one running right
 * now is not in this list — and a view built on it must say "not recorded yet" rather than guess.
 */
export type IterationPhasesResponse = {
  readonly iterationId: number;
  readonly phases: readonly IterationPhase[];
};

/** What the permanent banner shows, and where it got it. */
export type ModeResponse = {
  /** 'Study', 'Workshop', or 'unknown'. Never assumed. */
  readonly mode: string;
  readonly observed: readonly string[];
  readonly source: string;
};

/** The modes the audit trail is expected to record. Anything else reads as unknown, never as safe. */
const KnownModes: readonly string[] = ['Study', 'Workshop'];

/** Which query parameters `/api/audit` understands. Anything else is refused rather than ignored. */
const AuditFilters: readonly string[] = ['mode', 'tool', 'outcome', 'target', 'limit'];

/**
 * How many audit entries one request returns unless it asks for a different number.
 *
 * @remarks
 * The trail of a machine that has been running the loop is thousands of lines and grows with every
 * write; today's is 2291. Sending all of it and letting the browser build a row per line is slow in
 * exactly the situation somebody is in when they open this view — trying to find out what happened.
 * The response says how many matched, so a truncated answer is never mistaken for a complete one.
 */
const DefaultAuditLimit = 200;

/** The most a single request may ask for, so a mistyped limit cannot serve the whole trail. */
const MaximumAuditLimit = 5000;

const RunPathPattern = /^\/api\/runs\/([^/]+)$/;

const IterationPhasesPathPattern = /^\/api\/iterations\/([^/]+)\/phases$/;

/**
 * Answers one request from the recorded data.
 *
 * @param path The request path, without its query string.
 * @param query The query string, already parsed.
 * @param sources Where to read the evidence.
 * @returns The status and the body to serialise.
 * @remarks
 * A pure function of its arguments, and that is the point: every rule below — an unknown route, an
 * unrecognised filter, a run that does not exist — is a case a test can state without a socket.
 */
export function respondTo(path: string, query: URLSearchParams, sources: ApiSources): ApiResponse {
  const runPath = RunPathPattern.exec(path);

  if (runPath !== null) {
    return runDetail(runPath[1] ?? '', sources);
  }

  const iterationPhases = IterationPhasesPathPattern.exec(path);

  if (iterationPhases !== null) {
    return phasesOfIteration(iterationPhases[1] ?? '', sources);
  }

  const route = Routes[path];

  if (route === undefined) {
    return {
      status: 404,
      body: { error: `No endpoint at ${path}.`, endpoints: knownEndpoints() }
    };
  }

  return route(query, sources);
}

/** Every run, newest first. */
function runs(_query: URLSearchParams, sources: ApiSources): ApiResponse {
  const body: RunsResponse = { runs: sources.reader.runs() };

  return { status: 200, body };
}

/**
 * One run with its iterations and what each phase cost inside it.
 *
 * @remarks
 * An identifier that is not a whole number is a bad request rather than a missing run. The two say
 * different things to whoever is looking at the dashboard: one is a typo in a URL, the other is a
 * run that has been deleted or never existed.
 */
function runDetail(rawId: string, sources: ApiSources): ApiResponse {
  const runId = Number(rawId);

  if (!Number.isInteger(runId) || runId < 1) {
    return { status: 400, body: { error: `'${rawId}' is not a run identifier.` } };
  }

  const run = sources.reader.run(runId);

  if (run === undefined) {
    return { status: 404, body: { error: `No run ${runId} was recorded.` } };
  }

  const body: RunDetailResponse = {
    run,
    iterations: sources.reader.iterationsOf(runId),
    phases: sources.reader.phaseDurations(runId)
  };

  return { status: 200, body };
}

/**
 * What one iteration has got through so far.
 *
 * @remarks
 * An empty list is a real answer, not a missing one: an iteration that has started and finished no
 * phase yet has nothing to show, and that is different from an iteration that does not exist.
 */
function phasesOfIteration(rawId: string, sources: ApiSources): ApiResponse {
  const iterationId = Number(rawId);

  if (!Number.isInteger(iterationId) || iterationId < 1) {
    return { status: 400, body: { error: `'${rawId}' is not an iteration identifier.` } };
  }

  const body: IterationPhasesResponse = {
    iterationId,
    phases: sources.reader.phasesOfIteration(iterationId)
  };

  return { status: 200, body };
}

/**
 * The numbers phase 3 exists to produce, per specification and per phase.
 *
 * @remarks
 * Every rate leaves here with the count it was computed from beside it, never as a bare percentage.
 * The roadmap says so and it is right: 83% hides whether it was five of six or fifty of sixty.
 */
function metrics(_query: URLSearchParams, sources: ApiSources): ApiResponse {
  const specifications = sources.reader.specificationStatistics();

  const body: MetricsResponse = {
    specifications,
    phases: sources.reader.phaseDurations(),
    sampleSize: {
      runs: sources.reader.runs().length,
      specificationAttempts: specifications.reduce((total, entry) => total + entry.attempts, 0)
    }
  };

  return { status: 200, body };
}

/**
 * The audit trail, filtered.
 *
 * @remarks
 * A filter this endpoint does not know is a bad request, not an empty filter. Answering `?tol=X`
 * with every entry there is would look like a trail that recorded no such restriction, which is the
 * one thing an audit view may never do.
 */
function audit(query: URLSearchParams, sources: ApiSources): ApiResponse {
  const unknown = [...query.keys()].filter((key) => !AuditFilters.includes(key));

  if (unknown.length > 0) {
    return {
      status: 400,
      body: { error: `Unknown filter(s): ${unknown.join(', ')}.`, filters: AuditFilters }
    };
  }

  const limit = readLimit(query.get('limit'));

  if (limit === undefined) {
    return {
      status: 400,
      body: { error: `limit takes a whole number between 1 and ${MaximumAuditLimit}.` }
    };
  }

  const trail = sources.readAudit();
  const matched = trail.entries.filter((entry) => matchesFilters(entry, query));

  const body: AuditResponse = {
    // The end of the list rather than the start: the trail is written in order, so the most recent
    // entries are the ones somebody opening this view is looking for.
    entries: matched.slice(-limit),
    matched: matched.length,
    total: trail.entries.length,
    limit,
    unreadableLines: trail.unreadableLines
  };

  return { status: 200, body };
}

/**
 * How many entries were asked for.
 *
 * @returns The limit, or undefined when what was asked for is not one.
 * @remarks
 * Undefined rather than a fallback to the default. `?limit=all` silently becoming 200 would answer a
 * request for everything with a fifth of it and say nothing about the difference.
 */
function readLimit(value: string | null): number | undefined {
  if (value === null) {
    return DefaultAuditLimit;
  }

  const limit = Number(value);

  if (!Number.isInteger(limit) || limit < 1 || limit > MaximumAuditLimit) {
    return undefined;
  }

  return limit;
}

/** The five criteria and whether the door is open. */
function gate(_query: URLSearchParams, sources: ApiSources): ApiResponse {
  return { status: 200, body: sources.evaluateGate() };
}

/**
 * Which mode the recorded operations were carried out in, for the permanent banner.
 *
 * @remarks
 * This is what was *observed*, not what a session is in: the authority on that is `GetOperationMode`
 * on a live server, and this API reads files. So an empty trail answers 'unknown' and a mode the
 * harness does not recognise answers 'unknown' too — never 'Study'. The banner is a safety notice,
 * and a safety notice that guesses the safe answer when it does not know is worse than none.
 *
 * Workshop wins over Study when both appear, for the same reason.
 */
function mode(_query: URLSearchParams, sources: ApiSources): ApiResponse {
  const observed = [...new Set(sources.readAudit().entries.map((entry) => entry.mode))];

  const body: ModeResponse = {
    mode: decideMode(observed),
    observed,
    source: 'the audit trail; the live session is authoritative and is asked through GetOperationMode'
  };

  return { status: 200, body };
}

function decideMode(observed: readonly string[]): string {
  if (observed.length === 0 || observed.some((entry) => !KnownModes.includes(entry))) {
    return 'unknown';
  }

  return observed.includes('Workshop') ? 'Workshop' : 'Study';
}

/**
 * Whether one entry survives the filters.
 *
 * @remarks
 * The target is matched as a case-insensitive substring because it is a block path and the useful
 * question is "everything under this group". The other three are matched exactly: they are closed
 * sets, and a substring match on them would let 'Fail' quietly also mean 'Failed'.
 */
function matchesFilters(entry: AuditEntry, query: URLSearchParams): boolean {
  const target = query.get('target');

  if (target !== null && !entry.target.toLowerCase().includes(target.toLowerCase())) {
    return false;
  }

  return exactly(entry.mode, query.get('mode')) && exactly(entry.tool, query.get('tool')) && exactly(entry.outcome, query.get('outcome'));
}

function exactly(value: string, wanted: string | null): boolean {
  return wanted === null || value === wanted;
}

const Routes: Readonly<Record<string, (query: URLSearchParams, sources: ApiSources) => ApiResponse>> = {
  '/api/runs': runs,
  '/api/metrics': metrics,
  '/api/audit': audit,
  '/api/gate': gate,
  '/api/mode': mode
};

/** What a 404 lists, so a wrong URL says what the right ones are. */
function knownEndpoints(): string[] {
  return [...Object.keys(Routes), '/api/runs/{id}', '/api/iterations/{id}/phases'].sort();
}
