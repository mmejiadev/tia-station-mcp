import { existsSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { parseFlags } from '../options.ts';
import { repositoryRoot } from '../serverLocation.ts';
import { HybridSearch } from './hybridSearch.ts';
import { KnowledgeStore } from './knowledgeStore.ts';
import { TrigramModel } from './lexicalVector.ts';
import { renderLookup } from './renderCitations.ts';

/**
 * Asks the local index a question and prints what it can cite, or that it cannot.
 *
 * @remarks
 * This is what the `hardware-lookup` skill runs. It answers on standard output in one of two
 * shapes — text for a person, JSON for a caller — and both come from the same lookup and the same
 * renderer, so the skill cannot be shown something the terminal would not show.
 *
 * An index that does not exist yet is not an error worth a stack trace: it is somebody who has not
 * built one, and the answer names the command that builds it.
 */

type Options = {
  readonly indexPath: string;
  readonly query: string;
  readonly device: string | undefined;
  readonly limit: number | undefined;
  readonly format: 'text' | 'json';
};

const Usage =
  'Usage: node src/knowledge/hardwareLookup.ts --query "<question>" [--device <name>] ' +
  '[--limit <n>] [--index <file>] [--format text|json]. Every flag takes a value.';

function main(): number {
  const options = parseOptions(process.argv.slice(2));

  if (!existsSync(options.indexPath)) {
    console.log(`No knowledge index at ${options.indexPath}. Build one with: npm run knowledge:index`);

    return 1;
  }

  const store = KnowledgeStore.open(options.indexPath, TrigramModel);

  try {
    return answer(new HybridSearch(store, TrigramModel), options);
  } finally {
    store.close();
  }
}

/** Runs the lookup and prints it. Returns 0 for a citation and 2 for an honest silence. */
function answer(search: HybridSearch, options: Options): number {
  const result = search.lookup(options.query, {
    ...(options.device === undefined ? {} : { device: options.device }),
    ...(options.limit === undefined ? {} : { limit: options.limit }),
  });

  if (options.format === 'json') {
    console.log(JSON.stringify({ ...result, indexedChunks: search.indexedChunks }, null, 2));
  } else {
    console.log(renderLookup(result));
  }

  // Not-found is a correct answer, not a failure, and the exit code says which one it was so a
  // caller can tell the two apart without parsing the text.
  return result.outcome === 'cited' ? 0 : 2;
}

function parseOptions(args: readonly string[]): Options {
  const values = parseFlags(args, Usage);
  const query = values.get('--query');

  if (query === undefined || query.trim() === '') {
    throw new Error(`No question was asked. ${Usage}`);
  }

  const format = values.get('--format') ?? 'text';

  if (format !== 'text' && format !== 'json') {
    throw new Error(`Unrecognised format: ${format}. ${Usage}`);
  }

  return {
    indexPath: resolve(values.get('--index') ?? join(repositoryRoot(), '.tia-mcp', 'harness', 'knowledge.db')),
    query,
    device: values.get('--device'),
    limit: readLimit(values.get('--limit')),
    format,
  };
}

function readLimit(value: string | undefined): number | undefined {
  if (value === undefined) {
    return undefined;
  }

  const limit = Number(value);

  if (!Number.isInteger(limit) || limit < 1) {
    throw new Error(`--limit must be a positive whole number, not '${value}'.`);
  }

  return limit;
}

process.exitCode = main();
