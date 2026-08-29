import Anthropic from '@anthropic-ai/sdk';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { UnusableGeneration, type Generation, type GenerationRequest, type Generator } from './generator.ts';
import { repositoryRoot } from './serverLocation.ts';
import type { TokenUsage } from './telemetry.ts';

/**
 * The model that writes the SCL.
 *
 * @remarks
 * Recorded with every run as the generator's name, so a measurement is attributable to a model
 * rather than to "the LLM". Changing it changes what the numbers are about.
 *
 * It is the default and not the only choice: `--model` names another, and the cheaper models cost
 * a fifth of this one per generation. The published measurement should be of a model somebody
 * chose; the twentieth attempt at getting the loop to run should not cost what this one costs.
 */
export const DefaultModel = 'claude-opus-5';

/**
 * Room for a whole cell.
 *
 * @remarks
 * The two-station cell expands to some six hundred lines of SCL, and a program cut off in the
 * middle of a function block does not fail as a truncation - it fails as a syntax error, and the
 * next attempt would be sent to fix a fault the generator never made.
 */
const MaxTokens = 32000;

/**
 * What is asked of the model, as data rather than as a string built at the call site.
 *
 * @remarks
 * The seam this module exists to have. The generator is a prompt, a response and the rules for
 * turning one into the other, and none of that needs a network to be tested - only the sender does.
 */
export type SclRequest = {
  readonly system: string;
  readonly prompt: string;
};

/**
 * What came back: the answer, and what it cost.
 *
 * @remarks
 * The usage is not optional here, unlike on a `Generation`. Anything that answers an SCL request
 * has been asked one, and a sender that could not say what it cost would put the harness back to
 * estimating - which is the thing this was built to stop.
 */
export type SclAnswer = {
  readonly text: string;
  readonly usage: TokenUsage;
  /**
   * Why the model stopped, as the API reported it, and what kinds of block came back.
   *
   * @remarks
   * Diagnostics, and they exist because of a run that could not be explained. Fifteen of the
   * forty-six generations on 2026-08-28 came back with no text at all, and the failure said only
   * "the model answered with no SCL" — which is a symptom that fits a truncated answer, a refusal
   * and a bug in this file equally well, so the sample could not be attributed to the model or to
   * us. `end_turn` with a thinking block and no text means one thing; `max_tokens` means another;
   * `refusal` means a third. Optional so a test double need not invent one.
   */
  readonly stopReason?: string | undefined;
  readonly blocks?: readonly string[] | undefined;
};

/** Something that can answer an SCL request. The real one calls the API; tests pass a double. */
export type MessageSender = (request: SclRequest) => Promise<SclAnswer>;

/**
 * A generator that asks a model for the cell's SCL.
 *
 * @param send How the request reaches a model.
 * @param model The model's name, recorded with the run.
 * @returns A generator the loop can use in place of the pattern expander.
 * @remarks
 * The counterpart to `createStubGenerator`, and the reason that one was built first: everything
 * around this - the loop, the server, the specifications, the download - was measured before a
 * model was ever involved, so a failure here can be attributed to the generation rather than
 * argued about.
 *
 * It does not retry. The loop above it already does, with the compiler's errors in hand, which is
 * the retry that means something; and the SDK retries transport failures twice on its own. A third
 * retry here would only blur which of the three was doing the work.
 */
export function createModelGenerator(send: MessageSender, model: string = DefaultModel): Generator {
  return {
    name: model,

    async generate(request: GenerationRequest): Promise<Generation> {
      const answer = await send({ system: SystemPrompt, prompt: buildPrompt(request) });

      // The usage travels with a source that may well not compile, and that is the point: an
      // attempt whose SCL the compiler rejects was paid for like any other, and a cost that counted
      // only the attempts that worked would understate the bill by the ones worth seeing.
      //
      // An answer with no SCL in it was paid for too. It used to throw a plain error and take its
      // cost with it, so the store recorded the cheap half of the sample and none of the expensive
      // failures; now the cost comes out inside the failure and the loop records it either way.
      try {
        return { source: extractScl(answer.text), usage: answer.usage };
      } catch (failure) {
        throw new UnusableGeneration(describeUnusable(failure, answer), answer.usage);
      }
    }
  };
}

