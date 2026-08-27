import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { DefaultModel } from '../src/modelGenerator.ts';
import { parseOptions } from '../src/options.ts';

/**
 * The command line is where a run is told what it is measuring, so a flag that is quietly ignored
 * produces numbers about something other than what was asked for. These are the cases that would
 * otherwise be found by reading a report and believing it.
 */
describe('command line', () => {
  it('names the default model when none was asked for, rather than leaving it unrecorded', () => {
    const options = parseOptions(['--archive', 'cell.zap20']);

    assert.equal(options.model, DefaultModel);
  });

  it('takes the model it was given', () => {
    const options = parseOptions(['--archive', 'cell.zap20', '--generator', 'model', '--model', 'claude-haiku-4-5']);

    assert.equal(options.model, 'claude-haiku-4-5');
  });

  it('refuses a model for the stub generator, which will not ask it anything', () => {
    // Ignored, the flag would look like it had selected a model while the repository's own patterns
    // produced the SCL - and the run would be filed as a measurement of that model.
    assert.throws(
      () => parseOptions(['--archive', 'cell.zap20', '--model', 'claude-haiku-4-5']),
      /the generator is the stub/
    );
  });

  it('refuses an empty model, which would otherwise reach the API as a request for nothing', () => {
    assert.throws(
      () => parseOptions(['--archive', 'cell.zap20', '--generator', 'model', '--model', '   ']),
      /not an empty string/
    );
  });
});
