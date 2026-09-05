import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { repairPageText } from '../src/knowledge/pageText.ts';

/**
 * Every case here was read off the corpus, not invented. Two of the three indexed manuals carry
 * control characters inside their technical references, and the retrieval gate found it while its
 * ground truth was being written. If a fourth document arrives whose corruption does not fit the
 * rule, one of these is the test to argue with.
 */
describe('page text repair', () => {
  it('joins a standard number split by a control character', () => {
    // The case that started it: EN ISO 13855 was unfindable because BM25 tokenised it as
    // something no query contains.
    assert.equal(repairPageText('according to EN ISO 13\u0001855 and'), 'according to EN ISO 13855 and');
  });

  it('joins a standard number split in the middle of its digits', () => {
    assert.equal(repairPageText('ESPE as defined by IEC 61\u0001496D1'), 'ESPE as defined by IEC 61496D1');
  });

  it('keeps the gap where the character stood between a word and a number', () => {
    // Deleting here would manufacture VDMA24562, a token the document does not contain and nobody
    // will ever search for.
    assert.equal(repairPageText('VDMA\u000224\u0002562,'), 'VDMA 24562,');
  });

  it('does not glue two words together', () => {
    assert.equal(repairPageText('NF\u0002E\u000249\u0002003.1'), 'NF E 49003.1');
  });

  it('turns a bullet glyph into the space it was standing in for', () => {
    assert.equal(repairPageText('tions:\n\u0002 8009855\n'), 'tions:\n 8009855\n');
  });

  it('leaves a page with nothing wrong with it exactly as it was', () => {
    // The 373 pages of the corpus that are clean must come through untouched, or every excerpt in
    // the index stops being verbatim.
    const page = 'Mini Displayport supports monitors with\na resolution of 1920 x 1080.';

    assert.equal(repairPageText(page), page);
  });

  it('keeps the line breaks the extractor put in', () => {
    // A citation's excerpt is read by a person. Collapsing the newlines would save nothing and
    // make a page of a manual harder to read than the manual.
    assert.equal(repairPageText('first\nsecond\nthird'), 'first\nsecond\nthird');
  });

  it('collapses only the doubled spaces the repair itself creates', () => {
    assert.equal(repairPageText('a \u0002 b'), 'a b');
  });

  it('handles a run of control characters as one separator', () => {
    assert.equal(repairPageText('mLoad\u0003\u0002\u00032'), 'mLoad 2');
  });

  it('does not fall over on a control character at either end', () => {
    assert.equal(repairPageText('\u0001abc\u0001'), ' abc ');
  });
});
