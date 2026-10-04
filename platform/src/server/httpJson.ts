import type { IncomingMessage, ServerResponse } from 'node:http';

// A profile with a full biography is a few kilobytes; anything far larger is not a request this
// server serves, and reading it whole would let one request fill the memory.
const MaximumBodyBytes = 64 * 1024;

/**
 * Reads a request's body as JSON.
 *
 * @param request The request.
 * @returns The parsed body; an empty object for an empty one.
 * @throws When the body is larger than the limit or is not JSON.
 */
export async function readJsonBody(request: IncomingMessage): Promise<unknown> {
  const chunks: Buffer[] = [];
  let size = 0;

  for await (const chunk of request) {
    size += (chunk as Buffer).length;

    if (size > MaximumBodyBytes) {
      throw new Error(`The request body is larger than ${MaximumBodyBytes} bytes.`);
    }

    chunks.push(chunk as Buffer);
  }

  const text = Buffer.concat(chunks).toString('utf8');

  return text.trim().length === 0 ? {} : JSON.parse(text);
}

/**
 * Answers with JSON.
 *
 * @param response The response.
 * @param status The HTTP status.
 * @param body What to send.
 */
export function sendJson(response: ServerResponse, status: number, body: unknown): void {
  response.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' });
  response.end(JSON.stringify(body));
}
