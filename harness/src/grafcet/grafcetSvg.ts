/**
 * Draws a chart as SVG with the graphical symbols of IEC 60848:2013.
 *
 * @remarks
 * Step: a square, doubled for an initial step. Transition: a short thick bar across the link, with
 * its condition on the right and NOT drawn as an overbar, as on paper. Actions: rectangles to the
 * right of their step. Selection (OR) branches hang from a single line, parallel (AND) branches from
 * a double one. A return is either a loop on the left with an upward arrow or a jump arrow naming
 * its target; {@link layOutSequence} decides which.
 *
 * A term transcribed with `?` is drawn in the warning colour and keeps its question mark, so an
 * unconfirmed reading is visible in the drawing and not only in the report.
 */

import { layOutSequence, type RoutedTransition, type SequenceLayout } from './grafcetLayout.ts';
import type { Action, Condition, Grafcet, Sequence } from './grafcetModel.ts';
import { drawCondition, escape, segments } from './grafcetSvgText.ts';

const StepSize = 40;
const RowHeight = 118;
const CharWidth = 7.6;
const Top = 70;
const SequenceGap = 60;
const BarOffset = StepSize + 36;
const DivergenceOffset = StepSize + 14;

/** Distances in the drawing, in SVG units, named so that a change of style is one edit. */
const PageMargin = 20;
const BottomMargin = 40;
const TitleRise = 34;
const TextBaseline = 5;
const InitialInset = 4;
const ActionGap = 14;
const ActionPadding = 18;
const ActionTextInset = 9;
const ActionHeight = 28;
const GuardRise = 20;
const GuardInset = 4;
const BarHalfWidth = 13;
const ConditionGap = 20;
const ConditionOffset = 22;
const MinLaneWidth = 110;
const LanePadding = 36;
const JoinRise = 14;
const SyncGap = 16;
const DoubleLineGap = 4;
const DoubleLineOverhang = 8;
const JumpLength = 24;
const JumpLabelDrop = 42;
const LoopMarginBase = 30;
const LoopDrop = 18;
const LoopDropStep = 10;
const LoopColumnGap = 24;
const LoopColumnStep = 14;
const LoopRise = 16;
const LoopRiseStep = 8;

interface Placed {
  readonly sequence: Sequence;
  readonly layout: SequenceLayout;
  readonly laneX: readonly number[];
  readonly width: number;
}

