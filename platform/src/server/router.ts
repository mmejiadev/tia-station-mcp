import type { IncomingMessage, ServerResponse } from 'node:http';
import type { PlatformDatabase } from '../db/connection.ts';
import type { WorkspaceResult } from '../workspace/workspaceResult.ts';
import { BadRequest } from './badRequest.ts';
import { sendJson } from './httpJson.ts';
import { MaximumInteger } from './requestFields.ts';

/** The signed-in person a request is made by. Never taken from the request itself. */
export type SignedIn = {
  readonly userId: string;
  readonly name: string;
  readonly email: string;
  readonly image: string | null;
};

/** Everything a route needs to answer one request. */
export type RouteContext = {
  readonly database: PlatformDatabase;
  readonly request: IncomingMessage;
  readonly response: ServerResponse;
  readonly person: SignedIn;
  /** The named groups of the route's pattern, as text. */
  readonly params: Readonly<Record<string, string>>;
  readonly query: URLSearchParams;
};

/** One endpoint: a method, a path pattern with named groups, and what answers it. */
export type Route = {
  readonly method: 'GET' | 'POST' | 'PATCH' | 'DELETE';
  readonly pattern: RegExp;
  readonly handle: (context: RouteContext) => Promise<void>;
};

/** The HTTP status each kind of refusal is answered with. */
const RefusalStatus = { invalid: 400, forbidden: 403, 'not-found': 404 } as const;

/**
 * Finds the route for a request.
 *
 * @param routes The table.
 * @param method The request's method.
 * @param path The request's path, without the query.
 * @returns The route and its parameters, or undefined.
 */
export function findRoute(
  routes: readonly Route[],
  method: string,
  path: string
): { route: Route; params: Record<string, string> } | undefined {
  for (const route of routes) {
    const match = route.method === method ? route.pattern.exec(path) : null;

    if (match !== null) {
      return { route, params: { ...(match.groups ?? {}) } };
    }
  }

  return undefined;
}

/**
 * Answers with a workspace result: its value, or the refusal with its status.
 *
 * @param response The response.
 * @param result What the request came to.
 * @param doneStatus The status for a result carrying a value; 200 unless something was created.
 */
export function sendResult<T>(response: ServerResponse, result: WorkspaceResult<T>, doneStatus = 200): void {
  if (result.kind === 'done') {
    sendJson(response, doneStatus, result.value);
    return;
  }

  sendJson(response, RefusalStatus[result.refusal], { error: result.reason });
}

/**
 * A path or query parameter as a whole number, or undefined when it is not one.
 *
 * @param value The parameter.
 * @param maximum The largest value the column it is compared with can hold.
 * @returns The number.
 * @remarks
 * A number beyond the column's range is "not a number" here, so the id it names is answered as one
 * that does not exist — sending it on makes PostgreSQL fail, and a 500 for a client's typo.
 */
export function wholeNumber(value: string | null | undefined, maximum: number = MaximumInteger): number | undefined {
  if (value === null || value === undefined || !/^\d{1,16}$/.test(value)) {
    return undefined;
  }

  const parsed = Number(value);

  return parsed <= maximum ? parsed : undefined;
}

/**
 * A path parameter, URL-decoded.
 *
 * @param value The parameter as it arrived.
 * @returns The decoded text.
 * @throws {BadRequest} It does not decode.
 */
export function decodedParameter(value: string | undefined): string {
  try {
    return decodeURIComponent(value ?? '');
  } catch {
    throw new BadRequest('A parameter of the path could not be decoded.');
  }
}