/**
 * Refuses now if the key that will be needed is not there.
 *
 * @throws Error naming the variable, when it is unset or empty.
 * @remarks
 * Exported so it can be asked **before** anything expensive starts. `createApiSender` calls it too,
 * but by then a run has already opened TIA Portal, and finding out about a missing key after a
 * forty-five second startup is finding out late — most often after a `setx` in a terminal that was
 * never reopened, which is exactly the case that looks like the key *is* set.
 *
 * An empty value counts as missing. `ANTHROPIC_API_KEY=` in a shell profile sets the variable to
 * nothing, and a client built on it fails later with an authentication error that blames the key
 * rather than the fact that there isn't one.
 */
export function requireApiKey(): void {
  const key = process.env['ANTHROPIC_API_KEY'];

  if (key === undefined || key.trim().length === 0) {
    throw new Error(
      'ANTHROPIC_API_KEY is not set, so --generator model has nothing to ask. Put it in ' +
        'harness/.env, which git ignores, and start the run with `npm run run --` so that Node ' +
        'loads it.'
    );
  }
}

/**
 * The sender that actually calls the API.
 *
 * @param model Which model to ask.
 * @returns A sender, or a thrown error naming the missing key.
 * @remarks
 * Streamed, and not for the progress: a request this size is minutes of generation, and a
 * non-streaming call of that length is what HTTP timeouts are made of. `finalMessage()` gives back
 * the whole message once it has arrived, which is all this caller wants.
 *
 * The key comes from the environment and from nowhere else — `npm run run` puts it there by
 * loading `harness/.env`, which `.gitignore` has covered since before anything read it. A file is
 * the lesser of two evils, and the other one was tried first: a key exported for the whole Windows
 * account is read by every process that account starts, this editor included, and that bill lands
 * somewhere nobody is looking. The file is ignored by git and read by one command.
 */
export function createApiSender(model: string = DefaultModel): MessageSender {
  requireApiKey();

  const client = new Anthropic();

  return async (request: SclRequest): Promise<SclAnswer> => {
    const stream = client.messages.stream({
      model,
      max_tokens: MaxTokens,
      ...thinkingFor(model),
      system: request.system,
      messages: [{ role: 'user', content: request.prompt }]
    });

    const message = await stream.finalMessage();

    // Narrowed rather than indexed: the content is a union, and with thinking on the first block is
    // usually not the text one.
    const text = message.content
      .filter((block) => block.type === 'text')
      .map((block) => block.text)
      .join('\n');

    // `message.model` and not the argument: a request can be served by a model other than the one
    // named — a refusal fallback is the case that exists today — and pricing what actually ran is
    // the whole reason for recording this instead of estimating it.
    return {
      text,
      usage: readUsage(message.model, message.usage),
      stopReason: message.stop_reason ?? undefined,
      blocks: message.content.map((block) => block.type)
    };
  };
}

/**
 * Says why an answer could not be used, with the two facts that tell the causes apart.
 *
 * @param failure What {@link extractScl} refused.
 * @param answer The answer as it arrived.
 * @remarks
 * The message goes into the run's output and into the store, so the next unexplained sample is
 * explained by reading it rather than by paying for another one. That is the whole point: the
 * five-run sample cost ten euros and could not answer whether the fault was the model's or ours.
 */
function describeUnusable(failure: unknown, answer: SclAnswer): string {
  const reason = failure instanceof Error ? failure.message : String(failure);
  const blocks = answer.blocks === undefined ? 'unknown' : answer.blocks.join(', ') || 'none';

  return `${reason} Stop reason: ${answer.stopReason ?? 'unknown'}. Blocks: ${blocks}. Text: ${answer.text.length} character(s).`;
}

/**
 * The models that take adaptive thinking.
 *
 * @remarks
 * Not a preference: `thinking: { type: 'adaptive' }` is **rejected with a 400** by models older than
 * the 4.6 generation, which want a token budget instead. So the parameter cannot simply be sent to
 * whatever `--model` names, and this is the list of what it can be sent to.
 *
 * Found before it cost anything, by reading the API reference rather than by watching a run die:
 * the flag that made a cheaper model reachable made an unreachable request reachable with it.
 */
const AdaptiveThinkingModels: readonly string[] = ['claude-opus-5', 'claude-sonnet-5', 'claude-fable-5'];

/**
 * How to ask this model to think, as the fragment of a request that says so.
 *
 * @param model The model about to be asked.
 * @returns The thinking parameter, or nothing at all.
 * @remarks
 * An unknown model gets no thinking parameter rather than a guessed one. Omitting it is valid on
 * every model there has ever been; guessing wrong is a 400 that arrives after TIA Portal has been
 * started, and it would blame the model name rather than this decision.
 *
 * A model generating without thinking is a weaker generator, and that is a fair thing for a run to
 * measure - the run records which model it asked, so the number stays attributable.
 */
