import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import type { ChunkId, CorpusDocument, DocumentId } from './citation.ts';
import type { EmbeddingModel } from './embeddingModel.ts';
import type { IndexableChunk } from './bm25.ts';
import type { PageChunk } from './chunker.ts';

/**
 * The local index: documents, their pages, their chunks and the vectors over them.
 *
 * @remarks
 * It is a second SQLite file, next to the measurement store rather than inside it. The two answer
 * different questions and have different lifetimes — `metrics.db` is a record of runs that must
 * never be rebuilt, while this one is derived from PDFs and can be thrown away and rebuilt from
 * the recipe at any time. Mixing them would put a rebuildable table beside an irreplaceable one.
 *
 * Pages are stored whole, in full, alongside the chunks cut from them. That looks like duplication
 * and is the point: it is what lets a test assert that every excerpt is a literal span of its page,
 * and what lets a citation widen to its surroundings later without re-reading the PDF.
 */

/** The schema this build understands. There is nothing to migrate from yet, and no version 0. */
export const KnowledgeSchemaVersion = 1;

/** A chunk as it comes back for citing: the text, and where it is. */
export type StoredChunk = {
  readonly chunkId: ChunkId;
  readonly documentId: DocumentId;
  readonly pageNumber: number;
  readonly text: string;
};

/** What a document needs before it is inserted. The identifier comes from the store. */
export type DocumentDescription = Omit<CorpusDocument, 'documentId' | 'ingestedAt'>;

const Schema = `
CREATE TABLE IF NOT EXISTS knowledge_schema_version (
  version INTEGER NOT NULL,
  embedding_model TEXT NOT NULL,
  dimensions INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS document (
  document_id INTEGER PRIMARY KEY AUTOINCREMENT,
  device TEXT NOT NULL,
  title TEXT NOT NULL,
  version TEXT NOT NULL,
  source_url TEXT NOT NULL,
  sha256 TEXT NOT NULL UNIQUE,
  page_count INTEGER NOT NULL,
  ingested_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS page (
  document_id INTEGER NOT NULL REFERENCES document(document_id),
  page_number INTEGER NOT NULL,
  text TEXT NOT NULL,
  PRIMARY KEY (document_id, page_number)
);
CREATE TABLE IF NOT EXISTS chunk (
  chunk_id INTEGER PRIMARY KEY AUTOINCREMENT,
  document_id INTEGER NOT NULL REFERENCES document(document_id),
  page_number INTEGER NOT NULL,
  start_offset INTEGER NOT NULL,
  end_offset INTEGER NOT NULL,
  text TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS chunk_vector (
  chunk_id INTEGER PRIMARY KEY REFERENCES chunk(chunk_id),
  vector BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS chunk_by_document ON chunk (document_id);
`;

/** The index, opened for reading or for writing. One class, because there is one file. */
export class KnowledgeStore {
  private readonly database: DatabaseSync;

  private constructor(database: DatabaseSync) {
    this.database = database;
  }

  /**
   * Opens the index, creating it if it is not there, and refuses one built by another model.
   *
   * @param path Where the file lives. Its directory is created if missing.
   * @param model The model whose vectors this session will write or compare against.
   * @remarks
   * The refusal is the governance rule applied to a store: an index whose vectors came from a
   * different model, or a different width, cannot be compared with this one, and a cosine over
   * mismatched vectors returns a number that means nothing rather than an error. Refusing is the
   * only answer that cannot mislead. Rebuilding from the recipe is the fix, and it is cheap.
   */
  static open(path: string, model: EmbeddingModel): KnowledgeStore {
    mkdirSync(dirname(path), { recursive: true });
    const database = new DatabaseSync(path);
    database.exec(Schema);
    verifyStamp(database, model);

    return new KnowledgeStore(database);
  }

  close(): void {
    this.database.close();
  }

  /** Every indexed document, newest first. */
  listDocuments(): CorpusDocument[] {
    const rows = this.database
      .prepare('SELECT * FROM document ORDER BY ingested_at DESC')
      .all() as unknown as DocumentRow[];

    return rows.map(toCorpusDocument);
  }

  /** The document with this content hash, or undefined. Used to skip re-ingesting a file. */
  documentByHash(sha256: string): CorpusDocument | undefined {
    const row = this.database
      .prepare('SELECT * FROM document WHERE sha256 = ?')
      .get(sha256) as unknown as DocumentRow | undefined;

    return row === undefined ? undefined : toCorpusDocument(row);
  }

  /** Inserts a document and returns its identifier. Pages and chunks follow. */
  insertDocument(description: DocumentDescription): DocumentId {
    const result = this.database
      .prepare(`INSERT INTO document (device, title, version, source_url, sha256, page_count, ingested_at)
                VALUES (?, ?, ?, ?, ?, ?, ?)`)
      .run(
        description.device,
        description.title,
        description.version,
        description.sourceUrl,
        description.sha256,
        description.pageCount,
        Date.now(),
      );

    return Number(result.lastInsertRowid) as DocumentId;
  }

