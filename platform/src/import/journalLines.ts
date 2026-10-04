import { createHash } from 'node:crypto';

/** One line of a journal, read. */
export type JournalLine<TRecord> = {
  readonly lineNumber: number;
  /** A hash of the line, which keys it: the journals have no chain, and the line is the record. */
  readonly entryKey: string;
  readonly record: TRecord;
};

/** What reading a journal produced: every record, or the first line that is not one. */
export type JournalReadResult<TRecord> =
  | { readonly kind: 'records'; readonly lines: readonly JournalLine<TRecord>[] }
  | { readonly kind: 'refused'; readonly line: number; readonly reason: string };

/**
 * Reads a journal the server wrote, one JSON object per line.
 *
 * @param lines The file, one element per line, blank lines included.
 * @param toRecord Turns one parsed object into a record, or undefined when it is not one.
 * @param kind What the journal records, for the reason a line is refused.
 * @returns Every record, or the first line that is not one.
 * @remarks
 * A line that is not a record refuses the whole journal, as an unreadable audit line does: an import
 * that skipped it would present a gap as a complete history.
 */
export function readJournal<TRecord>(
  lines: readonly string[],
  toRecord: (raw: Record<string, unknown>) => TRecord | undefined,
  kind: string
): JournalReadResult<TRecord> {
  const read: JournalLine<TRecord>[] = [];

  for (let index = 0; index < lines.length; index++) {
    const line = (lines[index] ?? '').trim();

    if (line.length === 0) {
      continue;
    }

    const raw = parseObject(line);
    const record = raw === undefined ? undefined : toRecord(raw);

    if (record === undefined) {
      return { kind: 'refused', line: index + 1, reason: `this line is not a ${kind} record` };
    }

    read.push({ lineNumber: index + 1, entryKey: createHash('sha256').update(line, 'utf8').digest('hex'), record });
  }

  return { kind: 'records', lines: read };
}

/**
 * A field's text, or empty when it has none.
 *
 * @param raw The parsed line.
 * @param field The field.
 * @returns Its text.
 */
export function textOf(raw: Record<string, unknown>, field: string): string {
  return typeof raw[field] === 'string' ? (raw[field] as string) : '';
}

/**
 * A field's whole number, or undefined when it has none.
 *
 * @param raw The parsed line.
 * @param field The field.
 * @returns Its value.
 */
export function wholeNumberOf(raw: Record<string, unknown>, field: string): number | undefined {
  const value = raw[field];

  return typeof value === 'number' && Number.isInteger(value) ? value : undefined;
}

function parseObject(line: string): Record<string, unknown> | undefined {
  let parsed: unknown;

  try {
    parsed = JSON.parse(line);
  } catch {
    return undefined;
  }

  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    return undefined;
  }

  return parsed as Record<string, unknown>;
}