export function thinkingFor(model: string): { thinking?: { type: 'adaptive' } } {
  if (!AdaptiveThinkingModels.includes(model)) {
    return {};
  }

  return { thinking: { type: 'adaptive' } };
}

/**
 * Turns the API's usage into the shape the store records.
 *
 * @param model The model that actually answered.
 * @param usage What the response reported.
 * @returns The four counts, with the cache ones defaulted to zero.
 * @remarks
 * The cache fields are nullable on the wire and this harness does not cache, so today they arrive as
 * null. Defaulted to zero rather than left undefined because the column is NOT NULL and, more to the
 * point, "no tokens were cached" is what actually happened.
 */
export function readUsage(model: string, usage: Anthropic.Usage): TokenUsage {
  return {
    model,
    inputTokens: usage.input_tokens,
    outputTokens: usage.output_tokens,
    cacheCreationTokens: usage.cache_creation_input_tokens ?? 0,
    cacheReadTokens: usage.cache_read_input_tokens ?? 0
  };
}

/**
 * What the model is told about the job, once.
 *
 * @remarks
 * The constraints are the ones a program has to meet to be measurable at all: it must compile in
 * TIA Portal V20, and the acceptance steps read specific tags, so a cell that works under other
 * names would fail for a reason that has nothing to do with whether it works. Everything else -
 * how the stations coordinate, what the sequence does - is deliberately left open, because that is
 * the part being measured.
 */
export const SystemPrompt = [
  'You write SCL (Structured Control Language) for a Siemens S7-1500 PLC, compiled by TIA Portal V20.',
  '',
  'Your answer is written straight into a project and compiled. So:',
  '- Answer with SCL only. No explanation, no commentary outside SCL comments.',
  '- Every block you need goes in one answer, in dependency order: a block must be declared before',
  '  the block that instantiates it.',
  '- Use only SCL that TIA Portal V20 accepts. REGION and typed constants are fine; anything from',
  '  another vendor is not.',
  '- Declare every variable you use. An undeclared tag is the most common way this fails.',
  '',
  'The program has to be observable from outside: a test writes and reads tags of the cell data',
  'block by name while the CPU runs, so those names are part of the specification and not yours to',
  'choose.'
].join('\n');

/**
 * The request for one attempt.
 *
 * @remarks
 * The cell specification goes in as the JSON it is, rather than as prose about it: it is the same
 * file the pattern expander reads, so the two generators are answering the same question.
 *
 * On a later attempt the compiler's own errors go in unedited. Summarising them would throw away
 * the line numbers and the block names, which are the parts that make a fix possible.
 */
export function buildPrompt(request: GenerationRequest): string {
  const { specification, attempt, previousErrors } = request;
  const cell = readFileSync(join(repositoryRoot(), specification.cellPath), 'utf8');

  const parts = [
    `Goal: ${specification.goal}`,
    '',
    'Cell specification:',
    cell,
    '',
    'The acceptance check writes and reads these tags, so they must exist with these exact names:',
    describeTags(request),
    ''
  ];

  if (attempt > 1 && previousErrors.length > 0) {
    parts.push(
      `Attempt ${attempt}. The previous answer did not compile. TIA Portal reported:`,
      previousErrors.join('\n'),
      '',
      'Fix those and answer with the complete SCL again, not with a patch.'
    );
  }

  return parts.join('\n');
}

/** The tags the acceptance steps touch, which is the observable half of the specification. */
function describeTags(request: GenerationRequest): string {
  const tags = new Set(request.specification.acceptance.map((step) => step.tag));

  return [...tags].map((tag) => `- ${tag}`).join('\n');
}

/**
 * Pulls the SCL out of an answer.
 *
 * @param answer What the model said.
 * @returns The source, without the fences a model tends to wrap it in.
 * @remarks
 * Fences are stripped rather than forbidden. The instruction not to use them is in the system
 * prompt, and a model that uses them anyway has still answered the question - failing the attempt
 * over punctuation would spend a compile to report a formatting preference.
 *
 * An empty answer is not source and is refused here, where it is still cheap. Written into a
 * project it would arrive as "0 blocks generated", which reads like a server fault.
 */
export function extractScl(answer: string): string {
  const fenced = /```(?:scl|pascal|st)?\s*\n([\s\S]*?)```/i.exec(answer);
  const source = (fenced === null ? answer : fenced[1] ?? '').trim();

  if (source.length === 0) {
    throw new Error('The model answered with no SCL at all.');
  }

  return source;
}
