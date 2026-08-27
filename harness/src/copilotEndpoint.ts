import { buildBrief, type BriefSources } from './copilotBrief.ts';
import { answerQuestion, readChatRequest, type ChatAsker } from './copilotChat.ts';
import type { ApiResponse } from './dashboardApi.ts';
import { estimateCost } from './modelPricing.ts';
import type { TokenUsage } from './telemetry.ts';

/**
 * Whether there is a copilot to talk to, and why not when there is not.
 *
 * @remarks
 * The view asks this before it offers a text box, and it is written to be *said* rather than
 * inferred. A chat that looks ready and fails on the first question teaches somebody that the whole
 * dashboard is flaky; one that says "there is no API key, so there is nothing to ask" is telling
 * them the one thing they can act on. The same rule the mode banner is built on: never guess, and
 * never let a missing answer look like a working one.
 */
export type CopilotStatus = {
  readonly available: boolean;
  /** Which model answers, so nobody has to wonder what a turn is costing. */
  readonly model: string;
  /** Present only when unavailable, and it names what to do about it. */
  readonly reason?: string;
};

/** What one answered question comes back as. */
export type ChatResponse = {
  readonly answer: string;
  readonly model: string;
  readonly usage: TokenUsage;
  /**
   * What this turn cost, at the list prices in `modelPricing.ts`.
   *
   * @remarks
   * Undefined when the model is not in that table, never zero - the same rule the run report
   * follows. A cost of "$0.00" is a number somebody would add up and believe.
   */
  readonly costDollars: number | undefined;
  /** How large the brief was. Most of the input tokens are it, and this is why a turn is not free. */
  readonly briefCharacters: number;
};

/** The copilot, as the server uses it. */
export type Copilot = {
  /** Whether a question can be asked at all. */
  status(): CopilotStatus;
  /** Answers one question, or refuses it with a reason. */
  ask(body: unknown): Promise<ApiResponse>;
};

/**
 * Builds the copilot, or a copilot that can only explain why it cannot answer.
 *
 * @param sources Where the brief is read from.
 * @param model Which model to ask.
 * @param makeAsker How to build the thing that calls the API. Injected so tests need no key.
 * @returns A copilot that is either available or honest about not being.
 * @remarks
 * A missing key is not an error here, it is a state. The API server has five other endpoints that
 * work perfectly without one, and refusing to start because a sixth cannot would take the dashboard
 * down over the feature that was cut for four months.
 */
export function createCopilot(
  sources: BriefSources,
  model: string,
  makeAsker: () => ChatAsker
): Copilot {
  const asker = buildAsker(makeAsker);

  return {
    status(): CopilotStatus {
      if (asker.ok) {
        return { available: true, model };
      }

      return { available: false, model, reason: asker.reason };
    },

    async ask(body: unknown): Promise<ApiResponse> {
      if (!asker.ok) {
        // 503 and not 500: nothing is broken, the machine is not set up for this. The distinction is
        // what tells somebody to add a key rather than to go looking for a bug.
        return { status: 503, body: { error: asker.reason } };
      }

      const request = readChatRequest(body);

      if (!request.ok) {
        return { status: 400, body: { error: request.reason } };
      }

      const answer = await answerQuestion(request.request, buildBrief(sources), asker.ask);
      const response: ChatResponse = {
        answer: answer.answer,
        model: answer.usage.model,
        usage: answer.usage,
        costDollars: estimateCost(answer.usage),
        briefCharacters: answer.briefCharacters
      };

      return { status: 200, body: response };
    }
  };
}

/** The asker, or the reason there is not one. */
function buildAsker(
  makeAsker: () => ChatAsker
): { readonly ok: true; readonly ask: ChatAsker } | { readonly ok: false; readonly reason: string } {
  try {
    return { ok: true, ask: makeAsker() };
  } catch (error) {
    // The only expected failure is a missing key, and its message already says what to do. Anything
    // else is reported as itself rather than translated into a guess about what went wrong.
    return { ok: false, reason: error instanceof Error ? error.message : String(error) };
  }
}
