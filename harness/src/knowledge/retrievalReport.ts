import { existsSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { parseFlags } from '../options.ts';
import { repositoryRoot } from '../serverLocation.ts';
import { HybridSearch } from './hybridSearch.ts';
import { KnowledgeStore } from './knowledgeStore.ts';
import { TrigramModel } from './lexicalVector.ts';
import {
  evaluateRetrieval,
  judgeAbstention,
  judgeCitation,
  RequiredAbstentionRate,
  RequiredCitationPrecision,
  type AbstentionJudgement,
  type CitationJudgement,
  type EvaluationSet,
  type RetrievalVerdict
} from './retrievalGate.ts';

/**
 * Asks the retrieval gate whether the search is good enough to be shown to anybody.
 *
 * @remarks
 * The counterpart of `gateReport.ts` for the knowledge layer. It runs every question in the
 * evaluation set through the same `HybridSearch` the `hardware-lookup` skill uses, so the number it
 * reports is about the thing people actually get.
 *
 * **The ground truth is re-checked against the corpus before anything is scored.** An evaluation set
 * whose recorded pages have drifted from the documents would report a precision about nothing, and
 * it would drift silently — a re-ingested corpus, a different edition of a manual. Checking costs
 * milliseconds and turns that into a refusal.
 */

type Options = {
  readonly indexPath: string;
  readonly questionsPath: string;
  readonly verbose: boolean;
};

const Usage =
  'Usage: node src/knowledge/retrievalReport.ts [--index <file>] [--questions <file>] [--verbose true]. ' +
  'Every flag takes a value.';

function main(): number {
  const options = parseOptions(process.argv.slice(2));

  if (!existsSync(options.indexPath)) {
    console.log(`No knowledge index at ${options.indexPath}. Build one with: npm run knowledge:index`);

    return 1;
  }

  const set = readQuestions(options.questionsPath);
  const store = KnowledgeStore.open(options.indexPath, TrigramModel);

  try {
    const drift = groundTruthDrift(store, set);

    if (drift.length > 0) {
      console.log('The evaluation set does not match the corpus, so nothing was scored:');
      drift.forEach((line) => console.log(`  ${line}`));

      return 1;
    }

    return report(evaluate(new HybridSearch(store, TrigramModel), set), options.verbose);
  } finally {
    store.close();
  }
}

/**
 * Re-checks every recorded page and phrase against the documents.
 *
 * @param store The corpus.
 * @param set The questions.
 * @returns One line per question whose ground truth no longer holds. Empty when it all holds.
 */
function groundTruthDrift(store: KnowledgeStore, set: EvaluationSet): string[] {
  const documents = new Map(store.listDocuments().map((document) => [document.device, document.documentId]));
  const drift: string[] = [];

  for (const question of set.answerable) {
    const documentId = documents.get(question.device);

    if (documentId === undefined) {
      drift.push(`no indexed document for '${question.device}'`);

      continue;
    }

    const page = store.readPage(documentId, question.page);

    if (page === undefined) {
      drift.push(`${question.device} has no page ${question.page}`);

      continue;
    }

    if (!page.includes(question.contains)) {
      drift.push(`${question.device} p${question.page} no longer contains '${question.contains}'`);
    }
  }

  return drift;
}

function evaluate(search: HybridSearch, set: EvaluationSet): RetrievalVerdict {
  const citations: CitationJudgement[] = set.answerable.map((question) =>
    judgeCitation(question, search.lookup(question.question))
  );

  const abstentions: AbstentionJudgement[] = set.unanswerable.map((question) =>
    judgeAbstention(question, search.lookup(question.question))
  );

  return evaluateRetrieval(citations, abstentions);
}

/** Prints the verdict. Returns 0 when the gate opens, 1 when it does not. */
function report(verdict: RetrievalVerdict, verbose: boolean): number {
  console.log('');
  console.log('Retrieval gate');
  console.log('');
  console.log(rate('citation precision', verdict.citationPrecision, RequiredCitationPrecision, verdict.citations.length));
  console.log(rate('correct abstention', verdict.abstentionRate, RequiredAbstentionRate, verdict.abstentions.length));
  console.log('');

  if (verbose) {
    printMisses(verdict);
  }

  console.log(
    verdict.open
      ? 'The retrieval gate is open. The cited safety surface may be built on this.'
      : 'The retrieval gate is shut. Nothing further in the knowledge layer is shown to anybody.'
  );

  return verdict.open ? 0 : 1;
}

function rate(name: string, measured: number, required: number, sample: number): string {
  const verdict = measured >= required ? 'MET    ' : 'NOT MET';

  return `${verdict}  ${name}: ${percentage(measured)} of n=${sample}, ${percentage(required)} required`;
}

/** Names what went wrong, because a rate nobody can look behind is a rate nobody can fix. */
function printMisses(verdict: RetrievalVerdict): void {
  const missed = verdict.citations.filter((one) => !one.found);
  const spoke = verdict.abstentions.filter((one) => !one.abstained);

  missed.forEach((one) =>
    console.log(`  missed   ${one.question}  [${one.outcome}, cited ${one.citedPages.join(', ') || 'nothing'}]`)
  );

  spoke.forEach((one) => console.log(`  answered ${one.question}  [cited ${one.citedInstead.join(', ')}]`));

  if (missed.length > 0 || spoke.length > 0) {
    console.log('');
  }
}

function percentage(ratio: number): string {
  return `${(ratio * 100).toFixed(0)}%`;
}

function readQuestions(path: string): EvaluationSet {
  const set = JSON.parse(readFileSync(path, 'utf8')) as Partial<EvaluationSet>;

  if (!Array.isArray(set.answerable) || !Array.isArray(set.unanswerable)) {
    throw new Error(`${path} needs an 'answerable' and an 'unanswerable' list.`);
  }

  return { answerable: set.answerable, unanswerable: set.unanswerable };
}

function parseOptions(args: readonly string[]): Options {
  const values = parseFlags(args, Usage);

  return {
    indexPath: resolve(
      values.get('--index') ?? join(repositoryRoot(), '.tia-mcp', 'harness', 'knowledge.db')
    ),
    questionsPath: resolve(
      values.get('--questions') ?? join(repositoryRoot(), 'harness', 'knowledge-eval', 'questions.json')
    ),
    verbose: values.get('--verbose') !== 'false'
  };
}

process.exitCode = main();
