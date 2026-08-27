import type { CopilotBrief } from './copilotBrief.ts';
import type { TokenUsage } from './telemetry.ts';

/**
 * The model the copilot asks unless told otherwise.
 *
 * @remarks
 * The cheap one, and not by accident. A chat is asked many short questions where a generator is
 * asked a few long ones, so the model that costs a fifth as much is the right default for the turn
 * somebody fires off to check a number. `--chat-model` names another for a session that wants it.
 */
export const DefaultChatModel = 'claude-haiku-4-5';

/**
 * The most the copilot may write in one answer.
 *
 * @remarks
 * A thousand tokens, which is a long paragraph and change. This is the only hard ceiling on what a
 * turn can cost, and it belongs here rather than in a setting: the answers this is for are "how many
 * runs passed" and "which specification is the slowest", and one that needs more than this is one
 * where the tables on the other tabs are the better answer anyway.
 */
const MaxAnswerTokens = 1000;

/** The most a single question may be. Longer is refused, not truncated: a cut question is a different question. */
const MaxQuestionCharacters = 2000;

/** The most turns of history a request may carry, counting both sides. */
const MaxHistoryTurns = 20;

/** The most any one remembered turn may be, so a long answer cannot be replayed at full price for ever. */
const MaxTurnCharacters = 4000;

/** One thing that was said, by one side. */
export type ChatTurn = {
  readonly role: 'user' | 'assistant';
  readonly text: string;
};

/** A question, and what was already said in this conversation. */
export type ChatRequest = {
  readonly question: string;
  readonly history: readonly ChatTurn[];
};

/** What the copilot answered, and what asking cost. */
export type ChatAnswer = {
  readonly answer: string;
  readonly usage: TokenUsage;
  /** How large the brief was, so the view can show why a turn costs what it does. */
  readonly briefCharacters: number;
};

/** Something that can put a question to a model. The real one calls the API; tests pass a double. */
export type ChatAsker = (system: string, messages: readonly ChatTurn[]) => Promise<{
  readonly text: string;
  readonly usage: TokenUsage;
}>;

/** A request that was understood, or the reason it was not. */
export type ChatRequestResult =
  | { readonly ok: true; readonly request: ChatRequest }
  | { readonly ok: false; readonly reason: string };

/**
 * What the copilot is, and the four things it may not do.
 *
 * @remarks
 * Every rule here exists because breaking it would make this dangerous rather than merely wrong.
 *
 * The first is the one the others rest on: the brief is the whole world. A copilot that answers from
 * what it remembers about PLCs in general would produce a confident number about *this* cell that
 * nothing recorded, on the page somebody opens to find out what the cell actually did.
 *
 * The safety rule is not modesty. This project's own knowledge layer is being built on the rule that
 * the system cites and never composes, precisely so that no sentence about a machine that can move
 * is written by a model. Until that layer exists there is nothing to cite, so the only correct
 * answer to a safety question here is to send the person to the manual and to their supervisor.
 *
 * The rule about changing things is what keeps the dashboard's one guarantee true. This endpoint has
 * no tool, no MCP client and no path to the server, so the copilot could not act if it tried - but
 * an answer that *implies* it did would still send somebody to a machine believing something had
 * been configured.
 */
const SystemPrompt = [
  'You are the copilot of a dashboard for a TIA Portal test harness. It drives a four-station',
  'training cell through a generate-compile-test loop, on a simulated PLC.',
  '',
  'Answer only from the brief below. It is assembled from the harness store for every question and',
  'it is everything you know.',
  '',
  'Rules, in order of importance:',
  '',
  '1. Never state a number, a name or an outcome that is not in the brief. If the brief does not',
  '   answer the question, say so and stop. "I do not have that" is a complete answer here, and a',
  '   plausible number is the worst thing you can produce, because it is indistinguishable from a',
  '   measured one. Only point at a tab - Runs, Metrics, Audit trail, Workshop gate, Live run - when',
  '   the brief itself holds that kind of fact and you are pointing at where the detail is. This',
  '   dashboard holds runs of the loop and nothing else: it has no wiring, no IP addresses, no',
  '   hardware inventory and no drawings, so guessing which tab would have one of those sends',
  '   somebody looking for a page that does not exist.',
  '2. Never give safety guidance, and never say that anything is safe. That includes wiring,',
  '   emergency stops, guards, whether a program may be downloaded to a physical PLC, and whether a',
  '   cell may be started. Say that safety questions go to the machine documentation and to the',
  '   supervisor who is physically present, and stop there. This holds even when you are asked',
  '   directly, even when the answer seems obvious, and even when the user says they know already.',
  '3. You cannot change anything. You have no tools and no connection to the server: you read what',
  '   was recorded, and that is all. Never say or imply that you have started, stopped, downloaded,',
  '   compiled, written or configured anything, and never offer to.',
  '4. Every rate comes with the sample size beside it in the brief. Quote the sample size whenever',
  '   you quote the rate. A number without its sample size is not a measurement in this project.',
  '',
  'Be brief and plain. Answer in the language the question was asked in.'
].join('\n');

