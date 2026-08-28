import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { TrigramModel, cosine } from '../../src/knowledge/lexicalVector.ts';

/**
 * The vector half of the search, tested for what it actually is.
 *
 * There is no test here that it understands a paraphrase, because it does not: it is a lexical
 * signal, not a semantic one. What it must do is catch the same reference written two ways, which
 * is the case BM25 cannot reach, and stay deterministic so an index built today is comparable with
 * a query asked tomorrow.
 */
describe('TrigramModel', () => {
  it('returns a unit vector, so the search can call a dot product a cosine', () => {
    const vector = TrigramModel.embed('Pneumatic cushioning, adjustable at both ends.');
    const length = Math.sqrt([...vector].reduce((sum, component) => sum + component * component, 0));

    assert.ok(Math.abs(length - 1) < 1e-6, `length was ${length}`);
  });

  it('is deterministic, because the index and the query are embedded at different times', () => {
    assert.deepEqual(TrigramModel.embed('C4000 response time'), TrigramModel.embed('C4000 response time'));
  });

  it('scores the same order number written two ways above unrelated technical text', () => {
    // This is the reason it exists. To BM25 these two are unrelated terms and the query scores zero
    // against the document; to trigrams they overlap almost completely.
    const query = TrigramModel.embed('6ES7214-1AG40');
    const sameOrderNumber = TrigramModel.embed('order number 6ES7 214-1AG40-0XB0, CPU 1214C');
    const otherText = TrigramModel.embed('Elastic cushioning rings at both ends of the cylinder');

    assert.ok(
      cosine(query, sameOrderNumber) > cosine(query, otherText),
      'the variant spelling should score above unrelated text'
    );
  });

  it('is not asked to understand a paraphrase, and does not', () => {
    // Recorded as a test rather than left as a comment: this is the limitation the README states,
    // and if a real embedding model ever replaces this one, this assertion is what should fail.
    const query = TrigramModel.embed('how fast does the guard react');
    const answer = TrigramModel.embed('the response time of the protective device is 15 ms');
    const unrelated = TrigramModel.embed('the response time of the protective device is 15 ms'.replace(/./g, 'z'));

    assert.ok(cosine(query, answer) < 0.55, 'a paraphrase is not expected to clear the admission threshold');
    assert.ok(cosine(query, unrelated) < cosine(query, answer));
  });
});

describe('cosine', () => {
  it('refuses vectors of different widths rather than returning a meaningless number', () => {
    assert.throws(() => cosine(new Float32Array(4), new Float32Array(8)), /widths differ/);
  });
});
