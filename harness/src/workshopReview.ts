import { readFileSync } from 'node:fs';
import type { ReviewRecord } from './gate.ts';

/**
 * The one line the review file has to contain.
 *
 * @remarks
 * A date and a name, and nothing else is parsed. The file is meant to be read by a person — notes
 * from the review belong in it — so the format is one recognisable line rather than a schema.
 */
const ReviewLine = /^reviewed:\s*(\d{4}-\d{2}-\d{2})\s+by\s+(.+?)\s*$/m;

/**
 * Reads the record of the in-person design review, if there is one.
 *
 * @param path The review file.
 * @returns The record, or undefined when there is no file or no review line in it.
 * @remarks
 * Criterion 5 of the workshop gate is the one no measurement can answer: a person has to have
 * looked at the design in the same room as the machine. This reads the fact that it happened; it
 * cannot and does not check that it did.
 *
 * A missing file is not an error. It is the normal state before the review has happened, and the
 * gate reports it as the criterion not being met — which is the correct answer, arrived at without
 * anything having to fail.
 */
export function readReviewRecord(path: string): ReviewRecord | undefined {
  const contents = readTextOrNothing(path);

  if (contents === undefined) {
    return undefined;
  }

  const matched = ReviewLine.exec(contents);

  if (matched === null) {
    return undefined;
  }

  return { date: matched[1] ?? '', reviewer: matched[2] ?? '' };
}

/**
 * The file's text, or nothing when it is not there.
 *
 * @remarks
 * Only a missing file is treated as "no review". A file that exists and cannot be read is a
 * different situation — a permission problem, a directory where a file was meant to be — and
 * swallowing that would report "no review yet" for a review that may well be recorded.
 */
function readTextOrNothing(path: string): string | undefined {
  try {
    return readFileSync(path, 'utf8');
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      return undefined;
    }

    throw error;
  }
}