  /** Stores one page whole, exactly as it was extracted. */
  insertPage(documentId: DocumentId, pageNumber: number, text: string): void {
    this.database
      .prepare('INSERT OR REPLACE INTO page (document_id, page_number, text) VALUES (?, ?, ?)')
      .run(documentId, pageNumber, text);
  }

  /** Stores one chunk and its vector together: a chunk without a vector is half indexed. */
  insertChunk(documentId: DocumentId, chunk: PageChunk, vector: Float32Array): ChunkId {
    const result = this.database
      .prepare(`INSERT INTO chunk (document_id, page_number, start_offset, end_offset, text)
                VALUES (?, ?, ?, ?, ?)`)
      .run(documentId, chunk.pageNumber, chunk.startOffset, chunk.endOffset, chunk.text);

    const chunkId = Number(result.lastInsertRowid) as ChunkId;
    this.database
      .prepare('INSERT INTO chunk_vector (chunk_id, vector) VALUES (?, ?)')
      .run(chunkId, toBlob(vector));

    return chunkId;
  }

  /** Every chunk, for the BM25 index that is rebuilt whenever the store is opened. */
  readIndexableChunks(): IndexableChunk[] {
    return this.database
      .prepare('SELECT chunk_id AS chunkId, text FROM chunk ORDER BY chunk_id')
      .all() as unknown as IndexableChunk[];
  }

  /** Every vector, for the brute-force scan. Eighteen thousand of these is milliseconds. */
  readVectors(): { chunkId: number; vector: Float32Array }[] {
    const rows = this.database
      .prepare('SELECT chunk_id AS chunkId, vector FROM chunk_vector ORDER BY chunk_id')
      .all() as unknown as { chunkId: number; vector: Uint8Array }[];

    return rows.map((row) => ({ chunkId: row.chunkId, vector: fromBlob(row.vector) }));
  }

  /** One chunk with the document it belongs to: what turns a ranked identifier into a citation. */
  readChunk(chunkId: number): (StoredChunk & { document: CorpusDocument }) | undefined {
    const row = this.database
      .prepare(`SELECT c.chunk_id, c.page_number, c.text AS chunk_text, d.*
                FROM chunk c JOIN document d ON d.document_id = c.document_id
                WHERE c.chunk_id = ?`)
      .get(chunkId) as unknown as (DocumentRow & ChunkRow) | undefined;

    if (row === undefined) {
      return undefined;
    }

    return {
      chunkId: row.chunk_id as ChunkId,
      documentId: row.document_id as DocumentId,
      pageNumber: row.page_number,
      text: row.chunk_text,
      document: toCorpusDocument(row),
    };
  }

  /** One page, whole, as it was stored. The verbatim test reads this. */
  readPage(documentId: DocumentId, pageNumber: number): string | undefined {
    const row = this.database
      .prepare('SELECT text FROM page WHERE document_id = ? AND page_number = ?')
      .get(documentId, pageNumber) as unknown as { text: string } | undefined;

    return row?.text;
  }
}

type DocumentRow = {
  document_id: number;
  device: string;
  title: string;
  version: string;
  source_url: string;
  sha256: string;
  page_count: number;
  ingested_at: number;
};

type ChunkRow = { chunk_id: number; page_number: number; chunk_text: string };

function toCorpusDocument(row: DocumentRow): CorpusDocument {
  return {
    documentId: row.document_id as DocumentId,
    device: row.device,
    title: row.title,
    version: row.version,
    sourceUrl: row.source_url,
    sha256: row.sha256,
    pageCount: row.page_count,
    ingestedAt: row.ingested_at,
  };
}

/** Stamps an empty index, or refuses one this model cannot read. */
function verifyStamp(database: DatabaseSync, model: EmbeddingModel): void {
  const existing = database
    .prepare('SELECT version, embedding_model AS model, dimensions FROM knowledge_schema_version')
    .get() as unknown as { version: number; model: string; dimensions: number } | undefined;

  if (existing === undefined) {
    database
      .prepare('INSERT INTO knowledge_schema_version (version, embedding_model, dimensions) VALUES (?, ?, ?)')
      .run(KnowledgeSchemaVersion, model.name, model.dimensions);

    return;
  }

  if (existing.version !== KnowledgeSchemaVersion) {
    throw new Error(`Knowledge index is schema version ${existing.version}; this build reads ${KnowledgeSchemaVersion}. Rebuild it from the recipe.`);
  }

  if (existing.model !== model.name || existing.dimensions !== model.dimensions) {
    throw new Error(`Knowledge index was built by ${existing.model} at ${existing.dimensions} dimensions, not ${model.name} at ${model.dimensions}. Rebuild it from the recipe.`);
  }
}

function toBlob(vector: Float32Array): Uint8Array {
  return new Uint8Array(vector.buffer.slice(vector.byteOffset, vector.byteOffset + vector.byteLength));
}

function fromBlob(blob: Uint8Array): Float32Array {
  const copy = blob.slice();

  return new Float32Array(copy.buffer, copy.byteOffset, copy.byteLength / Float32Array.BYTES_PER_ELEMENT);
}
