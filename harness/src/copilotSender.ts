import Anthropic from '@anthropic-ai/sdk';
import { DefaultChatModel, maxAnswerTokens, type ChatAsker, type ChatTurn } from './copilotChat.ts';
import { readUsage, requireApiKey, thinkingFor } from './modelGenerator.ts';

/**
 * The asker that actually calls the API.
 *
 * @param model Which model to ask.
 * @returns An asker, or a thrown error naming the missing key.
 * @remarks
 * Deliberately built the same way as the generator's sender, and deliberately sharing its key check,
 * its thinking rule and its usage reader rather than repeating them. The 400 that the thinking
 * parameter causes on older models was found once; a second copy of that decision here is a second
 * place for it to be got wrong.
 *
 * **No tools are passed, and that is the safety property of this whole feature.** The copilot cannot
 * call the MCP server, cannot reach TIA Portal and cannot write to the store, because nothing here
 * gives it the means to. The dashboard's guarantee - that every write goes through the guard in the
 * server - is kept by construction rather than by asking the model nicely in the prompt.
 *
 * Not streamed, unlike the generator. An answer is capped at a thousand tokens and arrives in a few
 * seconds, which is well inside every timeout; streaming would buy a nicer cursor and cost a second
 * protocol between here and the browser.
 */
export function createChatAsker(model: string = DefaultChatModel): ChatAsker {
  requireApiKey();

  const client = new Anthropic();

  return async (system: string, messages: readonly ChatTurn[]) => {
    const message = await client.messages.create({
      model,
      max_tokens: maxAnswerTokens(),
      ...thinkingFor(model),
      system,
      messages: messages.map((turn) => ({ role: turn.role, content: turn.text }))
    });

    const text = message.content
      .filter((block) => block.type === 'text')
      .map((block) => block.text)
      .join('\n');

    // `message.model` and not the argument, for the same reason the generator does it: a request can
    // be served by a model other than the one named, and the cost belongs to the one that answered.
    return { text, usage: readUsage(message.model, message.usage) };
  };
}
