/**
 * The one thing retrieval needs from an embedding, and nothing more.
 *
 * @remarks
 * `docs/KNOWLEDGE-LAYER.md` requires the retrieval call to sit behind one interface so that growing
 * out of a brute-force scan is one class rather than a migration. This is that interface, and it is
 * deliberately three members wide: a name that gets recorded with the index, the dimension count so
 * a store can refuse vectors that were built by a different model, and the function itself.
 */
export type EmbeddingModel = {
  /** Recorded next to the vectors, so an index built by another model is detectable. */
  readonly name: string;
  readonly dimensions: number;
  /** Must return a unit-length vector: the search does a dot product and calls it a cosine. */
  embed(text: string): Float32Array;
};