/** Renders the whole chart as one standalone SVG document. */
export function renderGrafcetSvg(grafcet: Grafcet): string {
  let left = PageMargin;
  const placed: Placed[] = [];

  for (const sequence of grafcet.sequences) {
    const item = place(sequence, left);
    placed.push(item);
    left += item.width + SequenceGap;
  }

  const height = Top + Math.max(1, ...placed.map((item) => item.layout.rows)) * RowHeight + BottomMargin;
  const body = placed.map(drawSequence).join('\n');
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${left} ${height}" width="${left}" height="${height}" role="img" aria-label="${escape(grafcet.title)}">
<style>${Style}</style>
<defs><marker id="arrow" viewBox="0 0 10 10" refX="5" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" class="fill"/></marker></defs>
${body}
</svg>`;
}

const Style = `
.line{stroke:var(--grafcet-ink,#1d2430);stroke-width:1.6;fill:none}
.bar{stroke:var(--grafcet-ink,#1d2430);stroke-width:3.5}
.box{stroke:var(--grafcet-ink,#1d2430);stroke-width:1.6;fill:var(--grafcet-paper,#ffffff)}
.fill{fill:var(--grafcet-ink,#1d2430)}
text{font-family:ui-monospace,Consolas,monospace;font-size:13px;fill:var(--grafcet-ink,#1d2430)}
.number{font-size:15px;font-weight:600;text-anchor:middle}
.title{font-family:system-ui,sans-serif;font-size:15px;font-weight:600}
.small{font-size:11px}
.uncertain{fill:var(--grafcet-warn,#b45309);font-weight:700}`;

function place(sequence: Sequence, left: number): Placed {
  const layout = layOutSequence(sequence);
  const loopMargin = layout.loops > 0 ? LoopMarginBase + layout.loops * LoopColumnStep : 0;
  const widths = Array.from({ length: layout.lanes }, (_, lane) => laneWidth(sequence, layout, lane));
  const laneX = widths.map((_, lane) => left + loopMargin + widths.slice(0, lane).reduce((sum, width) => sum + width, 0));
  return { sequence, layout, laneX, width: loopMargin + widths.reduce((sum, width) => sum + width, 0) };
}

function laneWidth(sequence: Sequence, layout: SequenceLayout, lane: number): number {
  const steps = sequence.steps.filter((step) => layout.cells.get(step.number)?.lane === lane);
  const actions = steps.map((step) => StepSize + ActionGap + actionsWidth(step.actions));
  const conditions = layout.routes.filter((route) => route.barLane === lane).map((route) => StepSize / 2 + ConditionOffset + textWidth(route.transition.condition));
  return Math.max(MinLaneWidth, ...actions, ...conditions) + LanePadding;
}

function actionsWidth(actions: readonly Action[]): number {
  return actions.reduce((sum, action) => sum + actionText(action).length * CharWidth + ActionPadding, 0);
}

function textWidth(condition: Condition): number {
  return segments(condition).reduce((sum, segment) => sum + segment.text.length, 0) * CharWidth;
}

function drawSequence(item: Placed): string {
  const parts = [`<text class="title" x="${item.laneX[0]}" y="${Top - TitleRise}">${escape(item.sequence.name)}</text>`];
  parts.push(...item.sequence.steps.map((step) => drawStep(item, step.number, step.initial, step.actions)));
  parts.push(...drawDivergences(item));
  parts.push(...item.layout.routes.map((route) => drawRoute(item, route)));
  return `<g>${parts.join('')}</g>`;
}

function centreX(item: Placed, lane: number): number {
  return (item.laneX[lane] ?? item.laneX[0]!) + StepSize / 2;
}

function rowY(row: number): number {
  return Top + row * RowHeight;
}

function drawStep(item: Placed, number: number, initial: boolean, actions: readonly Action[]): string {
  const cell = item.layout.cells.get(number)!;
  const x = item.laneX[cell.lane]!;
  const y = rowY(cell.row);
  const inner = initial ? `<rect class="box" x="${x + InitialInset}" y="${y + InitialInset}" width="${StepSize - 2 * InitialInset}" height="${StepSize - 2 * InitialInset}"/>` : '';
  return `<rect class="box" x="${x}" y="${y}" width="${StepSize}" height="${StepSize}"/>${inner}<text class="number" x="${x + StepSize / 2}" y="${y + StepSize / 2 + TextBaseline}">${number}</text>${drawActions(x + StepSize, y + StepSize / 2, actions)}`;
}

function drawActions(fromX: number, centreY: number, actions: readonly Action[]): string {
  if (actions.length === 0) {
    return '';
  }

  let x = fromX + ActionGap;
  const parts = [`<line class="line" x1="${fromX}" y1="${centreY}" x2="${x}" y2="${centreY}"/>`];

  for (const action of actions) {
    const width = actionText(action).length * CharWidth + ActionPadding;
    const guard = action.kind === 'output' && action.condition !== null ? `<text class="small" x="${x + GuardInset}" y="${centreY - GuardRise}">${drawCondition(action.condition)}</text>` : '';
    parts.push(`<rect class="box" x="${x}" y="${centreY - ActionHeight / 2}" width="${width}" height="${ActionHeight}"/><text x="${x + ActionTextInset}" y="${centreY + TextBaseline}">${escape(actionText(action))}</text>${guard}`);
    x += width;
  }

  return parts.join('');
}

function actionText(action: Action): string {
  switch (action.kind) {
    case 'output':
      return action.name;
    case 'set':
      return `↑ ${action.name} := 1`;
    case 'reset':
      return `↑ ${action.name} := 0`;
    case 'timer':
      return `${action.name} = ${action.seconds} s`;
    default:
      throw new Error(`Unrecognised action: ${JSON.stringify(action satisfies never)}`);
  }
}

/** The single line a selection hangs from, drawn once per step with more than one way out. */
function drawDivergences(item: Placed): readonly string[] {
  return item.sequence.steps.flatMap((step) => {
    const routes = item.layout.routes.filter((route) => route.transition.from.length === 1 && route.transition.from[0] === step.number);

    if (routes.length < 2) {
      return [];
    }

    const cell = item.layout.cells.get(step.number)!;
    const y = rowY(cell.row) + DivergenceOffset;
    const xs = [centreX(item, cell.lane), ...routes.map((route) => centreX(item, route.barLane))];
    return [`<line class="line" x1="${centreX(item, cell.lane)}" y1="${rowY(cell.row) + StepSize}" x2="${centreX(item, cell.lane)}" y2="${y}"/><line class="line" x1="${Math.min(...xs)}" y1="${y}" x2="${Math.max(...xs)}" y2="${y}"/>`];
  });
}

function drawRoute(item: Placed, route: RoutedTransition): string {
  const x = centreX(item, route.barLane);
  const barY = rowY(route.row) + BarOffset;
  const lead = drawLead(item, route, x, barY);
  const bar = `<line class="bar" x1="${x - BarHalfWidth}" y1="${barY}" x2="${x + BarHalfWidth}" y2="${barY}"/><text x="${x + ConditionGap}" y="${barY + TextBaseline}">${drawCondition(route.transition.condition)}</text>`;
  return lead + bar + drawTail(item, route, x, barY);
}

/** The link from the source step(s) down to the bar. */
function drawLead(item: Placed, route: RoutedTransition, x: number, barY: number): string {
  const sources = route.transition.from.map((from) => item.layout.cells.get(from)!);
  const branching = item.layout.routes.filter((other) => other.transition.from.length === 1 && other.transition.from[0] === route.transition.from[0]).length > 1;

  if (sources.length > 1) {
    const joinY = barY - SyncGap;
    const xs = sources.map((cell) => centreX(item, cell.lane));
    const drops = sources.map((cell) => `<line class="line" x1="${centreX(item, cell.lane)}" y1="${rowY(cell.row) + StepSize}" x2="${centreX(item, cell.lane)}" y2="${joinY - DoubleLineGap}"/>`).join('');
    return `${drops}${doubleLine(Math.min(...xs, x), Math.max(...xs, x), joinY - DoubleLineGap)}<line class="line" x1="${x}" y1="${joinY}" x2="${x}" y2="${barY}"/>`;
  }

  const top = branching ? rowY(sources[0]!.row) + DivergenceOffset : rowY(sources[0]!.row) + StepSize;
  return `<line class="line" x1="${x}" y1="${top}" x2="${x}" y2="${barY}"/>`;
}

/** The link from the bar to the target step(s): straight, joined, parallel, a loop or a jump. */
function drawTail(item: Placed, route: RoutedTransition, x: number, barY: number): string {
  switch (route.kind) {
    case 'jump':
      return `<line class="line" x1="${x}" y1="${barY}" x2="${x}" y2="${barY + JumpLength}" marker-end="url(#arrow)"/><text class="number" x="${x}" y="${barY + JumpLabelDrop}">${route.transition.to.map((to) => `X${to}`).join(', ')}</text>`;
    case 'loop':
      return drawLoop(item, route, x, barY);
    case 'forward':
      return route.transition.to.length > 1 ? drawParallel(item, route, x, barY) : drawStraight(item, route.transition.to[0]!, x, barY);
    default:
      throw new Error(`Unrecognised route: ${JSON.stringify(route.kind satisfies never)}`);
  }
}

function drawStraight(item: Placed, target: number, x: number, barY: number): string {
  const cell = item.layout.cells.get(target)!;
  const targetX = centreX(item, cell.lane);
  const targetTop = rowY(cell.row);

  if (targetX === x) {
    return `<line class="line" x1="${x}" y1="${barY}" x2="${x}" y2="${targetTop}"/>`;
  }

  const joinY = targetTop - JoinRise;
  return `<polyline class="line" points="${x},${barY} ${x},${joinY} ${targetX},${joinY} ${targetX},${targetTop}"/>`;
}

function drawParallel(item: Placed, route: RoutedTransition, x: number, barY: number): string {
  const splitY = barY + SyncGap;
  const cells = route.transition.to.map((to) => item.layout.cells.get(to)!);
  const xs = cells.map((cell) => centreX(item, cell.lane));
  const drops = cells.map((cell) => `<line class="line" x1="${centreX(item, cell.lane)}" y1="${splitY + DoubleLineGap}" x2="${centreX(item, cell.lane)}" y2="${rowY(cell.row)}"/>`).join('');
  return `<line class="line" x1="${x}" y1="${barY}" x2="${x}" y2="${splitY}"/>${doubleLine(Math.min(...xs, x), Math.max(...xs, x), splitY)}${drops}`;
}

function doubleLine(fromX: number, toX: number, y: number): string {
  const left = fromX - DoubleLineOverhang;
  const right = toX + DoubleLineOverhang;
  return `<line class="line" x1="${left}" y1="${y}" x2="${right}" y2="${y}"/><line class="line" x1="${left}" y1="${y + DoubleLineGap}" x2="${right}" y2="${y + DoubleLineGap}"/>`;
}

/** A return to the first lane, drawn on the left, nested so that no two loops cross. */
function drawLoop(item: Placed, route: RoutedTransition, x: number, barY: number): string {
  const target = item.layout.cells.get(route.transition.to[0]!)!;
  const targetX = centreX(item, target.lane);
  const bottomY = barY + LoopDrop + route.loopIndex * LoopDropStep;
  const columnX = item.laneX[0]! - LoopColumnGap - route.loopIndex * LoopColumnStep;
  const topY = rowY(target.row) - LoopRise - route.loopIndex * LoopRiseStep;
  const middleY = (bottomY + topY) / 2;
  return `<polyline class="line" points="${x},${barY} ${x},${bottomY} ${columnX},${bottomY} ${columnX},${middleY}"/>`
    + `<line class="line" x1="${columnX}" y1="${middleY}" x2="${columnX}" y2="${middleY - 1}" marker-end="url(#arrow)"/>`
    + `<polyline class="line" points="${columnX},${middleY} ${columnX},${topY} ${targetX},${topY} ${targetX},${rowY(target.row)}"/>`;
}
