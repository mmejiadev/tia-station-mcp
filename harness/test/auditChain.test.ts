import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { canonicalForm, linkHash, verifyAuditChain } from '../src/auditChain.ts';

/**
 * The chain is what makes editing the audit trail detectable, and this is what reports it:
 * criterion 3 of the workshop gate is answered from these functions, so a bug here is a gate that
 * says "the audit is complete" about a file somebody rewrote.
 *
 * Two kinds of test, and both are necessary.
 *
 * The first kind checks that this code computes **the same hash the server did**. The trail is
 * written by `JsonlAuditTrail` in C# over a canonical form built by `System.Text.Json`, and
 * verified here in TypeScript: two languages, two JSON writers, one set of bytes that has to match.
 * So the expected values are not typed out here at all — they are read from two files under
 * `assets/` that .NET produced, through the very assembly the server is built against. A test that
 * asserted what this implementation happens to produce would pass just as happily against a
 * canonical form the server has never written.
 *
 * The second kind tampers with a genuine trail and checks that the chain notices. A chain that has
 * never been shown to catch a forgery is decoration.
 */

const Assets = join(dirname(fileURLToPath(import.meta.url)), 'assets');

/**
 * What .NET writes for one entry, and the values it wrote it from.
 *
 * @remarks
 * The vector covers every character the two JSON writers disagree about: the HTML-sensitive ASCII
 * .NET escapes and JavaScript does not, a backslash, the short escapes, control characters, two
 * non-ASCII code points and a surrogate pair.
 */
const EscapingVector = JSON.parse(readFileSync(join(Assets, 'audit-chain-escaping.json'), 'utf8')) as {
  readonly sequence: number;
  readonly previousHash: string;
  readonly fields: readonly string[];
  readonly canonical: string;
  readonly hash: string;
};

/**
 * A trail .NET wrote: one entry from before chaining existed, then three chained ones.
 *
 * @remarks
 * The second chained entry carries what a Spanish workshop actually produces — `ñ` in a block name,
 * `<` and `+` in SCL, a pair of quotes in the detail — and every entry carries the `+` of its
 * timestamp offset, and on disk that plus sign is not a plus sign: the .NET encoder writes it as a
 * six-character escape. It is the whole reason this fixture is read from a file rather than typed
 * out here — and the reason a verifier built on `JSON.stringify` would report line 1 of every trail
 * ever written as a forgery.
 */
/** One fixture, as the lines the server wrote, whatever line ending the checkout gave it. */
function linesOf(name: string): readonly string[] {
  return readFileSync(join(Assets, name), 'utf8')
    .split('\n')
    .map((text) => text.replace(/\r$/, ''))
    .filter((text) => text.trim().length > 0);
}

const TrailLines = linesOf('audit-chain-golden.jsonl');

/** The entry with no chain fields, which the trail already held when chaining was added. */
const UnattestedEntry = at(TrailLines, 0);

/** The three chained entries. */
const GoldenTrail: readonly string[] = TrailLines.slice(1);

/**
 * A trail written under version 2 of the canonical form, which added the citation.
 *
 * @remarks
 * Written by the server itself, not by hand: the point of a golden fixture here is that the bytes
 * came out of `System.Text.Json` and the hashes out of the C# chain, so this file failing is how
 * the two implementations announce that they have drifted apart.
 */
const VersionTwoTrail: readonly string[] = linesOf('audit-chain-golden-v2.jsonl');

describe('audit chain, against what .NET actually wrote', () => {
  it('builds the canonical form byte for byte as System.Text.Json does', () => {
    const canonical = canonicalForm(EscapingVector.sequence, EscapingVector.previousHash, EscapingVector.fields);

    assert.equal(canonical, EscapingVector.canonical);
  });

  it('computes the hash System.Text.Json produced for that same vector', () => {
    const hash = linkHash(EscapingVector.sequence, EscapingVector.previousHash, EscapingVector.fields);

    assert.equal(hash, EscapingVector.hash);
  });

  it('computes the hash the server recorded for a real entry', () => {
    // The plus in the timestamp offset is the character that makes or breaks this.
    const fields = [
      '2026-08-29T12:00:00.0000000+00:00',
      'AAA-111',
      'Study',
      'WriteScl',
      'PLC_0/Blocks/FB_Estacion_1',
      '',
      '',
      'agent',
      'Applied',
      ''
    ];

    assert.equal(linkHash(1, '', fields), '7607e107cc1c8b783dd2508df865ce2b15f72d42fb8c66bebe11f676f918c985');
  });

  it('accepts an untouched trail', () => {
    const report = verifyAuditChain(GoldenTrail);

    assert.equal(report.intact, true, report.reason);
    assert.equal(report.chained, 3);
    assert.equal(report.unchained, 0);
  });

  it('accepts a trail read from a file with Windows line endings', () => {
    // The server writes Environment.NewLine and the reader splits on '\n', so every line arrives
    // with a carriage return on the end of it.
    const report = verifyAuditChain(GoldenTrail.map((line) => `${line}\r`));

    assert.equal(report.intact, true, report.reason);
    assert.equal(report.chained, 3);
  });
});

