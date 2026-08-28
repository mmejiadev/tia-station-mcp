import { join, resolve } from 'node:path';
import { parseFlags } from '../options.ts';
import { repositoryRoot } from '../serverLocation.ts';
import { ingestDocument, type IngestionSummary } from './ingestDocument.ts';
import { readCorpusRecipe, resolveEntryPath, type CorpusRecipe } from './corpusRecipe.ts';
import { KnowledgeStore } from './knowledgeStore.ts';
import { TrigramModel } from './lexicalVector.ts';

/**
 * Builds the local index from the recipe.
 *
 * @remarks
 * The whole of stage 1's ingestion, and it fetches nothing. Every document is a file already on
 * this machine, named by a recipe entry whose hash it must match. Downloading is stage 4 and
 * arrives with a whitelist and a quarantine, because *let the agent go and read the documentation*
 * is also a way to get attacker-controlled text into a system that plans actions on machinery.
 *
 * A missing file is reported and the rest of the corpus is still indexed: somebody who has three
 * of the four manuals should get an index of three, told plainly which one is absent, rather than
 * nothing. A file that is present and hashes wrong is a different thing and stops that document —
 * `verifyFileHash` throws, and the failure is named.
 */

/** Where the index and the recipe are, unless told otherwise. */
type Options = {
  readonly indexPath: string;
  readonly recipePath: string;
};

async function main(): Promise<number> {
  const options = parseOptions(process.argv.slice(2));
  const recipe = await readCorpusRecipe(options.recipePath);
  const store = KnowledgeStore.open(options.indexPath, TrigramModel);

  try {
    const failures = await indexAll(store, recipe);

    console.log('');
    console.log(`Index: ${options.indexPath}`);
    console.log(`Embedding model: ${TrigramModel.name} (lexical, not semantic — see lexicalVector.ts)`);

    return failures === 0 ? 0 : 1;
  } finally {
    store.close();
  }
}

/** Indexes every entry, reporting each as it goes. Returns how many could not be indexed. */
async function indexAll(store: KnowledgeStore, recipe: CorpusRecipe): Promise<number> {
  let failures = 0;

  console.log(`Recipe: ${recipe.recipePath}`);
  console.log('');

  for (const entry of recipe.documents) {
    try {
      report(await ingestDocument(store, TrigramModel, entry, resolveEntryPath(recipe, entry)));
    } catch (failure) {
      failures += 1;
      console.log(`FAILED   ${entry.device} — ${(failure as Error).message}`);
    }
  }

  return failures;
}

function report(summary: IngestionSummary): void {
  if (summary.alreadyIndexed) {
    console.log(`INDEXED  ${summary.device} — already in the index, unchanged (${summary.pages} pages)`);

    return;
  }

  console.log(`ADDED    ${summary.device} — ${summary.pages} pages, ${summary.chunks} chunks`);
}

function parseOptions(args: readonly string[]): Options {
  const values = parseFlags(
    args,
    'Usage: node src/knowledge/knowledgeIndex.ts [--index <file>] [--recipe <file>]. Every flag takes a value.'
  );

  const harnessRoot = join(repositoryRoot(), '.tia-mcp', 'harness');

  return {
    indexPath: resolve(values.get('--index') ?? join(harnessRoot, 'knowledge.db')),
    recipePath: resolve(values.get('--recipe') ?? join(repositoryRoot(), 'harness', 'corpus', 'recipe.json')),
  };
}

process.exitCode = await main();
