import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { dirname, isAbsolute, resolve } from 'node:path';

/**
 * The recipe: what the corpus is made of, without any of it being in the repository.
 *
 * @remarks
 * Manuals from Siemens, Universal Robots, SICK and Festo are copyrighted and are not redistributed
 * here. What is versioned is this list — device, title, document version, the URL the file came
 * from and the SHA-256 of the exact bytes — so somebody who clones the repository does not find a
 * library, they find a list and a command that builds one on their own machine from documents they
 * are entitled to have.
 *
 * The hash is not bookkeeping. A document is the input to a system that will later put excerpts in
 * front of somebody standing next to a machine, and *the file at that URL today* is not a thing the
 * repository can promise. Ingesting bytes whose hash does not match the recipe is refused, which is
 * the same rule as everywhere else here: the absence of a decision is a refusal, never a permission.
 */

/** One document, as the recipe describes it. */
export type RecipeEntry = {
  readonly device: string;
  readonly title: string;
  readonly version: string;
  readonly sourceUrl: string;
  readonly sha256: string;
  /** Path to the local PDF, relative to the recipe file. Never committed. */
  readonly file: string;
};

/** The recipe file, parsed and checked. */
export type CorpusRecipe = {
  readonly recipePath: string;
  readonly documents: readonly RecipeEntry[];
};

const Sha256Pattern = /^[0-9a-f]{64}$/;

const RequiredFields: readonly (keyof RecipeEntry)[] = ['device', 'title', 'version', 'sourceUrl', 'sha256', 'file'];

/** Validates one entry, naming what is wrong rather than reporting that something is. */
function validateEntry(entry: Partial<RecipeEntry>, index: number): RecipeEntry {
  for (const field of RequiredFields) {
    if (typeof entry[field] !== 'string' || entry[field].trim() === '') {
      throw new Error(`Recipe entry ${index} is missing "${field}".`);
    }
  }

  const checked = entry as RecipeEntry;

  if (!Sha256Pattern.test(checked.sha256)) {
    throw new Error(`Recipe entry ${index} (${checked.device}) has a sha256 that is not 64 lowercase hex characters.`);
  }

  return checked;
}

/**
 * Reads and validates a recipe.
 *
 * @param recipePath The recipe file. Paths inside it are resolved against its directory.
 */
export async function readCorpusRecipe(recipePath: string): Promise<CorpusRecipe> {
  const parsed = JSON.parse(await readFile(recipePath, 'utf8')) as { documents?: Partial<RecipeEntry>[] };

  if (!Array.isArray(parsed.documents)) {
    throw new Error(`Recipe ${recipePath} has no "documents" array.`);
  }

  return {
    recipePath,
    documents: parsed.documents.map(validateEntry),
  };
}

/** Where an entry's file actually is on this machine. */
export function resolveEntryPath(recipe: CorpusRecipe, entry: RecipeEntry): string {
  return isAbsolute(entry.file) ? entry.file : resolve(dirname(recipe.recipePath), entry.file);
}

/** The SHA-256 of a file, lowercase hex. */
export async function hashFile(path: string): Promise<string> {
  return createHash('sha256').update(await readFile(path)).digest('hex');
}

/**
 * Refuses a file whose bytes are not the bytes the recipe describes.
 *
 * @param path The local file.
 * @param expected The hash from the recipe.
 * @throws When they differ. There is no flag to proceed anyway: a document nobody can identify has
 * no business becoming a citation.
 */
export async function verifyFileHash(path: string, expected: string): Promise<void> {
  const actual = await hashFile(path);

  if (actual !== expected) {
    throw new Error(`${path} hashes to ${actual}, and the recipe says ${expected}. Refusing to index it.`);
  }
}
