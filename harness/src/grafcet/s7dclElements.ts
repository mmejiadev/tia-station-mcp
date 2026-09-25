/**
 * The lexical half of reading a SIMATIC SD document: turning its lines into elements, and its
 * literals into values. Kept apart from s7dclReader.ts, which gives the elements their meaning.
 */

/** Joins an element that spans several lines, such as a timer box, into one line. */
export function joinElements(lines: readonly string[]): readonly string[] {
  const elements: string[] = [];
  let buffer = '';

  for (const line of lines.filter((candidate) => candidate !== '')) {
    buffer = buffer === '' ? line : `${buffer} ${line}`;

    if (depth(buffer) <= 0) {
      elements.push(buffer.replace(/\(\s+/g, '(').replace(/\s+\)/g, ' )'));
      buffer = '';
    }
  }

  return elements;
}

function depth(text: string): number {
  return [...text].reduce((level, character) => level + (character === '(' ? 1 : character === ')' ? -1 : 0), 0);
}

/** Reads `pt := T#30S` (also T#500MS, T#1M30S) from a timer's argument list, in seconds. */
export function readPreset(argumentsText: string): number | null {
  const literal = /pt\s*:=\s*T#([0-9A-Z_]+)/i.exec(argumentsText)?.[1];

  if (literal === undefined) {
    return null;
  }

  const units: Record<string, number> = { D: 86400, H: 3600, M: 60, S: 1, MS: 0.001 };
  const parts = [...literal.toUpperCase().matchAll(/(\d+(?:\.\d+)?)(MS|D|H|M|S)/g)];
  return parts.length === 0 ? null : parts.reduce((total, part) => total + Number(part[1]) * units[part[2]!]!, 0);
}
