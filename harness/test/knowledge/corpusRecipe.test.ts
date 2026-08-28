import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { hashFile, readCorpusRecipe, resolveEntryPath, verifyFileHash } from '../../src/knowledge/corpusRecipe.ts';

/**
 * The recipe is the only part of the corpus that is in the repository, and the hash is the only
 * thing that ties a file on somebody's disk to the document this project meant.
 *
 * A document is the input to a system that will put excerpts in front of a person standing next to
 * a machine. *The file at that URL today* is not something the repository can promise, so a file
 * whose bytes are not the bytes the recipe describes is refused. There is no flag to proceed anyway.
 */
describe('corpus recipe', () => {
  it('reads the entries a document needs to be cited', async () => {
    const directory = temporaryDirectory();

    try {
      const recipePath = write(directory, 'recipe.json', JSON.stringify({ documents: [entry()] }));

      const recipe = await readCorpusRecipe(recipePath);

      assert.equal(recipe.documents.length, 1);
      assert.equal(recipe.documents[0]?.device, 'UR5e');
      assert.equal(resolveEntryPath(recipe, recipe.documents[0]!), join(directory, 'manual.pdf'));
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('names the field that is missing rather than reporting that something is', async () => {
    const directory = temporaryDirectory();

    try {
      const { sha256: _removed, ...withoutHash } = entry();
      const recipePath = write(directory, 'recipe.json', JSON.stringify({ documents: [withoutHash] }));

      await assert.rejects(() => readCorpusRecipe(recipePath), /missing "sha256"/);
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('refuses a hash that is not a hash', async () => {
    const directory = temporaryDirectory();

    try {
      const recipePath = write(directory, 'recipe.json', JSON.stringify({ documents: [{ ...entry(), sha256: 'abc' }] }));

      await assert.rejects(() => readCorpusRecipe(recipePath), /64 lowercase hex/);
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('refuses a file whose bytes are not the bytes the recipe describes', async () => {
    const directory = temporaryDirectory();

    try {
      const path = write(directory, 'manual.pdf', 'not the document the recipe means');

      await assert.rejects(() => verifyFileHash(path, '0'.repeat(64)), /Refusing to index it/);
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });

  it('accepts the file it describes', async () => {
    const directory = temporaryDirectory();

    try {
      const path = write(directory, 'manual.pdf', 'the document the recipe means');

      await verifyFileHash(path, await hashFile(path));
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });
});

function entry(): Record<string, string> {
  return {
    device: 'UR5e',
    title: 'UR5e user manual',
    version: 'SW 5.16',
    sourceUrl: 'https://example.invalid/ur5e.pdf',
    sha256: 'c1689d2760601b5b8559176221aa8c7bdebc4ff346fb70cda2bff2eef48faa4e',
    file: 'manual.pdf',
  };
}

function write(directory: string, name: string, contents: string): string {
  const path = join(directory, name);
  writeFileSync(path, contents);

  return path;
}

function temporaryDirectory(): string {
  return mkdtempSync(join(tmpdir(), 'recipe-'));
}
