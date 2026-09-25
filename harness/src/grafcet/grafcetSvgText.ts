/**
 * Typesetting of conditions and names inside the drawing: NOT as an overbar, an unconfirmed reading
 * in the warning style with its question mark kept, and every name escaped for SVG and HTML.
 */

import type { Condition } from './grafcetModel.ts';

/** A run of text with the same decoration. */
export interface Segment {
  readonly text: string;
  readonly overline: boolean;
  readonly uncertain: boolean;
}

export function drawCondition(condition: Condition): string {
  return segments(condition).map((segment) => {
    const classes = segment.uncertain ? ' class="uncertain"' : '';
    const decoration = segment.overline ? ' text-decoration="overline"' : '';
    return `<tspan${classes}${decoration}>${escape(segment.text)}</tspan>`;
  }).join('');
}

/** Flattens a condition into runs of text, marking which runs carry an overbar. */
export function segments(condition: Condition, overline = false): readonly Segment[] {
  const plain = (text: string): Segment => ({ text, overline, uncertain: false });

  switch (condition.kind) {
    case 'true':
      return [plain('1')];
    case 'variable':
      return [{ text: condition.name + (condition.uncertain ? '?' : ''), overline, uncertain: condition.uncertain }];
    case 'not':
      return condition.uncertain ? segments(condition.term, !overline).map((segment) => ({ ...segment, text: segment.text + '?', uncertain: true })) : segments(condition.term, !overline);
    case 'rising':
      return [plain('↑'), ...segments(condition.term, overline)];
    case 'falling':
      return [plain('↓'), ...segments(condition.term, overline)];
    case 'and':
      return joinSegments(condition.terms.map((term) => term.kind === 'or' ? [plain('('), ...segments(term, overline), plain(')')] : segments(term, overline)), plain('·'));
    case 'or':
      return joinSegments(condition.terms.map((term) => segments(term, overline)), plain(' + '));
    default:
      throw new Error(`Unrecognised condition: ${JSON.stringify(condition satisfies never)}`);
  }
}

function joinSegments(groups: readonly (readonly Segment[])[], separator: Segment): readonly Segment[] {
  return groups.flatMap((group, index) => index === 0 ? group : [separator, ...group]);
}

export function escape(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
