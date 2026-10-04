import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import { fromNodeHeaders, toNodeHandler } from 'better-auth/node';
import type { PlatformAuth } from '../auth/auth.ts';
import type { PlatformDatabase } from '../db/connection.ts';
import { claimIdentity, confirmIdentity } from '../people/identityClaims.ts';
import { readProfile, updateProfile, type ProfileFields } from '../people/profiles.ts';
import { readJsonBody, sendJson } from './httpJson.ts';

/** What the server needs: the sign-in service, the database, and the origin requests must come from. */
export type PlatformServerOptions = {
  readonly auth: PlatformAuth;
  readonly database: PlatformDatabase;
  /** The web's address as the browser sees it, e.g. http://localhost:5173. */
  readonly origin: string;
};

type SignedIn = { readonly userId: string; readonly name: string; readonly email: string; readonly image: string | null };

type Route = (request: IncomingMessage, response: ServerResponse, person: SignedIn, parameter: string) => Promise<void>;

const AuthPrefix = '/api/auth/';
const ConfirmPattern = /^\/api\/identities\/(\d+)\/confirm$/;

/**
 * The platform's HTTP server: Better Auth under `/api/auth/*`, and the platform's own endpoints.
 *
 * @param options The sign-in service, the database and the web's origin.
 * @returns The server, not yet listening.
 * @remarks
 * Thin on purpose. Every decision — who may confirm, what a profile may hold — is made by the
 * functions in `src/people/`, which the tests exercise directly; this only finds the signed-in
 * person and hands the request over.
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
      console.error('Platform request failed:', failure);
      sendJson(response, 500, { error: 'The platform could not answer this request.' });
    });
  });
}

async function handlePlatformRequest(options: PlatformServerOptions, request: IncomingMessage, response: ServerResponse): Promise<void> {
  const found = findRoute(options, request.method ?? 'GET', new URL(request.url ?? '/', 'http://platform').pathname);

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

  await found.route(request, response, person, found.parameter);
}

function findRoute(options: PlatformServerOptions, method: string, path: string): { route: Route; parameter: string } | undefined {
  const routes: Record<string, Route> = {
    'GET /api/me': (_request, response, person) => answerMe(options.database, response, person),
    'PATCH /api/me/profile': (request, response, person) => answerProfileUpdate(options.database, request, response, person),
    'POST /api/identities': (request, response, person) => answerClaim(options.database, request, response, person)
  };

  const exact = routes[`${method} ${path}`];

  if (exact !== undefined) {
    return { route: exact, parameter: '' };
  }

  const confirm = ConfirmPattern.exec(path);

  if (method === 'POST' && confirm?.[1] !== undefined) {
    return { route: (_request, response, person, id) => answerConfirm(options.database, response, person, id), parameter: confirm[1] };
  }

  return undefined;
}

function isSameOriginJson(request: IncomingMessage, origin: string): boolean {
  const isJson = (request.headers['content-type'] ?? '').toLowerCase().startsWith('application/json');

  return isJson && request.headers.origin === origin;
}

async function signedInPerson(auth: PlatformAuth, request: IncomingMessage): Promise<SignedIn | undefined> {
  const session = await auth.api.getSession({ headers: fromNodeHeaders(request.headers) });

  if (session === null) {
    return undefined;
  }

  return { userId: session.user.id, name: session.user.name, email: session.user.email, image: session.user.image ?? null };
}

async function answerMe(database: PlatformDatabase, response: ServerResponse, person: SignedIn): Promise<void> {
  sendJson(response, 200, { user: person, profile: await readProfile(database, person.userId) });
}

async function answerProfileUpdate(
  database: PlatformDatabase,
  request: IncomingMessage,
  response: ServerResponse,
  person: SignedIn
): Promise<void> {
  const fields = (await readJsonBody(request)) as ProfileFields;
  const result = await updateProfile(database, person.userId, fields);

  sendJson(response, result.kind === 'updated' ? 200 : 400, result);
}

async function answerClaim(database: PlatformDatabase, request: IncomingMessage, response: ServerResponse, person: SignedIn): Promise<void> {
  const body = (await readJsonBody(request)) as Record<string, unknown>;
  const text = (field: string): string => (typeof body[field] === 'string' ? (body[field] as string) : '');

  const result = await claimIdentity(database, {
    userId: person.userId,
    organizationId: text('organizationId'),
    stationName: text('stationName'),
    tiaAuthor: text('tiaAuthor')
  });

  sendJson(response, result.kind === 'claimed' ? 201 : 400, result);
}

async function answerConfirm(database: PlatformDatabase, response: ServerResponse, person: SignedIn, identityId: string): Promise<void> {
  const result = await confirmIdentity(database, { confirmerId: person.userId, identityId: Number(identityId) });

  sendJson(response, result.kind === 'confirmed' ? 200 : 403, result);
}
