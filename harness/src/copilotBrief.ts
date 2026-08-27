import type { AuditReadResult } from './auditTrail.ts';
import type { DashboardStore } from './dashboardApi.ts';
import type { GateVerdict } from './gate.ts';

/**
 * How many of the most recent runs the brief describes one by one.
 *
 * @remarks
 * Ten. The point of the brief is that it is small enough to send with every question, and a store
 * with thirty-nine runs in it - or, later, several hundred - would otherwise put the bill up on
 * every turn for detail nobody asked about. The totals above the list still cover all of them, so
 * the brief never implies that ten is all there is.
 */
const DescribedRuns = 10;

/**
 * How many audit outcomes the brief counts.
 *
 * @remarks
 * The trail is 2291 entries today and grows with every write. It is summarised as counts rather
 * than quoted, because a question about the trail is nearly always "how many, and were any
 * refused" - and quoting two thousand lines into a prompt to answer that is the difference between
 * a chat turn costing a cent and costing a euro.
 */
const CountedOutcomes = 8;

/**
 * Everything the copilot is allowed to know, as text.
 *
 * @remarks
 * The brief is the copilot's entire world, and that is deliberate. It is assembled here, from the
 * same reader the GET endpoints serve, so a number in an answer and the same number in a table come
 * from one query and cannot disagree. Anything absent from this string is something the copilot has
 * to say it does not know - which is only a usable rule because what is in it is written down here,
 * where it can be read, rather than gathered at the call site.
 *
 * It carries no secret. Every line of it is already on the dashboard's own pages, which is what
 * makes sending it to a model a thing that needs no separate decision each time.
 */
export type CopilotBrief = {
  /** The text sent to the model. */
  readonly text: string;
  /** How large it is, so a caller can say what a turn will cost before making it. */
  readonly characters: number;
};

/** Where the brief is assembled from: the same sources the read-only endpoints already use. */
export type BriefSources = {
  readonly reader: DashboardStore;
  readonly readAudit: () => AuditReadResult;
  readonly evaluateGate: () => GateVerdict;
};

/**
 * Assembles what the copilot knows, from what was recorded.
 *
 * @param sources The store, the trail and the gate.
 * @returns The brief, ready to send.
 * @remarks
 * Read afresh for every question rather than built once at startup. Somebody asking "how is the run
 * going" while a run is going is the case this view exists for, and a brief captured when the server
 * started would answer that with the state of an hour ago and sound exactly as confident.
 */
export function buildBrief(sources: BriefSources): CopilotBrief {
  const text = [
    describeRuns(sources.reader),
    describeSpecifications(sources.reader),
    describePhases(sources.reader),
    describeGate(sources.evaluateGate()),
    describeAudit(sources.readAudit())
  ].join('\n\n');

  return { text, characters: text.length };
}

/** The totals, then the most recent runs one by one. */
function describeRuns(reader: DashboardStore): string {
  const runs = reader.runs();
  const finished = runs.filter((run) => run.outcome !== undefined);
  const lines = runs.slice(0, DescribedRuns).map(describeOneRun);

  return [
    '## Runs',
    `${runs.length} run(s) recorded, ${finished.length} of them finished.`,
    runs.length === 0
      ? 'Nothing has been recorded in this store yet.'
      : `The ${lines.length} most recent, newest first:`,
    ...lines
  ].join('\n');
}

function describeOneRun(run: {
  readonly runId: number;
  readonly generator: string;
  readonly outcome: string | undefined;
  readonly startedAt: number;
  readonly specifications: number;
  readonly cleanCompilations: number;
  readonly passed: number;
}): string {
  // Named as unfinished rather than given a guessed outcome. A run with none is either in progress
  // or was interrupted, and the store cannot tell which - so saying which would be an invention.
  const outcome = run.outcome ?? 'no outcome recorded (still going, or interrupted)';

  return (
    `- run ${run.runId}, generator ${run.generator}, started ${new Date(run.startedAt).toISOString()}, ` +
    `${run.specifications} specification(s), ${run.cleanCompilations} compiled cleanly, ` +
    `${run.passed} passed on a simulated CPU, outcome: ${outcome}`
  );
}

/** Per specification, each rate beside the sample size that produced it. */
function describeSpecifications(reader: DashboardStore): string {
  const statistics = reader.specificationStatistics();

  if (statistics.length === 0) {
    return '## Specifications\n\nNo specification has been attempted in this store.';
  }

  const lines = statistics.map((entry) => {
    // Undefined and not zero. A specification that never reached a clean compilation has no mean,
    // and a printed 0 would read as the fastest one on the page.
    const mean =
      entry.meanIterationsToCleanCompilation === undefined
        ? 'no clean compilation, so no mean'
        : `${entry.meanIterationsToCleanCompilation.toFixed(2)} mean iterations to a clean compilation`;

    return (
      `- ${entry.specification}: attempted ${entry.attempts} time(s), ${entry.cleanCompilations} ` +
      `compiled cleanly, ${entry.passed} passed, ${mean}`
    );
  });

  return ['## Specifications', 'Every rate below is over the attempts named beside it.', ...lines].join('\n');
}

/** What each phase of the loop costs, with its sample size. */
function describePhases(reader: DashboardStore): string {
  const phases = reader.phaseDurations();

  if (phases.length === 0) {
    return '## Phase timings\n\nNo phase has been timed in this store.';
  }

  const lines = phases.map(
    (phase) =>
      `- ${phase.phase}: mean ${(phase.meanMilliseconds / 1000).toFixed(2)} s over ${phase.samples} ` +
      `sample(s), ${phase.failures} of which threw`
  );

  return ['## Phase timings', ...lines].join('\n');
}

/** The five criteria, as the gate itself judges them. */
function describeGate(verdict: GateVerdict): string {
  const lines = verdict.criteria.map(
    (criterion) => `- ${criterion.met ? 'MET' : 'NOT MET'} - ${criterion.name}: ${criterion.evidence}`
  );

  return [
    '## Workshop gate',
    `Verdict: ${verdict.open ? 'OPEN' : 'CLOSED'}.`,
    'The gate decides whether this may be shown to a class. It is evaluated by the harness, not by',
    'you, and the verdict above is the only one there is.',
    ...lines
  ].join('\n');
}

/**
 * The trail as counts, and the modes it recorded.
 *
 * @remarks
 * The mode is the one thing here that is never softened. A trail with an operation recorded in
 * Workshop Mode is the most important fact this brief can carry, so it is stated on its own line
 * rather than left to be inferred from a list of outcomes.
 */
function describeAudit(trail: AuditReadResult): string {
  const outcomes = new Map<string, number>();
  const modes = new Set<string>();

  for (const entry of trail.entries) {
    outcomes.set(entry.outcome, (outcomes.get(entry.outcome) ?? 0) + 1);
    modes.add(entry.mode);
  }

  const counted = [...outcomes.entries()]
    .sort(([, left], [, right]) => right - left)
    .slice(0, CountedOutcomes)
    .map(([outcome, count]) => `- ${outcome}: ${count}`);

  return [
    '## Audit trail',
    `${trail.entries.length} entry(ies) recorded.`,
    `Modes recorded: ${modes.size === 0 ? 'none' : [...modes].sort().join(', ')}.`,
    trail.unreadableLines.length === 0
      ? 'Every line of the trail was readable.'
      : `${trail.unreadableLines.length} line(s) could not be read, so these counts are incomplete.`,
    'Outcomes, most frequent first:',
    ...counted
  ].join('\n');
}