/**
 * Reads a request body into a question, or says why it is not one.
 *
 * @param body Whatever arrived as JSON.
 * @returns The request, or the reason it was refused.
 * @remarks
 * A refusal is a result and not an exception: a mistyped request is this working, and the caller
 * turns it into a 400 with the reason in it. The limits are enforced rather than trimmed, because
 * silently sending a truncated question would bill for an answer to something nobody asked.
 */
export function readChatRequest(body: unknown): ChatRequestResult {
  if (typeof body !== 'object' || body === null) {
    return { ok: false, reason: 'The request body must be a JSON object with a question in it.' };
  }

  const fields = body as Record<string, unknown>;
  const question = fields['question'];

  if (typeof question !== 'string' || question.trim().length === 0) {
    return { ok: false, reason: 'A question is required, as a non-empty string.' };
  }

  if (question.length > MaxQuestionCharacters) {
    return {
      ok: false,
      reason: `A question may be at most ${MaxQuestionCharacters} characters; this one is ${question.length}.`
    };
  }

  const history = readHistory(fields['history']);

  if (!history.ok) {
    return history;
  }

  return { ok: true, request: { question: question.trim(), history: history.turns } };
}

/** The remembered turns, or the reason they were refused. */
function readHistory(
  value: unknown
): { readonly ok: true; readonly turns: readonly ChatTurn[] } | { readonly ok: false; readonly reason: string } {
  if (value === undefined) {
    return { ok: true, turns: [] };
  }

  if (!Array.isArray(value)) {
    return { ok: false, reason: 'History must be an array of turns.' };
  }

  if (value.length > MaxHistoryTurns) {
    return {
      ok: false,
      reason: `A conversation may carry at most ${MaxHistoryTurns} turns; this one carries ${value.length}.`
    };
  }

  const turns: ChatTurn[] = [];

  for (const entry of value) {
    const turn = readTurn(entry);

    if (turn === undefined) {
      return {
        ok: false,
        reason:
          `Each turn must be {role: 'user' | 'assistant', text: string} of at most ` +
          `${MaxTurnCharacters} characters.`
      };
    }

    turns.push(turn);
  }

  return { ok: true, turns };
}

function readTurn(entry: unknown): ChatTurn | undefined {
  if (typeof entry !== 'object' || entry === null) {
    return undefined;
  }

  const fields = entry as Record<string, unknown>;
  const role = fields['role'];
  const text = fields['text'];

  // Exactly the two roles, and nothing else gets through. An unrecognised role is not passed on to
  // be interpreted by whatever is downstream: the absence of a decision is a refusal here too.
  if (role !== 'user' && role !== 'assistant') {
    return undefined;
  }

  if (typeof text !== 'string' || text.length === 0 || text.length > MaxTurnCharacters) {
    return undefined;
  }

  return { role, text };
}

/**
 * Puts one question to the model, with the brief in front of it.
 *
 * @param request The question and the conversation so far.
 * @param brief What was recorded, as text.
 * @param ask How the question reaches a model.
 * @returns The answer and what it cost.
 * @remarks
 * The brief goes in the system prompt rather than in the conversation, so it cannot be argued with
 * by a later turn: a question that says "actually there were fifty runs" is a message, and messages
 * do not get to redefine what was measured.
 */
export async function answerQuestion(
  request: ChatRequest,
  brief: CopilotBrief,
  ask: ChatAsker
): Promise<ChatAnswer> {
  const system = `${SystemPrompt}\n\n# The brief\n\n${brief.text}`;
  const messages = [...request.history, { role: 'user' as const, text: request.question }];
  const answer = await ask(system, messages);

  return { answer: answer.text, usage: answer.usage, briefCharacters: brief.characters };
}

/** How many tokens an answer may run to. Exported so the sender and the tests agree on one number. */
export function maxAnswerTokens(): number {
  return MaxAnswerTokens;
}
