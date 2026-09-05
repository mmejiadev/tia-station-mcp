import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  formatDuration,
  formatMean,
  formatRate,
  formatRecordedInstant,
  formatSpan
} from '../src/format.ts';
import { hashFor, viewFromHash } from '../src/viewRoute.ts';

/**
 * The rules about how a measurement may be shown are rules, not styling.
 *
 * The roadmap states two of them — never a bare percentage, never a mean without its sample size —
 * and a rule that lives only inside a component is one that gets checked by looking at a browser.
 * These are the tests that would fail if somebody made the table tidier by dropping the counts.
 */
describe('how a measurement is printed', () => {
  it('prints a rate as a count first and a percentage second', () => {
    // 82% alone hides whether it was nine of eleven or ninety of a hundred and ten.
    assert.equal(formatRate(9, 11), '9 of 11 (82%)');
  });

  it('says nothing was attempted rather than printing 0%', () => {
    // Zero of zero is not a failure rate: it is a question nobody has asked yet, and 0% would put a
    // bar on the chart for work that was never done.
    assert.equal(formatRate(0, 0), 'none attempted');
  });

  it('prints a mean with how many values it was taken over', () => {
    assert.equal(formatMean(2, 9), '2.0 over 9');
  });

  it('says there is nothing to average rather than showing zero', () => {
    // A specification that never compiled has no iteration count. Zero would draw it as the fastest
    // one on the page.
    assert.equal(formatMean(undefined, 0), 'nothing to average');
  });

  it('picks the unit that makes a duration readable', () => {
    // The loop spans three orders of magnitude: generating is milliseconds, downloading is seconds,
    // a whole run is minutes.
    assert.equal(formatDuration(611), '611 ms');
    assert.equal(formatDuration(11_220), '11.2 s');
    assert.equal(formatDuration(121_000), '2 m 01 s');
  });

  it('shows a timestamp it cannot read exactly as it was recorded', () => {
    // "Invalid Date" in an audit table is a line whose evidence has been destroyed by the thing
    // displaying it. The unreadable text is itself the evidence.
    assert.equal(formatRecordedInstant('not a date'), 'not a date');
    assert.notEqual(formatRecordedInstant('2026-08-26T18:34:26.9264277+00:00'), '2026-08-26T18:34:26.9264277+00:00');
  });

  it('says a run did not finish instead of giving it a duration', () => {
    // A run still going and a run interrupted for ever are different facts, and a duration measured
    // up to now would hide which of the two this is.
    assert.equal(formatSpan(1000, undefined), 'did not finish');
    assert.equal(formatSpan(1000, 4000), '3.0 s');
  });
});

describe('which view the address bar asks for', () => {
  const views = ['Runs', 'Metrics', 'Workshop gate'];

  it('turns a view name into a fragment somebody can send', () => {
    assert.equal(hashFor('Workshop gate'), '#/workshop-gate');
  });

  it('opens the view a fragment names', () => {
    assert.equal(viewFromHash('#/workshop-gate', views), 'Workshop gate');
  });

  it('falls back to the first view rather than rendering nothing', () => {
    // The one place in this project where falling back is right: the fragment is something typed or
    // a stale bookmark, nothing is gated on it, and a blank page would say less than the first view.
    assert.equal(viewFromHash('', views), 'Runs');
    assert.equal(viewFromHash('#/no-such-view', views), 'Runs');
  });

  it('opens the Guide from a link somebody can send', () => {
    // The view a new installation is pointed at, so its address has to survive being pasted into a
    // message rather than only being reachable by clicking through the tabs.
    assert.equal(hashFor('Guide'), '#/guide');
    assert.equal(viewFromHash('#/guide', [...views, 'Guide']), 'Guide');
  });

  it('refuses to route when there are no views at all', () => {
    assert.throws(() => viewFromHash('#/anything', []), /no views at all/);
  });
});
