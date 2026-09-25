import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatCondition, parseCondition } from '../../src/grafcet/conditionText.ts';
import { isUncertain, type Condition } from '../../src/grafcet/grafcetModel.ts';

/**
 * The whiteboard notation is where a chart enters the system, so a misread operator here becomes a
 * wrong transition everywhere downstream.
 */
function parse(text: string): Condition {
  const result = parseCondition(text);
  assert.ok(result.ok, result.ok ? '' : result.error);

  return result.condition;
}

describe('transition-condition notation', () => {
  it('binds AND tighter than OR, as on paper', () => {
    const condition = parse('A.B + C');

    assert.equal(condition.kind, 'or');
    assert.equal(formatCondition(condition), 'A.B + C');
  });

  it('keeps the parentheses that change the meaning and drops the rest', () => {
    assert.equal(formatCondition(parse('A.(B + C)')), 'A.(B + C)');
    assert.equal(formatCondition(parse('(A.B) + C')), 'A.B + C');
  });

  it('reads / ! and ¬ as the same negation', () => {
    assert.equal(formatCondition(parse('/A')), formatCondition(parse('!A')));
    assert.equal(formatCondition(parse('¬A')), '/A');
  });

  it('collapses a double negation', () => {
    assert.equal(formatCondition(parse('//A')), 'A');
  });

  it('carries an uncertain reading through a round trip', () => {
    // An overbar that could not be read on a photo must stay visibly unsure, not become a guess.
    const condition = parse('PS.?/SCALE');

    assert.equal(isUncertain(condition), true);
    assert.equal(formatCondition(condition), 'PS.?/SCALE');
  });

  it('reads edges and the constant 1', () => {
    assert.equal(formatCondition(parse('↑START.1')), '↑START');
    assert.equal(formatCondition(parse('1')), '1');
  });

  it('refuses an empty condition instead of reading it as always true', () => {
    const result = parseCondition('   ');

    assert.equal(result.ok, false);
  });

  it('refuses an unbalanced parenthesis', () => {
    assert.equal(parseCondition('(A.B').ok, false);
  });

  it('refuses a question mark in front of a group', () => {
    assert.equal(parseCondition('?(A.B)').ok, false);
  });
});
