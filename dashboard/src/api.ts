import type {
  AuditResponse,
  IterationPhasesResponse,
  MetricsResponse,
  ModeResponse,
  RunDetailResponse,
  RunsResponse
} from '../../harness/src/dashboardApi.ts';
import type { ChatTurn } from '../../harness/src/copilotChat.ts';
import type { ChatResponse, CopilotStatus } from '../../harness/src/copilotEndpoint.ts';
import type { GateVerdict } from '../../harness/src/gate.ts';

export type {
  AuditResponse,
  IterationPhasesResponse,
  MetricsResponse,
  ModeResponse,
  RunDetailResponse,
  RunsResponse,
  GateVerdict,
  ChatTurn,
  ChatResponse,
  CopilotStatus
};

/**
 * Reads one endpoint of the harness API.
 *
 * @param path The endpoint, with its query string.
 * @returns The body, typed by the caller from the contract the API exports.
 * @remarks
 * The types come from `harness/src/` rather than from a copy kept here. They are one contract, and a
 * front end with its own description of the same payload keeps compiling long after the API stopped
 * sending it — and then renders `undefined` into a number somebody is about to make a decision on.
 *
 * A failed request throws. It must never resolve to an empty list: an audit table that shows nothing
 * because the trail could not be read is claiming that nothing was ever changed.
 */
export async function read<T>(path: string): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' } });
  const body: unknown = await response.json().catch(() => undefined);

  if (!response.ok) {
    throw new Error(errorFrom(body) ?? `${path} answered ${response.status}.`);
  }

  return body as T;
}

/** Every run, newest first. */
export function readRuns(): Promise<RunsResponse> {
  return read<RunsResponse>('/api/runs');
}

/** One run, with its iterations and phase timings. */
export function readRun(runId: number): Promise<RunDetailResponse> {
  return read<RunDetailResponse>(`/api/runs/${runId}`);
}

/**
 * The phases one iteration has finished.
 *
 * @remarks
 * Finished, not running. A phase reaches the store when it ends, so this never answers "what is it
 * doing right now" — which is why the view built on it does not claim to.
 */
export function readIterationPhases(iterationId: number): Promise<IterationPhasesResponse> {
  return read<IterationPhasesResponse>(`/api/iterations/${iterationId}/phases`);
}

/**
 * The measurements, each with the sample size behind it.
 *
 * @param generator Which generator to count, or undefined for every one in the store.
 * @remarks
 * The parameter is not a convenience. The stub expander and a model are two different experiments,
 * and a rate computed over both is a number about neither — so the response always names the
 * generator it is about, including when the answer is 'all'.
 */
export function readMetrics(generator?: string): Promise<MetricsResponse> {
  const suffix = generator === undefined ? '' : `?generator=${encodeURIComponent(generator)}`;

  return read<MetricsResponse>(`/api/metrics${suffix}`);
}

/** The audit trail, filtered by whichever of the filters are set. */
export function readAudit(filters: Readonly<Record<string, string>>): Promise<AuditResponse> {
  const query = new URLSearchParams(Object.entries(filters).filter(([, value]) => value.length > 0));
  const suffix = query.size === 0 ? '' : `?${query.toString()}`;

  return read<AuditResponse>(`/api/audit${suffix}`);
}

/** The five workshop criteria and the verdict. */
export function readGate(): Promise<GateVerdict> {
  return read<GateVerdict>('/api/gate');
}

/** What the permanent banner shows. */
export function readMode(): Promise<ModeResponse> {
  return read<ModeResponse>('/api/mode');
}

/** Whether there is a copilot to talk to, and which model answers. */
export function readCopilotStatus(): Promise<CopilotStatus> {
  return read<CopilotStatus>('/api/copilot');
}

/**
 * Puts one question to the copilot.
 *
 * @param question What was typed.
 * @param history Everything said so far in this conversation.
 * @returns The answer, and what the turn cost.
 * @remarks
 * The only request this dashboard makes that is not a GET, and the only one that carries a body. It
 * changes nothing: the endpoint behind it reads the store, asks a model with no tools, and returns
 * what came back. The conversation lives in this tab and nowhere else - the server keeps none of it,
 * which is why the history is sent every time.
 */
export async function askCopilot(question: string, history: readonly ChatTurn[]): Promise<ChatResponse> {
  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ question, history })
  });

  const body: unknown = await response.json().catch(() => undefined);

  if (!response.ok) {
    throw new Error(errorFrom(body) ?? `The copilot answered ${response.status}.`);
  }

  return body as ChatResponse;
}

/** The message the API sent with a refusal, when it sent one. */
function errorFrom(body: unknown): string | undefined {
  if (typeof body !== 'object' || body === null) {
    return undefined;
  }

  const error = (body as Record<string, unknown>)['error'];

  return typeof error === 'string' ? error : undefined;
}
