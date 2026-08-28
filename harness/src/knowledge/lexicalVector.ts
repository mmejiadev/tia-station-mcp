import type { EmbeddingModel } from './embeddingModel.ts';

/**
 * The vector half of the hybrid search, and an honest account of what it is.
 *
 * @remarks
 * **This is not a semantic embedding, and nothing here should be read as claiming it is.** Anthropic
 * publishes no embeddings API, and the brief's non-negotiables put the index on this machine with no
 * new service and no second API key. What ships instead is a hashed character-trigram vector: a
 * fuzzy *lexical* signal, computed locally, deterministic, and testable without a network.
 *
 * It earns its place next to BM25 rather than duplicating it. BM25 matches whole tokens, so
 * `6ES7214-1AG40` and `6ES7 214-1AG40-0XB0` are two unrelated terms to it and a query for one
 * scores zero against the other. Trigrams overlap heavily across both, which is precisely the
 * failure mode the brief names — technical queries are exact references, and vector search is weak
 * there. Paraphrase, which a real embedding would catch, this does not catch. That limitation is
 * stated in `docs/KNOWLEDGE-LAYER.md` and in the README rather than hidden behind the word
 * "hybrid".
 *
 * Replacing it with a real model is one class: implement {@link EmbeddingModel}, re-index, and the
 * model name recorded beside the vectors makes the old index refuse to be read as the new one.
 */

/** Fixed width. Wider buys nothing at eighteen thousand chunks and costs store size. */
const Dimensions = 512;

/** Trigrams, because pairs collide on everything and four-grams miss short part numbers. */
const GramLength = 3;

/**
 * Folds text to the alphabet the grams are taken over.
 *
 * @remarks
 * Case and punctuation go, because `X20 connector`, `x20-connector` and `X20_CONNECTOR` are the
 * same reference written three ways. Runs of separators collapse to one space so a gram never
 * spans a gap that was not there.
 */
function foldForGrams(text: string): string {
  return ` ${text.toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim()} `;
}

/** FNV-1a, 32-bit. Small, fast, and it spreads short grams better than a sum of char codes. */
function hashGram(gram: string): number {
  let hash = 0x811c9dc5;

  for (let index = 0; index < gram.length; index += 1) {
    hash ^= gram.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }

  return (hash >>> 0) % Dimensions;
}

/** L2 normalisation in place, so the search's dot product is a cosine. */
function normalise(vector: Float32Array): Float32Array {
  let sumOfSquares = 0;

  for (const component of vector) {
    sumOfSquares += component * component;
  }

  if (sumOfSquares === 0) {
    return vector;
  }

  const length = Math.sqrt(sumOfSquares);

  for (let index = 0; index < vector.length; index += 1) {
    vector[index] = (vector[index] ?? 0) / length;
  }

  return vector;
}

/** The local, key-free, deliberately lexical implementation of {@link EmbeddingModel}. */
export const TrigramModel: EmbeddingModel = {
  name: `trigram-${GramLength}-${Dimensions}`,
  dimensions: Dimensions,
  embed(text: string): Float32Array {
    const folded = foldForGrams(text);
    const vector = new Float32Array(Dimensions);

    for (let index = 0; index + GramLength <= folded.length; index += 1) {
      const bucket = hashGram(folded.slice(index, index + GramLength));
      vector[bucket] = (vector[bucket] ?? 0) + 1;
    }

    return normalise(vector);
  },
};

/**
 * Cosine of two vectors the model produced.
 *
 * @remarks
 * A plain dot product, because {@link TrigramModel} returns unit vectors. Vectors of different
 * widths are a programming error — two models in one index — and throw rather than being padded
 * into a number that means nothing.
 */
export function cosine(left: Float32Array, right: Float32Array): number {
  if (left.length !== right.length) {
    throw new Error(`Vector widths differ: ${left.length} and ${right.length}. The index was built by another model.`);
  }

  let total = 0;

  for (let index = 0; index < left.length; index += 1) {
    total += (left[index] ?? 0) * (right[index] ?? 0);
  }

  return total;
}
