/**
 * Reads and writes transition-conditions in the notation used on a whiteboard.
 *
 * @remarks
 * `.` (or `·`, `*`) is AND, `+` is OR, `/` (or `!`, `¬`) is NOT, `↑` and `↓` are rising and falling
 * edges, `1` is always true, and parentheses group. That is the notation of IEC 60848 examples and of
 * most classrooms, where NOT is an overbar; `/` is how an overbar is typed.
 *
 * `?` marks a term the transcriber could not read with certainty, typically a negation bar on a
 * photographed whiteboard. It is kept all the way into the drawing and into the checks, so an unsure
 * reading can never turn silently into a confident one.
 */

import { and, negate, or, variable, type Condition, Always } from './grafcetModel.ts';

/** The outcome of parsing: a condition, or the reason there is none. */
export type ConditionParse =
  | { readonly ok: true; readonly condition: Condition }
  | { readonly ok: false; readonly error: string };

const AndSymbols = new Set(['.', '·', '*']);
const NotSymbols = new Set(['/', '!', '¬']);
const IdentifierPattern = /^[A-Za-z_][A-Za-z0-9_]*/;

/** Parses a condition. An empty text is refused rather than read as "always true". */
export function parseCondition(text: string): ConditionParse {
  const reader = new Reader(text);

  if (reader.atEnd()) {
    return { ok: false, error: 'empty condition (write 1 for a transition that is always true)' };
  }

  try {
    const condition = reader.readOr();
    return reader.atEnd() ? { ok: true, condition } : { ok: false, error: `unexpected '${reader.rest()}'` };
  } catch (error) {
    return { ok: false, error: (error as Error).message };
  }
}

/** Writes a condition back in the same notation, with the fewest parentheses that keep its meaning. */
export function formatCondition(condition: Condition): string {
  return format(condition, 0);
}

const OrPrecedence = 1;
const AndPrecedence = 2;
const UnaryPrecedence = 3;

function format(condition: Condition, outer: number): string {
  switch (condition.kind) {
    case 'true':
      return '1';
    case 'variable':
      return (condition.uncertain ? '?' : '') + condition.name;
    case 'not':
      return (condition.uncertain ? '?' : '') + '/' + format(condition.term, UnaryPrecedence);
    case 'rising':
      return '↑' + format(condition.term, UnaryPrecedence);
    case 'falling':
      return '↓' + format(condition.term, UnaryPrecedence);
    case 'and':
      return wrap(condition.terms.map((term) => format(term, AndPrecedence)).join('.'), AndPrecedence, outer);
    case 'or':
      return wrap(condition.terms.map((term) => format(term, OrPrecedence)).join(' + '), OrPrecedence, outer);
    default:
      throw new Error(`Unrecognised condition: ${JSON.stringify(condition satisfies never)}`);
  }
}

function wrap(text: string, own: number, outer: number): string {
  return own < outer ? `(${text})` : text;
}

/** A recursive-descent reader over one condition string. */
class Reader {
  private readonly _text: string;
  private _position = 0;

  public constructor(text: string) {
    this._text = text;
    this.skipSpace();
  }

  public atEnd(): boolean {
    return this._position >= this._text.length;
  }

  public rest(): string {
    return this._text.slice(this._position);
  }

  public readOr(): Condition {
    const terms = [this.readAnd()];

    while (this.accept((symbol) => symbol === '+')) {
      terms.push(this.readAnd());
    }

    return or(terms);
  }

  private readAnd(): Condition {
    const terms = [this.readUnary()];

    while (this.accept((symbol) => AndSymbols.has(symbol))) {
      terms.push(this.readUnary());
    }

    return and(terms);
  }

  private readUnary(): Condition {
    if (this.accept((symbol) => symbol === '?')) {
      return this.markUncertain(this.readUnary());
    }

    if (this.accept((symbol) => NotSymbols.has(symbol))) {
      return negate(this.readUnary());
    }

    if (this.accept((symbol) => symbol === '↑')) {
      return { kind: 'rising', term: this.readUnary() };
    }

    return this.accept((symbol) => symbol === '↓') ? { kind: 'falling', term: this.readUnary() } : this.readPrimary();
  }

  private markUncertain(condition: Condition): Condition {
    if (condition.kind === 'variable') {
      return variable(condition.name, true);
    }

    if (condition.kind === 'not') {
      return negate(condition.term, true);
    }

    throw new Error("'?' goes directly before a variable or a negation, e.g. ?/PS");
  }

  private readPrimary(): Condition {
    if (this.accept((symbol) => symbol === '(')) {
      const inner = this.readOr();
      this.expect(')');
      return inner;
    }

    if (this.accept((symbol) => symbol === '1')) {
      return Always;
    }

    const match = IdentifierPattern.exec(this.rest());

    if (match === null) {
      throw new Error(this.atEnd() ? 'condition ends too early' : `unexpected '${this.rest()}'`);
    }

    this._position += match[0].length;
    this.skipSpace();
    return variable(match[0]);
  }

  private accept(test: (symbol: string) => boolean): boolean {
    const symbol = this._text[this._position];

    if (symbol === undefined || !test(symbol)) {
      return false;
    }

    this._position++;
    this.skipSpace();
    return true;
  }

  private expect(symbol: string): void {
    if (!this.accept((candidate) => candidate === symbol)) {
      throw new Error(`expected '${symbol}'`);
    }
  }

  private skipSpace(): void {
    while (this._position < this._text.length && /\s/.test(this._text[this._position]!)) {
      this._position++;
    }
  }
}
