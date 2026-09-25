/**
 * Checks that every R_BF / S_BF in an initialisation covers exactly the steps it is meant to.
 *
 * @remarks
 * `R_BF "X31", n := 15` clears fifteen consecutive *bits of memory* starting at X31's address, not
 * fifteen *steps*. It only means "clear X31 to X45" if those tags sit at consecutive addresses, and
 * nothing in TIA Portal checks that. Found in a real class project on 2026-09-24: the range cleared
 * the steps of another conveyor and missed five of its own, and the program compiled cleanly.
 *
 * The intended range is inferred from the rung itself: the same rung sets the sequence's initial
 * step, so the range should clear that sequence's other steps and nothing else.
 */

import type { Grafcet } from './grafcetModel.ts';
import type { LadFinding, LadOperation } from './s7dclReader.ts';
import { formatBitAddress, type BitTag } from './tagTable.ts';

type RangeOperation = LadOperation & { readonly kind: 'setRange' | 'resetRange'; readonly count: number };

/** Compares each bit range with the steps of the sequence its rung initialises. */
export function checkBitfieldRanges(operations: readonly LadOperation[], tags: readonly BitTag[], grafcet: Grafcet): readonly LadFinding[] {
  const ranges = operations.filter((operation): operation is RangeOperation => operation.kind === 'setRange' || operation.kind === 'resetRange');
  return ranges.flatMap((range) => checkRange(range, operations, tags, grafcet));
}

function checkRange(range: RangeOperation, operations: readonly LadOperation[], tags: readonly BitTag[], grafcet: Grafcet): readonly LadFinding[] {
  const start = tags.find((tag) => tag.name === range.operand);

  if (start === undefined) {
    return [{ ...range, severity: 'warning', message: `${range.operand} is not in the tag table, so the range ${range.operand}..+${range.count} cannot be checked` }];
  }

  const covered = tags.filter((tag) => tag.area === start.area && tag.bit >= start.bit && tag.bit < start.bit + range.count);
  const intended = intendedSteps(range, operations, grafcet);

  if (intended === null) {
    return [];
  }

  return [...reportExtra(range, covered, intended, start), ...reportMissing(range, covered, intended, start)];
}

/** The non-initial steps of the sequence whose initial step the same rung sets. */
function intendedSteps(range: RangeOperation, operations: readonly LadOperation[], grafcet: Grafcet): readonly string[] | null {
  const initialSet = operations.find((operation) => operation.kind === 'set' && operation.block === range.block && operation.network === range.network);
  const sequence = grafcet.sequences.find((candidate) => candidate.steps.some((step) => step.initial && `X${step.number}` === (initialSet as { operand?: string } | undefined)?.operand));
  return sequence === undefined ? null : sequence.steps.filter((step) => !step.initial).map((step) => `X${step.number}`);
}

function reportExtra(range: RangeOperation, covered: readonly BitTag[], intended: readonly string[], start: BitTag): readonly LadFinding[] {
  const extra = covered.filter((tag) => !intended.includes(tag.name));
  const end = formatBitAddress(start.area, start.bit + range.count - 1);
  return extra.length === 0 ? [] : [{ ...range, severity: 'error', message: `${range.kind === 'resetRange' ? 'R_BF' : 'S_BF'} ${range.operand} n=${range.count} covers ${formatBitAddress(start.area, start.bit)}..${end}, which also holds ${extra.map(tag => tag.name).join(', ')}` }];
}

function reportMissing(range: RangeOperation, covered: readonly BitTag[], intended: readonly string[], start: BitTag): readonly LadFinding[] {
  const missing = intended.filter((name) => !covered.some((tag) => tag.name === name));
  return missing.length === 0 ? [] : [{ ...range, severity: 'error', message: `${range.operand} n=${range.count} from ${formatBitAddress(start.area, start.bit)} does not reach ${missing.join(', ')}: give the sequence's steps consecutive addresses, or clear them one by one` }];
}
