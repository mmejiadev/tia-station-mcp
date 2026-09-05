import { createHash } from 'node:crypto';

/**
 * The hash recorded for the first entry of a chain, which has no predecessor.
 *
 * @remarks
 * `AuditChain.Root` on the C# side. Empty rather than a hash of nothing, so that a trail's first
 * line is recognisable as a first line.
 */
export const AuditChainRoot = '';

/**
 * The order the chain hashes an entry's values in, per version of the canonical form.
 *
 * @remarks
 * The same table, in the same order, as `JsonlAuditTrail.ChainedFieldsByVersion`. The two are
 * separate declarations in two languages and nothing but this comment keeps them together:
 * reordering one makes every hash already written report as tampered with, which is the worst way
 * to find out that they have drifted apart.
 *
 * **A published list may never be edited - a field is added by adding a version.** The hash covers
 * the values in order and nothing else, so an eleventh value changes the hash of an entry written
 * with ten. Version 2 added `documentation` on 2026-09-05; version 1 stays because the trails
 * written before it must keep verifying.
 */
const ChainedFieldsByVersion: Readonly<Record<string, readonly string[]>> = {
  '1': ['timestamp', 'planId', 'mode', 'tool', 'target', 'value', 'backupPath', 'origin', 'outcome', 'detail'],
  '2': [
    'timestamp',
    'planId',
    'mode',
    'tool',
    'target',
    'value',
    'backupPath',
    'origin',
    'outcome',
    'detail',
    'documentation'
  ]
};

/** The version assumed for a line that names none, written before versioning existed. */
const OriginalChainVersion = '1';

/**
 * The characters `System.Text.Json` writes as a two-character escape.
 *
 * @remarks
 * The escaping in this file exists for one reason: the hash is taken over a string the server built
 * with `JsonSerializer.Serialize`, so recomputing it here means producing the *same bytes*.
 * `JSON.stringify` does not. It leaves `<`, `>`, `&`, `'`, `"`, `+`, a backtick and every non-ASCII
 * character as themselves, where the .NET encoder escapes all of them — and every real entry
 * carries a `+` in its timestamp offset, so the naive version reports the whole trail as forged on
 * its very first line.
 *
 * These values were not reasoned out. They were read off `System.Text.Json` by serialising every
 * code point from U+0000 to U+00FF through the assembly the server itself is built against, and the
 * tests hold the vectors that came back.
 */
const ShortEscapes = new Map<number, string>([
  [0x08, '\\b'],
  [0x09, '\\t'],
  [0x0a, '\\n'],
  [0x0c, '\\f'],
  [0x0d, '\\r'],
  [0x5c, '\\\\']
]);

/** Printable ASCII that .NET escapes anyway, because it is dangerous in HTML: " & ' + < > and ` */
const EscapedPrintable = new Set<number>([0x22, 0x26, 0x27, 0x2b, 0x3c, 0x3e, 0x60]);

const FirstPrintable = 0x20;
const LastPrintable = 0x7e;

const StrippedChain =
  'this entry carries no chain fields although the chain had already begun - they were stripped from it';

/** What a check of the trail's hash chain found. */
export type AuditChainReport = {
  /** How many entries carry chain fields and were verified against their predecessor. */
  readonly chained: number;
  /**
   * How many entries precede the chain and cannot be attested.
   *
   * @remarks
   * Reported rather than hidden. Chaining was added to a trail that already held thousands of
   * entries, and a chain can only vouch for what it covered. Counting them is the honest
   * alternative to either deleting that history or implying it is verified.
   */
  readonly unchained: number;
  /** Where the chain first fails, counting lines from one. Zero when it holds. */
  readonly brokenAtLine: number;
  /** Why it fails, in a sentence a person can act on. Empty when it holds. */
  readonly reason: string;
  readonly intact: boolean;
};

/**
 * Builds the exact string the server hashed for one entry.
 *
 * @param sequence The entry's position, counting from one.
 * @param previousHash The previous entry's hash, or {@link AuditChainRoot} for the first.
 * @param fields The entry's values, in the order of `ChainedFields`.
 * @returns A form that depends on the values and on nothing else.
 * @remarks
 * A JSON array, so the length of every value is encoded by the escaping and no value can be split
 * or joined with its neighbour. Concatenating with a separator would let a field containing that
 * separator impersonate two fields, which is how a naive chain is forged without breaking a hash.
 */
export function canonicalForm(sequence: number, previousHash: string, fields: readonly string[]): string {
  const parts = [String(sequence), previousHash, ...fields];

  return `[${parts.map(quote).join(',')}]`;
}

/**
 * Computes the hash that links one entry to its predecessor.
 *
 * @param sequence The entry's position, counting from one.
 * @param previousHash The previous entry's hash.
 * @param fields The entry's values, in the order of `ChainedFields`.
 * @returns The hash, lowercase hexadecimal, as the server writes it.
 */
export function linkHash(sequence: number, previousHash: string, fields: readonly string[]): string {
  return createHash('sha256').update(canonicalForm(sequence, previousHash, fields), 'utf8').digest('hex');
}

/**
 * Checks that every chained entry still matches the one before it.
 *
 * @param lines The trail, one line per element, blank lines included.
 * @returns What the check found, including how much of the file predates chaining.
 * @remarks
 * Read-only and side-effect free: it answers a question and repairs nothing. Anything here that
 * "fixed" a broken chain would destroy the only evidence that something was edited.
 *
 * **This detects tampering; it does not prevent it**, and it does not stop somebody who recomputes
 * the whole chain. Preventing that needs a key this machine does not have.
 *
 * A line that is not JSON is skipped rather than reported as a break. Criterion 2 of the gate
 * already counts unreadable lines and fails on them, and naming a corrupt line a forgery would name
 * the wrong crime.
 */
