import { existsSync } from 'node:fs';
import { readAuditChain, readAuditTrail } from './auditTrail.ts';
import type { GateEvidence } from './gate.ts';
import type { RunStatistics } from './telemetry.ts';
import { readReviewRecord } from './workshopReview.ts';

/**
 * The two reads the gate needs from whatever recorded the runs.
 *
 * @remarks
 * A structural type rather than a class, because both things that hold the measurements satisfy it:
 * `Telemetry`, which the command-line report already had open, and `MetricsReader`, which the
 * dashboard API opens beside a run that is still writing. Naming what is needed instead of who
 * provides it is what lets the gathering below exist exactly once.
 */
export type RunStore = {
  runStatistics(): RunStatistics[];
  countUnfinishedIterations(): number;
};

/** Where the evidence that is not in the store lives. */
export type EvidencePaths = {
  readonly auditPath: string;
  readonly reviewPath: string;
};

/**
 * Gathers everything the gate judges.
 *
 * @param store The recorded runs.
 * @param paths The audit trail and the review record.
 * @returns The evidence, ready for `evaluateGate`.
 * @remarks
 * It exists so the dashboard and `npm run gate` cannot answer the same question differently. Two
 * callers that each assembled the evidence themselves would agree until one of them was changed,
 * and a gate that says "shut" in a terminal and "open" in a browser is worse than no gate at all.
 */
export function gatherEvidence(store: RunStore, paths: EvidencePaths): GateEvidence {
  return {
    runs: store.runStatistics(),
    unfinishedIterations: store.countUnfinishedIterations(),
    audit: readAuditTrail(paths.auditPath),
    chain: readAuditChain(paths.auditPath),
    backupExists: existsSync,
    review: readReviewRecord(paths.reviewPath)
  };
}
