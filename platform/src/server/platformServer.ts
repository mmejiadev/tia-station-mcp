import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import { fromNodeHeaders, toNodeHandler } from 'better-auth/node';
import type { PlatformAuth } from '../auth/auth.ts';
import type { PlatformDatabase } from '../db/connection.ts';
import { BadRequest } from './badRequest.ts';
import { sendJson } from './httpJson.ts';
import { findRoute, type Route, type SignedIn } from './router.ts';
import { peopleRoutes } from './routes/peopleRoutes.ts';
import { workspaceRoutes } from './routes/workspaceRoutes.ts';

/** What the server needs: the sign-in service, the database, and the origin requests must come from. */
export type PlatformServerOptions = {
  readonly auth: PlatformAuth;
  readonly database: PlatformDatabase;
  /** The web's address as the browser sees it, e.g. http://localhost:5173. */
  readonly origin: string;
};

const AuthPrefix = '/api/auth/';

/** Every endpoint of the platform's own, all under `/api/platform/`. */
const Routes: readonly Route[] = [...peopleRoutes, ...workspaceRoutes];

/**
 * The platform's HTTP server: Better Auth under `/api/auth/*`, and the platform's own endpoints
 * under `/api/platform/*`.
 *
 * @param options The sign-in service, the database and the web's origin.
 * @returns The server, not yet listening.
 * @remarks
 * Thin on purpose. Every decision — who sees a project, who may confirm an identity — is made by
 * the functions in `src/people/` and `src/workspace/`, which the tests exercise directly; this only
 * finds the signed-in person and hands the request to its route.
 *
 * Everything under one prefix, so that the dashboard's development server forwards two prefixes here
 * and leaves the rest of `/api` to the harness, whatever either adds later.
 *
 * **A request that changes something must come from the web's own origin and be JSON.** Sessions
 * travel in a cookie, which the browser also attaches to a request another site makes; checking
 * the origin is what stops that site acting as the person (cross-site request forgery). Better
 * Auth checks its own endpoints the same way.
 *
 * The person is always the signed-in one: no endpoint takes a user id from the request.
 */
export function createPlatformServer(options: PlatformServerOptions): Server {
  const authHandler = toNodeHandler(options.auth);

  return createServer((request, response) => {
    if ((request.url ?? '').startsWith(AuthPrefix)) {
      void authHandler(request, response);
      return;
    }

    handlePlatformRequest(options, request, response).catch((failure: unknown) => {
      if (failure instanceof BadRequest) {
        sendJson(response, 400, { error: failure.message });
        return;
      }

      console.error('Platform request failed:', failure);
      sendJson(response, 500, { error: 'The platform could not answer this request.' });
    });
  });
}

async function handlePlatformRequest(options: PlatformServerOptions, request: IncomingMessage, response: ServerResponse): Promise<void> {
  const url = new URL(request.url ?? '/', 'http://platform');
  const found = findRoute(Routes, request.method ?? 'GET', url.pathname);

  if (found === undefined) {
    sendJson(response, 404, { error: 'No such endpoint.' });
    return;
  }

  if (request.method !== 'GET' && !isSameOriginJson(request, options.origin)) {
    sendJson(response, 403, { error: 'Changes are accepted only as JSON from the platform itself.' });
    return;
  }

  const person = await signedInPerson(options.auth, request);

  if (person === undefined) {
    sendJson(response, 401, { error: 'Sign in first.' });
    return;
  }

  await found.route.handle({ database: options.database, request, response, person, params: found.params, query: url.searchParams });
}

/**
 * Whether a changing request is JSON from the web's own origin.
 *
 * @remarks
 * A DELETE carries no body, so it is not asked to be JSON — only to come from the platform.
 */
function isSameOriginJson(request: IncomingMessage, origin: string): boolean {
  const isJson = (request.headers['content-type'] ?? '').toLowerCase().startsWith('application/json');

  return request.headers.origin === origin && (isJson || request.method === 'DELETE');
}

async function signedInPerson(auth: PlatformAuth, request: IncomingMessage): Promise<SignedIn | undefined> {
  const session = await auth.api.getSession({ headers: fromNodeHeaders(request.headers) });

  if (session === null) {
    return undefined;
  }

  return { userId: session.user.id, name: session.user.name, email: session.user.email, image: session.user.image ?? null };
}
