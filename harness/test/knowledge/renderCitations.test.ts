import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { CitationFooter, NotFoundAnswer, type LookupResult } from '../../src/knowledge/citation.ts';
import { renderLookup } from '../../src/knowledge/renderCitations.ts';

/**
 * What reaches a screen.
 *
 * The rule being held here is the one the brief calls unskippable: every surface that shows an
 * excerpt shows the footer, and a lookup that found nothing shows the sentence and nothing else.
 * Both are asserted on the renderer rather than on its callers, because a caller can forget.
 */
describe('renderLookup', () => {
  it('renders a not-found as exactly the sentence, with no prose around it', () => {
    const result: LookupResult = { outcome: 'not-found', query: 'anything', citations: [] };

    assert.equal(renderLookup(result), NotFoundAnswer);
  });

  it('appends the footer to every rendered citation', () => {
    assert.ok(renderLookup(oneCitation()).endsWith(CitationFooter));
  });

  it('shows the excerpt unaltered, and names its document, version and page', () => {
    const rendered = renderLookup(oneCitation());

    assert.ok(rendered.includes('the acknowledgement signal is active low'));
    assert.ok(rendered.includes('SW 5.16'));
    assert.ok(rendered.includes('page 43'));
  });
});

function oneCitation(): LookupResult {
  return {
    outcome: 'cited',
    query: 'acknowledgement signal',
    citations: [
      {
        device: 'UR5e',
        title: 'UR5e user manual',
        version: 'SW 5.16',
        page: 43,
        excerpt: 'the acknowledgement signal is active low',
        score: 0.5,
      },
    ],
  };
}
