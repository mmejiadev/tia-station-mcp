import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { chunkPage } from '../../src/knowledge/chunker.ts';

/**
 * Chunking is where a citation stops being verbatim, if it ever does.
 *
 * The excerpt on screen is a chunk, and a chunk is only the manufacturer's words for as long as
 * nothing tidies it on the way in. These tests hold that line at the earliest point it can be held.
 */
describe('chunkPage', () => {
  it('returns chunks that are literal slices of the page', () => {
    const page = [
      'Safety I/O wiring. The acknowledgement signal is active low.',
      'All safety inputs are paired and redundant, so a single fault does not lose the function.',
      'Do not connect the emergency stop to a standard digital input under any circumstances.',
    ].join('\n');

    const chunks = chunkPage(page, 7);

    for (const chunk of chunks) {
      assert.equal(chunk.text, page.slice(chunk.startOffset, chunk.endOffset));
      assert.equal(chunk.pageNumber, 7);
    }
  });

  it('keeps the awkward whitespace a PDF puts in rather than tidying it', () => {
    // The temptation is to collapse this into one clean line. Doing so would put words on screen in
    // an order and shape the manual does not have, under a heading that says verbatim.
    const page = `Response  time\n\n   of the   protective device , see chapter Response time on page 78. ${'x'.repeat(60)}`;

    const chunks = chunkPage(page, 1);

    assert.equal(chunks.length, 1);
    assert.equal(chunks[0]?.text, page);
  });

  it('cuts a page with no boundaries at all rather than emitting one enormous chunk', () => {
    const page = 'a'.repeat(5000);

    const chunks = chunkPage(page, 2);

    assert.ok(chunks.length > 1, `expected several chunks, got ${chunks.length}`);

    for (const chunk of chunks) {
      assert.ok(chunk.text.length <= 1400, `chunk of ${chunk.text.length} characters exceeds the ceiling`);
    }
  });

  it('skips page furniture instead of indexing a page number as a chunk', () => {
    // A running header on its own answers nothing and, indexed, competes with passages that do.
    assert.deepEqual(chunkPage('49\nUR5e User Manual\n', 49), []);
  });

  it('returns nothing for a page with no extractable text', () => {
    assert.deepEqual(chunkPage('', 3), []);
  });
});
