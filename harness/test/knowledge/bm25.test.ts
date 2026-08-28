import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { Bm25Index, queryCoverage, tokenise } from '../../src/knowledge/bm25.ts';

/**
 * BM25 does most of the work in this corpus, because technical questions are exact references.
 * `queryCoverage` is tested here beside it because it is the measure the abstention rule reads,
 * and the two together decide whether anything is said at all.
 */
describe('tokenise', () => {
  it('keeps an order number as one term instead of splitting letters from digits', () => {
    // Split apart, every order number in the corpus collapses into the same handful of tokens and
    // the ranking that depends on their rarity stops working.
    assert.deepEqual(tokenise('6ES7 214-1AG40-0XB0'), ['6es7', '214', '1ag40', '0xb0']);
  });

  it('folds case and drops punctuation', () => {
    assert.deepEqual(tokenise('Safety I/O, redundant.'), ['safety', 'i', 'o', 'redundant']);
  });
});

describe('Bm25Index', () => {
  it('ranks the chunk holding the rare term above one holding only common words', () => {
    const index = new Bm25Index([
      { chunkId: 1, text: 'The device is supplied with the machine and the documentation.' },
      { chunkId: 2, text: 'The response time of the C4000 host and guest is stated on page 78.' },
      { chunkId: 3, text: 'The machine and the device and the documentation are supplied.' },
    ]);

    const ranked = index.search('C4000 response time', 3);

    assert.equal(ranked[0]?.chunkId, 2);
  });

  it('returns nothing when no query term occurs anywhere', () => {
    const index = new Bm25Index([{ chunkId: 1, text: 'Pneumatic cushioning, adjustable at both ends.' }]);

    assert.deepEqual(index.search('firmware download', 5), []);
  });
});

describe('queryCoverage', () => {
  it('is the fraction of meaningful query words the chunk actually contains', () => {
    assert.equal(queryCoverage('response time C4000', 'The response time of the C4000 is stated.'), 1);
    assert.equal(queryCoverage('response time C4000', 'The response was slow.'), 1 / 3);
  });

  it('ignores stop words, so a question asked in a full sentence is not penalised', () => {
    assert.equal(queryCoverage('what is the response time', 'The response time is 15 ms.'), 1);
  });

  it('is zero for a query made only of stop words, which is a question that asked nothing', () => {
    assert.equal(queryCoverage('what is it', 'Anything at all.'), 0);
  });
});