export function verifyAuditChain(lines: readonly string[]): AuditChainReport {
  let previousHash = AuditChainRoot;
  let expected = 0;
  let chained = 0;
  let unchained = 0;

  for (let index = 0; index < lines.length; index++) {
    const record = readRecord(lines[index] ?? '');

    if (record === undefined) {
      continue;
    }

    // An unchained line is history from before chaining existed, which can only sit at the top of
    // the file. One *after* the chain began is a line somebody removed the hash from, and that is
    // exactly how an edited entry would otherwise pass itself off as unattested history.
    if (record['hash'] === undefined && expected > 0) {
      return broken(chained, unchained, index + 1, StrippedChain);
    }

    if (record['hash'] === undefined) {
      unchained++;

      continue;
    }

    expected++;

    const failure = breakReason(record, expected, previousHash);

    if (failure.length > 0) {
      return broken(chained, unchained, index + 1, failure);
    }

    chained++;
    previousHash = field(record, 'hash');
  }

  return { chained, unchained, brokenAtLine: 0, reason: '', intact: true };
}

/**
 * A one-line summary of a check.
 *
 * @param report What the check found.
 * @returns The description, for a person reading a verdict.
 */
export function describeAuditChain(report: AuditChainReport): string {
  if (!report.intact) {
    return `the audit chain breaks at line ${report.brokenAtLine}: ${report.reason}`;
  }

  if (report.unchained === 0) {
    return `the chain is intact over ${report.chained} entr(ies)`;
  }

  return (
    `the chain is intact over ${report.chained} entr(ies), and ${report.unchained} earlier ` +
    'entr(ies) predate chaining and are not attested'
  );
}

/**
 * Why this line does not follow the previous one, or empty when it does.
 *
 * @param record The line's fields.
 * @param expectedSequence The position this line has to claim.
 * @param previousHash The hash the line has to point back to.
 * @returns The reason, or an empty string.
 * @remarks
 * Three ways to fail, and each names a different tampering. A wrong sequence means a line was
 * removed or inserted; a wrong predecessor means the file was cut and re-joined; a hash that does
 * not recompute means the entry's own values were edited.
 */
function breakReason(record: Record<string, string>, expectedSequence: number, previousHash: string): string {
  if (field(record, 'seq') !== String(expectedSequence)) {
    return `expected sequence ${expectedSequence}, found '${field(record, 'seq')}' - an entry was removed or inserted`;
  }

  if (field(record, 'prev') !== previousHash) {
    return 'this entry does not point back to the one before it - the file was cut and re-joined';
  }

  const version = field(record, 'v').length > 0 ? field(record, 'v') : OriginalChainVersion;
  const chainedFields = ChainedFieldsByVersion[version];

  if (chainedFields === undefined) {
    return `this entry names chain version '${version}', which this reader does not know - it was written by a newer one`;
  }

  const values = chainedFields.map((name) => field(record, name));

  if (linkHash(expectedSequence, previousHash, values) !== field(record, 'hash')) {
    return "the entry's own values do not match its hash - it was edited after it was written";
  }

  return '';
}

function broken(chained: number, unchained: number, brokenAtLine: number, reason: string): AuditChainReport {
  return { chained, unchained, brokenAtLine, reason, intact: false };
}

/**
 * One line as the fields the server wrote, or nothing when it is not one.
 *
 * @param line The raw line.
 * @returns Its fields, or undefined for a blank or unreadable line.
 */
function readRecord(line: string): Record<string, string> | undefined {
  if (line.trim().length === 0) {
    return undefined;
  }

  let parsed: unknown;

  try {
    parsed = JSON.parse(line);
  } catch {
    return undefined;
  }

  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    return undefined;
  }

  return textOnly(parsed as Record<string, unknown>);
}

/**
 * The record with every value as text, or nothing when one of them is not.
 *
 * @param raw The parsed line.
 * @returns The fields, or undefined when the line cannot be one the server wrote.
 * @remarks
 * A value that is not text makes the whole line unreadable, because the server writes a dictionary
 * of strings and .NET refuses to read anything else back into one. A null is the one exception: it
 * reads as the empty string on both sides.
 */
function textOnly(raw: Record<string, unknown>): Record<string, string> | undefined {
  const record: Record<string, string> = {};

  for (const [key, value] of Object.entries(raw)) {
    if (value !== null && typeof value !== 'string') {
      return undefined;
    }

    record[key] = value ?? '';
  }

  return record;
}

function field(record: Record<string, string>, name: string): string {
  return record[name] ?? '';
}

/** One value, escaped the way `System.Text.Json` escapes it. */
function quote(value: string): string {
  let text = '"';

  for (let index = 0; index < value.length; index++) {
    text += escapeUnit(value.charCodeAt(index));
  }

  return `${text}"`;
}

/**
 * One UTF-16 code unit, escaped or not.
 *
 * @param code The code unit.
 * @returns What .NET would write for it.
 * @remarks
 * Per code unit rather than per code point, because .NET escapes a surrogate pair as two `\uXXXX`
 * escapes rather than as one. The hexadecimal is uppercase for the same reason everything else here
 * is what it is: that is what the bytes being verified contain.
 */
function escapeUnit(code: number): string {
  const short = ShortEscapes.get(code);

  if (short !== undefined) {
    return short;
  }

  if (code >= FirstPrintable && code <= LastPrintable && !EscapedPrintable.has(code)) {
    return String.fromCharCode(code);
  }

  return `\\u${code.toString(16).toUpperCase().padStart(4, '0')}`;
}
