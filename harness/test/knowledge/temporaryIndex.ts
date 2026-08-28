import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { chunkPage } from '../../src/knowledge/chunker.ts';
import { KnowledgeStore } from '../../src/knowledge/knowledgeStore.ts';
import { TrigramModel } from '../../src/knowledge/lexicalVector.ts';
import type { DocumentId } from '../../src/knowledge/citation.ts';

/**
 * Builds a small index on disk out of text the test writes itself.
 *
 * @remarks
 * No PDF, no corpus, no TIA Portal. The manuals are copyrighted and are not in the repository, so a
 * test that needed one could only run on a machine whose owner had fetched it — and the brief is
 * explicit that a rule which can only be checked on some machines is a rule that stops being
 * checked. Everything from chunking onwards is exercised on pages supplied here; `pdfPages.ts` is
 * the one part left uncovered, and it is the part that has no decisions in it.
 */

/** A document as a test describes it: some identity, and its pages. */
export type FakeDocument = {
  readonly device: string;
  readonly title: string;
  readonly version: string;
  readonly pages: readonly string[];
};

/** An index and the pages it was built from, so a test can compare a citation with its source. */
export type TemporaryIndex = {
  readonly store: KnowledgeStore;
  readonly path: string;
  /** Page text by document identifier and page number, exactly as it was stored. */
  readonly pageText: Map<string, string>;
};

/** A hash that is a hash-shaped string. Nothing here verifies one; `corpusRecipe` does that. */
function fakeHash(seed: string): string {
  return seed.padEnd(64, '0').slice(0, 64).replace(/[^0-9a-f]/g, '0');
}

/** Builds the index. The caller closes the store; the directory is a temporary one. */
export function buildTemporaryIndex(documents: readonly FakeDocument[]): TemporaryIndex {
  const path = join(mkdtempSync(join(tmpdir(), 'knowledge-')), 'knowledge.db');
  const store = KnowledgeStore.open(path, TrigramModel);
  const pageText = new Map<string, string>();

  for (const document of documents) {
    const documentId = store.insertDocument({
      device: document.device,
      title: document.title,
      version: document.version,
      sourceUrl: `https://example.invalid/${document.device}`,
      sha256: fakeHash(document.device.toLowerCase()),
      pageCount: document.pages.length,
    });

    addPages(store, documentId, document.pages, pageText);
  }

  return { store, path, pageText };
}

function addPages(
  store: KnowledgeStore,
  documentId: DocumentId,
  pages: readonly string[],
  pageText: Map<string, string>
): void {
  for (const [index, text] of pages.entries()) {
    const pageNumber = index + 1;
    store.insertPage(documentId, pageNumber, text);
    pageText.set(`${documentId}:${pageNumber}`, text);

    for (const chunk of chunkPage(text, pageNumber)) {
      store.insertChunk(documentId, chunk, TrigramModel.embed(chunk.text));
    }
  }
}
