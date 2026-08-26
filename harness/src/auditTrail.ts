import { readFileSync } from 'node:fs';

/**
 * How the server recorded the fate of one planned change.
 *
 * @remarks
 * The four the server writes, listed rather than left open. An outcome outside this set is not a
 * fifth kind of success: it is a record this harness does not understand, and the workshop gate
 * counts it as a silent failure. That is the whole point of criterion 2.
 */
export type AuditOutcome = 'Planned' | 'Applied' | 'Refused' | 'Failed';

const KnownOutcomes: readonly string[] = ['Planned', 'Applied', 'Refused', 'Failed'];

/** One line of the audit trail. */
export type AuditEntry = {
  readonly timestamp: string;
  readonly planId: string;
  /** 'Study' or 'Workshop'. Text rather than a union: an unknown mode must be readable to be refused. */
  readonly mode: string;
  readonly tool: string;
  readonly target: string;
  /** Where the previous state was saved, or empty when the change saved nothing. */
  readonly backupPath: string;
  readonly origin: string;
  /** Text, for the same reason as mode: an unrecognised outcome has to survive being read. */
  readonly outcome: string;
  readonly detail: string;
};

/**
 * What reading an audit trail produced, unreadable lines included.
 *
 * @remarks
 * Unreadable lines are returned rather than thrown or skipped. Skipping them would let a corrupt
 * trail look complete, which is the one thing an audit may never do; throwing would stop the gate
 * from reporting the corruption as the criterion failure it is.
 */
export type AuditReadResult = {
  readonly entries: readonly AuditEntry[];
  /** Line numbers, counting from one, that could not be read as an entry. */
  readonly unreadableLines: readonly number[];
};

/** Whether an outcome is one this harness knows how to judge. */
export function isKnownOutcome(outcome: string): outcome is AuditOutcome {
  return KnownOutcomes.includes(outcome);
}

/**
 * Reads an audit trail from disk.
 *
 * @param path The trail, one JSON object per line.
 * @returns Every entry, and the line numbers of anything that was not one.
 * @remarks
 * A missing file is not an empty trail and is not treated as one: the caller is asking about writes
 * that were recorded, and "there is no file" means nobody knows, not "there were none". It throws.
 */
export function readAuditTrail(path: string): AuditReadResult {
  const contents = readFileSync(path, 'utf8');
  const entries: AuditEntry[] = [];
  const unreadableLines: number[] = [];

  contents.split('\n').forEach((line, index) => {
    if (line.trim().length === 0) {
      return;
    }

    const entry = parseEntry(line);

    if (entry === undefined) {
      unreadableLines.push(index + 1);

      return;
    }

    entries.push(entry);
  });

  return { entries, unreadableLines };
}

/** One line, or nothing when it is not an entry. */
function parseEntry(line: string): AuditEntry | undefined {
  let parsed: unknown;

  try {
    parsed = JSON.parse(line);
  } catch {
    return undefined;
  }

  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    return undefined;
  }

  const raw = parsed as Record<string, unknown>;

  // Only the fields the gate reasons about are required. An entry missing one of them cannot be
  // judged, so it counts as unreadable rather than as an entry with blanks in it.
  if (!hasText(raw, 'planId') || !hasText(raw, 'mode') || !hasText(raw, 'tool') || !hasText(raw, 'outcome')) {
    return undefined;
  }

  return {
    timestamp: text(raw, 'timestamp'),
    planId: text(raw, 'planId'),
    mode: text(raw, 'mode'),
    tool: text(raw, 'tool'),
    target: text(raw, 'target'),
    backupPath: text(raw, 'backupPath'),
    origin: text(raw, 'origin'),
    outcome: text(raw, 'outcome'),
    detail: text(raw, 'detail')
  };
}

function hasText(raw: Record<string, unknown>, field: string): boolean {
  return typeof raw[field] === 'string' && (raw[field] as string).length > 0;
}

function text(raw: Record<string, unknown>, field: string): string {
  return typeof raw[field] === 'string' ? (raw[field] as string) : '';
}
