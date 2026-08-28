import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { HybridSearch } from '../../src/knowledge/hybridSearch.ts';
import { TrigramModel } from '../../src/knowledge/lexicalVector.ts';
import { buildTemporaryIndex, type FakeDocument } from './temporaryIndex.ts';

/**
 * The cardinal rule, asserted.
 *
 * `docs/KNOWLEDGE-LAYER.md`: *the system cites, it does not author*, and *when nothing can be
 * cited, the answer is "not found, open the manual"*. Every test here names one half of that, and
 * would fail if the half it names were removed. Incidental coverage from a test about ranking does
 * not count — a rule nobody asserts is a rule that quietly stops holding.
 */
describe('HybridSearch', () => {
  it('returns excerpts that are literal spans of the page they name', () => {
    const index = buildTemporaryIndex(corpus());

    try {
      const result = new HybridSearch(index.store, TrigramModel).lookup('safety input redundant paired');

      assert.equal(result.outcome, 'cited');
      assert.ok(result.citations.length > 0);

      for (const citation of result.citations) {
        const source = [...index.pageText.values()].some((page) => page.includes(citation.excerpt));
        assert.ok(source, `excerpt is not a literal span of any indexed page: ${citation.excerpt}`);
      }
    } finally {
      index.store.close();
    }
  });

  it('stays silent when the answer is not in the corpus', () => {
    const index = buildTemporaryIndex(corpus());

    try {
      // The defect this pins was real, on 2026-08-28: with a BM25 threshold alone, this question
      // came back with three excerpts from a robot manual, because `capital` occurs somewhere in
      // five hundred pages and is rare enough to rank well. Silence is a decision, not a leftover.
      const result = new HybridSearch(index.store, TrigramModel).lookup('what is the capital of France');

      assert.equal(result.outcome, 'not-found');
      assert.deepEqual(result.citations, []);
    } finally {
      index.store.close();
    }
  });

  it('says nothing about a device it holds no document for', () => {
    const index = buildTemporaryIndex(corpus());

    try {
      const result = new HybridSearch(index.store, TrigramModel).lookup('KR C4 controller cabinet mains supply');

      assert.equal(result.outcome, 'not-found');
    } finally {
      index.store.close();
    }
  });

  it('narrows to one device when asked, rather than answering about another', () => {
    const index = buildTemporaryIndex(corpus());

    try {
      const result = new HybridSearch(index.store, TrigramModel).lookup('response time', { device: 'C4000' });

      assert.equal(result.outcome, 'cited');

      for (const citation of result.citations) {
        assert.equal(citation.device, 'C4000');
      }
    } finally {
      index.store.close();
    }
  });

  it('carries the document, version and page on every excerpt it returns', () => {
    const index = buildTemporaryIndex(corpus());

    try {
      const result = new HybridSearch(index.store, TrigramModel).lookup('response time', { device: 'C4000' });

      for (const citation of result.citations) {
        assert.equal(citation.title, 'C4000 operating instructions');
        assert.equal(citation.version, 'im0011945, English');
        assert.ok(citation.page >= 1, 'a citation without a page is not a citation');
      }
    } finally {
      index.store.close();
    }
  });
});

/** Two short documents, written here so the tests need no PDF and no corpus. */
function corpus(): FakeDocument[] {
  return [
    {
      device: 'UR5e',
      title: 'UR5e user manual',
      version: 'SW 5.16',
      pages: [
        'All safety I/O are paired and redundant, so a single fault does not cause loss of the safety function. The safety input must be kept as two separate branches.',
        'The permanent safety input types are Robot Emergency Stop for emergency stop equipment only, Safeguard Stop for protective devices, and 3PE Stop for protective devices.',
      ],
    },
    {
      device: 'C4000',
      title: 'C4000 operating instructions',
      version: 'im0011945, English',
      pages: [
        'The response time of the entire protective device depends on the host and the guest, and is stated in the chapter on response time on page 78 of this document.',
        'The minimum distance depends on the stopping time of the machine, the response time of the protective device, and the approach speed of the person.',
      ],
    },
  ];
}
