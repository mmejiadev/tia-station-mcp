import { BadRequest } from './badRequest.ts';

// The largest value of a PostgreSQL integer, which every id here but a change's is.
export const MaximumInteger = 2_147_483_647;

/**
 * A text field of a body.
 *
 * @param body The body.
 * @param name The field.
 * @returns Its text, or empty when it is absent.
 * @throws {BadRequest} It is present and not text.
 */
export function textField(body: Readonly<Record<string, unknown>>, name: string): string {
  const value = body[name];

  if (value === undefined) {
    return '';
  }

  if (typeof value !== 'string') {
    throw new BadRequest(`'${name}' must be text.`);
  }

  return value;
}

/**
 * A field that may be text, cleared with null, or left out.
 *
 * @param body The body.
 * @param name The field.
 * @returns The text, null to clear it, or undefined to leave it as it is.
 * @throws {BadRequest} It is anything else.
 */
export function optionalTextField(body: Readonly<Record<string, unknown>>, name: string): string | null | undefined {
  const value = body[name];

  if (value === undefined || value === null || typeof value === 'string') {
    return value;
  }

  throw new BadRequest(`'${name}' must be text or null.`);
}

/**
 * A folder id from a body: a whole number in the database's range, or null for the top level.
 *
 * @param body The body.
 * @param name The field.
 * @returns The id, null for the top level, or undefined when the field is absent.
 * @throws {BadRequest} It is anything else.
 * @remarks
 * Anything else is refused rather than read as the top level — a folder sent as the text "5" would
 * otherwise move a project to the top without a word — and an id beyond the column's range is
 * refused here rather than sent on to fail in PostgreSQL.
 */
export function folderIdField(body: Readonly<Record<string, unknown>>, name: string): number | null | undefined {
  const value = body[name];

  if (value === undefined || value === null) {
    return value;
  }

  if (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= MaximumInteger) {
    return value;
  }

  throw new BadRequest(`'${name}' must be a folder's number, or null for the top level.`);
}

/**
 * A cursor or a page size from the query string.
 *
 * @param query The query string.
 * @param name The parameter.
 * @param maximum The largest value it may take.
 * @returns The number, or undefined when the parameter is absent.
 * @throws {BadRequest} It is present and not a whole number in range.
 * @remarks
 * A cursor that cannot be read is refused rather than dropped: dropping it returns the first page
 * again, and a client paging with it loops without ever being told.
 */
export function numberParameter(query: URLSearchParams, name: string, maximum: number): number | undefined {
  const value = query.get(name);

  if (value === null) {
    return undefined;
  }

  const parsed = /^\d{1,16}$/.test(value) ? Number(value) : Number.NaN;

  if (Number.isNaN(parsed) || parsed > maximum) {
    throw new BadRequest(`'${name}' must be a whole number no larger than ${maximum}.`);
  }

  return parsed;
}
