import Anthropic from '@anthropic-ai/sdk';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { GenerationRequest, Generator } from './generator.ts';
import { repositoryRoot } from './serverLocation.ts';

/**
 * The model that writes the SCL.
 *
 * @remarks
 * Recorded with every run as the generator's name, so a measurement is attributable to a model
 * rather than to "the LLM". Changing it changes what the numbers are about.
 */
const DefaultModel = 'claude-opus-5';

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

/** Something that can answer an SCL request. The real one calls the API; tests pass a double. */
export type MessageSender = (request: SclRequest) => Promise<string>;

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

    async generate(request: GenerationRequest): Promise<string> {
      const answer = await send({ system: SystemPrompt, prompt: buildPrompt(request) });

      return extractScl(answer);
    }
  };
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
 * The key comes from the environment and from nowhere else. A key in a file in this repository
 * would be a key in the repository's history five minutes later.
 */
export function createApiSender(model: string = DefaultModel): MessageSender {
  if (process.env['ANTHROPIC_API_KEY'] === undefined) {
    throw new Error(
      'ANTHROPIC_API_KEY is not set, so there is nothing to ask. Set it in the environment; do not ' +
        'put it in a file in this repository.'
    );
  }

  const client = new Anthropic();

  return async (request: SclRequest): Promise<string> => {
    const stream = client.messages.stream({
      model,
      max_tokens: MaxTokens,
      thinking: { type: 'adaptive' },
      system: request.system,
      messages: [{ role: 'user', content: request.prompt }]
    });

    const message = await stream.finalMessage();

    // Narrowed rather than indexed: the content is a union, and with thinking on the first block is
    // usually not the text one.
    return message.content
      .filter((block) => block.type === 'text')
      .map((block) => block.text)
      .join('\n');
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
const SystemPrompt = [
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
