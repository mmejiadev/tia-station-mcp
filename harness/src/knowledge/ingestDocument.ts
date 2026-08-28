import { chunkPage } from './chunker.ts';
import { readPdfPages } from './pdfPages.ts';
import { verifyFileHash } from './corpusRecipe.ts';
import type { EmbeddingModel } from './embeddingModel.ts';
import type { KnowledgeStore } from './knowledgeStore.ts';
import type { RecipeEntry } from './corpusRecipe.ts';

/**
 * Puts one document into the index: verify, extract, cut, embed, store.
 *
 * @remarks
 * The order matters and is the whole of the governance this stage has. The hash is checked **first**,
 * before a single page is read, so a file that is not the file the recipe describes never reaches
 * the parser. Stage 4 adds the whitelist and the quarantine that a fetched document needs; stage 1
 * ingests only what somebody put on the disk by hand, and the hash is what makes even that
 * identifiable afterwards.
 */

/** What one ingestion did, in the numbers a report prints. */
export type IngestionSummary = {
  readonly device: string;
  readonly title: string;
  readonly pages: number;
  readonly chunks: number;
  /** True when the document was already indexed, byte for byte. Nothing was re-read. */
  readonly alreadyIndexed: boolean;
};

/**
 * Indexes one document described by the recipe.
 *
 * @param store An open index.
 * @param model The model whose vectors the store was stamped with.
 * @param entry The recipe entry, which supplies every field of the citation but the page.
 * @param filePath The local PDF. Its hash must be the one the entry states.
 * @returns What was indexed, or that it already was.
 */
export async function ingestDocument(
  store: KnowledgeStore,
  model: EmbeddingModel,
  entry: RecipeEntry,
  filePath: string
): Promise<IngestionSummary> {
  await verifyFileHash(filePath, entry.sha256);

  const existing = store.documentByHash(entry.sha256);

  if (existing !== undefined) {
    return { device: entry.device, title: entry.title, pages: existing.pageCount, chunks: 0, alreadyIndexed: true };
  }

  const pages = await readPdfPages(filePath);
  const documentId = store.insertDocument({
    device: entry.device,
    title: entry.title,
    version: entry.version,
    sourceUrl: entry.sourceUrl,
    sha256: entry.sha256,
    pageCount: pages.length,
  });

  let chunks = 0;

  for (const [index, pageText] of pages.entries()) {
    chunks += indexPage(store, model, documentId, index + 1, pageText);
  }

  return { device: entry.device, title: entry.title, pages: pages.length, chunks, alreadyIndexed: false };
}

/** Stores one page whole and every chunk cut from it. Returns how many chunks that was. */
function indexPage(
  store: KnowledgeStore,
  model: EmbeddingModel,
  documentId: ReturnType<KnowledgeStore['insertDocument']>,
  pageNumber: number,
  pageText: string
): number {
  store.insertPage(documentId, pageNumber, pageText);

  const chunks = chunkPage(pageText, pageNumber);

  for (const chunk of chunks) {
    store.insertChunk(documentId, chunk, model.embed(chunk.text));
  }

  return chunks.length;
}
