import assert from 'node:assert/strict';
import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { loadSpecification, validateSpecification } from '../src/specification.ts';
import { repositoryRoot } from '../src/serverLocation.ts';

/**
 * A specification is refused before an iteration is spent on it.
 *
 * One iteration of this loop costs a compile, a download and a run — about a minute. Finding out at
 * the verify phase that a step names no tag would waste all of it, which is why validation is up
 * front and total.
 */
describe('specification', () => {
  it('accepts every specification that ships', () => {
    // Not a unit test of the parser: a check that the real files are loadable. A specification set
    // that does not parse is the one defect that stops the whole phase, and it is invisible until
    // something reads them.
    const directory = join(repositoryRoot(), 'harness', 'specs');
    const files = readdirSync(directory).filter((name) => name.endsWith('.json'));

    assert.ok(files.length > 0, 'no specifications found');

    for (const file of files) {
      const specification = loadSpecification(join(directory, file));

      assert.ok(specification.acceptance.length > 0, `${file} checks nothing`);
      assert.ok(specification.name.length > 0, `${file} has no name`);
    }
  });

  it('refuses an action it does not know instead of skipping it', () => {
    // The governance layer's rule, applied here: anything not foreseen refuses. A step silently
    // ignored would make a case pass while checking less than it says, which is worse than a case
    // that does not run.
    assert.throws(
      () =>
        validateSpecification(
          {
            ...minimal(),
            acceptance: [{ action: 'powerCycle', tag: 'X' }]
          },
          'test'
        ),
      /is not an action/
    );
  });

  it('refuses an expect step that asserts both or neither', () => {
    // Both is a contradiction nobody meant; neither looks like an assertion and asserts nothing.
    assert.throws(
      () => validateSpecification({ ...minimal(), acceptance: [{ action: 'expect', tag: 'X' }] }, 'test'),
      /exactly one/
    );

    assert.throws(
      () =>
        validateSpecification(
          { ...minimal(), acceptance: [{ action: 'expect', tag: 'X', equals: '1', notEquals: '2' }] },
          'test'
        ),
      /exactly one/
    );
  });

  it('refuses a case with no acceptance steps', () => {
    assert.throws(() => validateSpecification({ ...minimal(), acceptance: [] }, 'test'), /no acceptance steps/);
  });

  it('refuses a wait with no timeout, because a loop that waits forever is not a measurement', () => {
    assert.throws(
      () =>
        validateSpecification(
          { ...minimal(), acceptance: [{ action: 'waitFor', tag: 'X', equals: '1' }] },
          'test'
        ),
      /timeoutMilliseconds/
    );
  });

  it('names the file and the field when something is missing', () => {
    // The error a person actually reads. "acceptance[1]" and the field name is the difference
    // between fixing a typo and hunting for it.
    assert.throws(
      () =>
        validateSpecification(
          { ...minimal(), acceptance: [{ action: 'write', tag: 'X', value: '1' }, { action: 'write', tag: 'Y' }] },
          'specs/example.json'
        ),
      /specs\/example\.json: acceptance\[1\]: 'value'/
    );
  });
  it('accepts a hold step, which is how a case asserts that nothing happened', () => {
    const specification = validateSpecification(
      {
        ...minimal(),
        acceptance: [
          { action: 'hold', tag: 'DB_Cell.CompletedPieceId', notEquals: '55', durationMilliseconds: 5000 }
        ]
      },
      'test'
    );

    assert.equal(specification.acceptance[0]?.action, 'hold');
  });

  it('refuses a hold step with no duration', () => {
    // A hold of no length asserts nothing while looking like an assertion, which is the failure
    // mode this whole validation exists to prevent.
    assert.throws(
      () =>
        validateSpecification(
          { ...minimal(), acceptance: [{ action: 'hold', tag: 'DB_Cell.X', notEquals: '1' }] },
          'test'
        ),
      /durationMilliseconds/
    );
  });

});

/** The smallest thing that is a specification, for tests that vary one part of it. */
function minimal(): Record<string, unknown> {
  return {
    name: 'example',
    goal: 'something',
    cellPath: 'spec/cells/two-station-demo.json',
    softwarePath: 'PLC_0',
    controller: {
      name: 'Example',
      address: '192.168.0.1',
      subnetMask: '255.255.255.0',
      cpuType: 'CPU1511'
    },
    acceptance: [{ action: 'write', tag: 'X', value: '1' }]
  };
}
