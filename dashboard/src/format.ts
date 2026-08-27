/**
 * Every number the dashboard prints goes through here.
 *
 * @remarks
 * It is a module of pure functions and no React on purpose. These are the rules the roadmap states
 * about how a measurement may be reported — never a bare percentage, never a mean without its
 * sample size — and a rule that only exists inside a component is a rule that is checked by looking
 * at a browser. Here it is checked by `npm test`.
 */

/**
 * A count out of a total, with the percentage after it rather than instead of it.
 *
 * @param count How many.
 * @param total Out of how many.
 * @returns Something like `9 of 11 (82%)`.
 * @remarks
 * A total of zero prints as "none attempted" rather than as 0% or NaN%. Zero out of zero is not a
 * failure rate of nothing: it is a question nobody asked yet, and drawing it as 0% puts a bar on a
 * chart for work that was never done.
 */
export function formatRate(count: number, total: number): string {
  if (total === 0) {
    return 'none attempted';
  }

  return `${count} of ${total} (${Math.round((count / total) * 100)}%)`;
}

/**
 * A mean, with how many samples it was taken over.
 *
 * @param mean The value, or undefined when there was nothing to average.
 * @param samples How many values went into it.
 * @returns Something like `2.0 over 9`, or a sentence saying there is nothing to average.
 */
export function formatMean(mean: number | undefined, samples: number): string {
  if (mean === undefined) {
    return 'nothing to average';
  }

  return `${mean.toFixed(1)} over ${samples}`;
}

/**
 * A duration, in the unit that makes it readable.
 *
 * @param milliseconds How long.
 * @returns `611 ms`, `11.2 s`, or `2 m 01 s`.
 * @remarks
 * Three units because the loop spans three orders of magnitude: generating takes milliseconds,
 * downloading takes seconds, and a whole run takes minutes. One unit for all of them would print
 * either 0.002 s or 121000 ms, and neither is a number anybody reads.
 */
export function formatDuration(milliseconds: number): string {
  if (milliseconds < 1000) {
    return `${Math.round(milliseconds)} ms`;
  }

  if (milliseconds < 60_000) {
    return `${(milliseconds / 1000).toFixed(1)} s`;
  }

  const minutes = Math.floor(milliseconds / 60_000);
  const seconds = Math.round((milliseconds % 60_000) / 1000);

  return `${minutes} m ${String(seconds).padStart(2, '0')} s`;
}

/**
 * When something happened, in the reader's own time zone.
 *
 * @param epochMilliseconds The instant, as the store records it.
 * @remarks
 * The store keeps epoch milliseconds precisely so they can be subtracted and sorted; converting to
 * local time belongs here, at the last possible moment, and nowhere earlier.
 */
export function formatInstant(epochMilliseconds: number): string {
  return new Date(epochMilliseconds).toLocaleString();
}

/**
 * How long a run lasted, or that it has not finished.
 *
 * @param startedAt When it began.
 * @param endedAt When it ended, or undefined.
 * @remarks
 * An unfinished run is said to be unfinished rather than given a duration up to now. The two are
 * different facts: one run is still going, another was interrupted and never will, and inventing a
 * duration for either would hide which.
 */
export function formatSpan(startedAt: number, endedAt: number | undefined): string {
  if (endedAt === undefined) {
    return 'did not finish';
  }

  return formatDuration(endedAt - startedAt);
}

/**
 * An instant the server wrote as text, in the reader's own time zone.
 *
 * @param timestamp What the audit trail recorded, which is an ISO 8601 string from the C# side.
 * @returns The same instant, local, or the original text when it cannot be read as one.
 * @remarks
 * The fallback is the point. A timestamp this cannot parse still has to be shown exactly as it was
 * recorded — an audit line rendered as "Invalid Date" is a line whose evidence has been destroyed by
 * the thing displaying it.
 */
export function formatRecordedInstant(timestamp: string): string {
  const parsed = new Date(timestamp);

  if (Number.isNaN(parsed.getTime())) {
    return timestamp;
  }

  return parsed.toLocaleString();
}
