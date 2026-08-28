import { CitationFooter, NotFoundAnswer, type LookupResult } from './citation.ts';

/**
 * Renders a lookup for a person to read.
 *
 * @remarks
 * One renderer, used by the CLI and by anything that shows a lookup later — the MCP prompts, the
 * dashboard — because the brief is explicit that a checklist or a citation which exists twice will
 * diverge, and the divergent copy is the one that gets shown.
 *
 * What it renders is what the store holds. There is no summarising step here and no place to add
 * one: the excerpt goes to the screen as it came out of the document, and the footer that says so
 * is appended by this function rather than by its callers, so no caller can leave it off.
 */
export function renderLookup(result: LookupResult): string {
  if (result.outcome === 'not-found') {
    return NotFoundAnswer;
  }

  const blocks = result.citations.map((citation, index) => [
    `[${index + 1}] ${citation.device} — ${citation.title}`,
    `    ${citation.version}, page ${citation.page}`,
    '',
    quote(citation.excerpt),
  ].join('\n'));

  return [...blocks, CitationFooter].join('\n\n');
}

/** Indents the excerpt so its edges are visible, without altering a character of it. */
function quote(excerpt: string): string {
  return excerpt
    .split('\n')
    .map((line) => `    | ${line}`)
    .join('\n');
}
