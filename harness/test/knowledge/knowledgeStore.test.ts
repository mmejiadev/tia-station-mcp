import assert from 'node:assert/strict';
import { DatabaseSync } from 'node:sqlite';
import { describe, it } from 'node:test';
import { KnowledgeStore } from '../../src/knowledge/knowledgeStore.ts';
import { TrigramModel } from '../../src/knowledge/lexicalVector.ts';
import { buildTemporaryIndex } from './temporaryIndex.ts';
import type { EmbeddingModel } from '../../src/knowledge/embeddingModel.ts';

/**
 * The store keeps the two things a citation is made of: the page and the chunk cut from it.
 *
 * The refusals below are the governance rule applied to a file — the absence of a decision is a
 * refusal, never a permission. An index this build cannot read must say so, because the alternative
 * is a cosine over vectors from two different models, which is a number that means nothing and
 * looks exactly like one that means something.
 */
describe('KnowledgeStore', () => {
  it('stores every page whole, so an excerpt can be checked against its source', () => {
    const index = buildTemporaryIndex([
      { device: 'DSBC', title: 'DSBC cylinder', version: '2018', pages: [pageText()] },
    ]);

    try {
      const document = index.store.listDocuments()[0];

      assert.ok(document !== undefined);
      assert.equal(index.store.readPage(document.documentId, 1), pageText());
    } finally {
      index.store.close();
    }
  });

  it('finds a document by its content hash, so an unchanged file is not indexed twice', () => {
    const index = buildTemporaryIndex([
      { device: 'DSBC', title: 'DSBC cylinder', version: '2018', pages: [pageText()] },
    ]);

    try {
      const document = index.store.listDocuments()[0];

      assert.ok(document !== undefined);
      assert.equal(index.store.documentByHash(document.sha256)?.device, 'DSBC');
      assert.equal(index.store.documentByHash('0'.repeat(64)), undefined);
    } finally {
      index.store.close();
    }
  });

  it('returns a chunk with the document it came from, which is what a citation needs', () => {
    const index = buildTemporaryIndex([
      { device: 'DSBC', title: 'DSBC cylinder', version: '2018', pages: [pageText()] },
    ]);

    try {
      const first = index.store.readIndexableChunks()[0];

      assert.ok(first !== undefined);

      const stored = index.store.readChunk(first.chunkId);

      assert.equal(stored?.document.device, 'DSBC');
      assert.equal(stored?.pageNumber, 1);
      assert.ok(pageText().includes(stored?.text ?? ''), 'the chunk is not a span of its page');
    } finally {
      index.store.close();
    }
  });

  it('refuses an index built by a different embedding model', () => {
    const index = buildTemporaryIndex([
      { device: 'DSBC', title: 'DSBC cylinder', version: '2018', pages: [pageText()] },
    ]);
    index.store.close();

    assert.throws(() => KnowledgeStore.open(index.path, otherModel()), /Rebuild it from the recipe/);
  });

  it('refuses an index written by a schema it does not understand', () => {
    const index = buildTemporaryIndex([
      { device: 'DSBC', title: 'DSBC cylinder', version: '2018', pages: [pageText()] },
    ]);
    index.store.close();

    stampVersion(index.path, 99);

    assert.throws(() => KnowledgeStore.open(index.path, TrigramModel), /schema version 99/);
  });
});

/** A model that differs only in name and width, which is exactly what must be caught. */
function otherModel(): EmbeddingModel {
  return { name: 'something-else', dimensions: 768, embed: () => new Float32Array(768) };
}

function stampVersion(path: string, version: number): void {
  const database = new DatabaseSync(path);

  try {
    database.prepare('UPDATE knowledge_schema_version SET version = ?').run(version);
  } finally {
    database.close();
  }
}

function pageText(): string {
  return [
    'Cushioning: DSBC-...-P elastic cushioning rings or pads at both ends of the cylinder.',
    'DSBC-...-PPV pneumatic cushioning, adjustable at both ends, for higher impact energies.',
  ].join('\n');
}
