/** Why a workspace request was not carried out. */
export type RefusalKind =
  /** The input does not describe a valid request: an empty name, a folder inside itself. */
  | 'invalid'
  /** The thing does not exist — or exists where the person may not look, which they are not told. */
  | 'not-found'
  /** The person can see it but their role does not allow this. */
  | 'forbidden';

/** What a workspace request came to: its value, or why it was refused. */
export type WorkspaceResult<T> =
  | { readonly kind: 'done'; readonly value: T }
  | { readonly kind: 'refused'; readonly refusal: RefusalKind; readonly reason: string };

/**
 * A result carrying a value.
 *
 * @param value What the request produced.
 * @returns The result.
 */
export function done<T>(value: T): WorkspaceResult<T> {
  return { kind: 'done', value };
}

/**
 * A refusal.
 *
 * @param refusal Which kind.
 * @param reason A sentence the person can act on.
 * @returns The result.
 * @remarks
 * Refusals are results, not exceptions, for the reason the governance layer gives: a request the
 * rules turn down is the system working, and an exception would be reported as a failure to retry.
 */
export function refused<T>(refusal: RefusalKind, reason: string): WorkspaceResult<T> {
  return { kind: 'refused', refusal, reason };
}
