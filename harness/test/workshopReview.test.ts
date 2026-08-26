import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { repositoryRoot } from '../src/serverLocation.ts';
import { readReviewRecord } from '../src/workshopReview.ts';

/**
 * Criterion 5 of the workshop gate is the one no measurement can answer, so what this reads is
 * somebody's word that a review happened. The tests are about not inventing one.
 */
describe('workshop review', () => {
  const directory = mkdtempSync(join(tmpdir(), 'tia-review-'));

  after(() => {
    rmSync(directory, { recursive: true, force: true });
  });

  it('reads the date and the reviewer from a file written for a person', () => {
    const path = write('review.md', ['# Workshop review', '', 'reviewed: 2026-09-01 by the supervising teacher', '', 'Notes follow.']);

    const record = readReviewRecord(path);

    assert.deepEqual(record, { date: '2026-09-01', reviewer: 'the supervising teacher' });
  });

  it('reports no review when the file is not there', () => {
    // The normal state before the review happens, and not an error: the gate reports the criterion
    // as unmet, which is the correct answer.
    assert.equal(readReviewRecord(join(directory, 'absent.md')), undefined);
  });

  it('reports no review when the file says nothing about one', () => {
    // A file full of intentions is not a review. Anything short of the line is not a record.
    const path = write('intentions.md', ['We should book the review with the teacher.']);

    assert.equal(readReviewRecord(path), undefined);
  });

  function write(name: string, lines: readonly string[]): string {
    const path = join(directory, name);

    writeFileSync(path, lines.join('\n'), 'utf8');

    return path;
  }
  it('does not accept the template that ships in docs/ as a review', () => {
    // The template documents the format, and a format example that satisfied the gate would open a
    // workshop door by being copied. The placeholder date is deliberately not a date.
    const shipped = join(repositoryRoot(), 'docs', 'workshop-review.md');

    assert.equal(readReviewRecord(shipped), undefined);
  });

});