describe('audit chain, tampered with', () => {
  it('catches an entry edited in place and names the line', () => {
    // The forgery this exists for: somebody changes what a write actually did.
    const trail = replaceIn(1, '"planId":"BBB-222"', '"planId":"BBB-999"');

    const report = verifyAuditChain(trail);

    assert.equal(report.intact, false);
    assert.equal(report.brokenAtLine, 2);
    assert.match(report.reason, /edited after it was written/);
  });

  it('catches a removed entry as a gap in the sequence', () => {
    // Deleting the record of an action leaves the most plausible file of all: every remaining line
    // is genuine, and only the counting gives it away.
    const trail = [at(GoldenTrail, 0), at(GoldenTrail, 2)];

    const report = verifyAuditChain(trail);

    assert.equal(report.intact, false);
    assert.match(report.reason, /removed or inserted/);
  });

  it('catches reordered entries', () => {
    const trail = [at(GoldenTrail, 0), at(GoldenTrail, 2), at(GoldenTrail, 1)];

    assert.equal(verifyAuditChain(trail).intact, false);
  });

  it('catches an entry that points back to the wrong predecessor', () => {
    // A file cut and re-joined out of two trails: every line in it is genuine, the join is not.
    const trail = replaceIn(1, '"prev":"7607e107', '"prev":"0000000f');

    const report = verifyAuditChain(trail);

    assert.equal(report.intact, false);
    assert.equal(report.brokenAtLine, 2);
    assert.match(report.reason, /cut and re-joined/);
  });

  it('catches chain fields stripped from the last entry', () => {
    // The forgery that would otherwise work: edit the last entry and delete its chain fields, so
    // that the edit reads as history from before chaining existed. Nothing follows it to give it
    // away by a gap in the sequence.
    const trail = [at(GoldenTrail, 0), at(GoldenTrail, 1), withoutChainFields(at(GoldenTrail, 2))];

    const report = verifyAuditChain(trail);

    assert.equal(report.intact, false);
    assert.equal(report.brokenAtLine, 3);
    assert.match(report.reason, /stripped/);
  });

  it('blames the stripped entry itself rather than the one after it', () => {
    // Stripping an entry in the middle is caught either way, because the next entry's sequence no
    // longer follows. Naming the next line would send a person to the one entry that is genuine.
    const trail = [at(GoldenTrail, 0), withoutChainFields(at(GoldenTrail, 1)), at(GoldenTrail, 2)];

    const report = verifyAuditChain(trail);

    assert.equal(report.brokenAtLine, 2);
    assert.match(report.reason, /stripped/);
  });
});

describe('audit chain, across versions of the canonical form', () => {
  it('accepts a trail the server wrote with the citation field', () => {
    const report = verifyAuditChain(VersionTwoTrail);

    assert.equal(report.intact, true, report.reason);
    assert.equal(report.chained, 3);
  });

  it('still accepts the trail written before that field existed', () => {
    // The reason versions exist at all: an eleventh value changes the hash of an entry written
    // with ten, so without this the whole of a workshop's history would read as forged.
    const report = verifyAuditChain(GoldenTrail);

    assert.equal(report.intact, true, report.reason);
  });

  it('catches a citation edited after the fact', () => {
    // The citation is inside the hash. A record of what justified a change that anybody could
    // rewrite afterwards would justify nothing.
    const edited = VersionTwoTrail.map((line, index) =>
      index === 0 ? line.replace('page 47', 'page 48') : line);

    const report = verifyAuditChain(edited);

    assert.equal(report.intact, false);
    assert.match(report.reason, /do not match its hash/);
  });

  it('names a version it does not know instead of calling it a forgery', () => {
    const newer = VersionTwoTrail.map((line, index) =>
      index === 0 ? line.replace('"v":"2"', '"v":"99"') : line);

    const report = verifyAuditChain(newer);

    assert.equal(report.intact, false);
    assert.match(report.reason, /written by a newer one/);
  });
});

describe('audit chain, on a trail that is not all chained', () => {
  it('reports entries written before chaining as unattested rather than broken', () => {
    // Chaining was added to a file that already held history. Reporting that history as tampered
    // with would be false; reporting it as verified would be worse.
    const report = verifyAuditChain([UnattestedEntry, ...GoldenTrail]);

    assert.equal(report.intact, true, report.reason);
    assert.equal(report.unchained, 1);
    assert.equal(report.chained, 3);
  });

  it('ignores blank lines and lines that are not JSON', () => {
    // An unreadable line is criterion 2's business. Calling it a forgery here would name the wrong
    // crime, and stopping at it would hide the state of the rest of the chain.
    const trail = [at(GoldenTrail, 0), '', 'this is not JSON', at(GoldenTrail, 1), '   '];

    const report = verifyAuditChain(trail);

    assert.equal(report.intact, true, report.reason);
    assert.equal(report.chained, 2);
  });

  it('is intact and empty when there is nothing to verify', () => {
    const report = verifyAuditChain([]);

    assert.equal(report.intact, true);
    assert.equal(report.chained, 0);
  });
});

/** One line of a trail reused by several tests, with the fixture checked rather than assumed. */
function at(lines: readonly string[], index: number): string {
  const found = lines[index];

  assert.ok(found !== undefined, `the golden trail has no line ${index}`);

  return found;
}

/** The golden trail with one substitution made in one of its lines. */
function replaceIn(index: number, find: string, replacement: string): string[] {
  const trail = [...GoldenTrail];

  assert.ok(at(GoldenTrail, index).includes(find), `the golden trail no longer contains ${find}`);

  trail[index] = at(GoldenTrail, index).replace(find, replacement);

  return trail;
}

/** One line with its seq, prev and hash removed, as a forger would leave it. */
function withoutChainFields(text: string): string {
  const record = JSON.parse(text) as Record<string, string>;

  delete record['seq'];
  delete record['prev'];
  delete record['hash'];

  return JSON.stringify(record);
}
