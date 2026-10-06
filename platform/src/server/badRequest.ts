/**
 * A request this server cannot read: a body that is not a JSON object, a field of the wrong type, a
 * parameter that does not decode.
 *
 * @remarks
 * Thrown at the HTTP boundary and answered with 400, never logged as a failure of the server: it is
 * the client's mistake, and a log full of them hides the failures that are the server's.
 */
export class BadRequest extends Error {}
