import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { checkBitfieldRanges } from '../../src/grafcet/bitfieldCheck.ts';
import { ladToGrafcet } from '../../src/grafcet/ladToGrafcet.ts';
import { readS7dcl } from '../../src/grafcet/s7dclReader.ts';
import { formatBitAddress, parseBitAddress, readTagTable } from '../../src/grafcet/tagTable.ts';
import { ConsecutiveTags, Feeder, Init, InterleavedTags, Outfeed, Transfer } from './ladFixtures.ts';

function findings(tags: string): string[] {
  const blocks = [Feeder, Transfer, Outfeed, Init].map(readS7dcl);
  const { grafcet } = ladToGrafcet(blocks, 'test');

  return checkBitfieldRanges(blocks.flatMap((block) => block.operations), readTagTable(tags), grafcet).map((finding) => finding.message);
}

describe('R_BF ranges in the initialisation', () => {
  it('accepts ranges over consecutive step addresses', () => {
    assert.deepEqual(findings(ConsecutiveTags), []);
  });

  it('reports the steps of another sequence a range clears', () => {
    // The bug that compiled cleanly in a class project: the range ran through another conveyor.
    const messages = findings(InterleavedTags);

    assert.ok(messages.some((message) => message.includes('also holds X50, X51')), messages.join(' | '));
  });

  it('reports the steps of its own sequence a range misses', () => {
    const messages = findings(InterleavedTags);

    assert.ok(messages.some((message) => message.includes('does not reach X33, X34')), messages.join(' | '));
  });
});

describe('tag table', () => {
  it('reads a SimaticML export', () => {
    const xml = '<SW.Tags.PlcTag ID="1"><AttributeList><DataTypeName>Bool</DataTypeName><LogicalAddress>%M10.1</LogicalAddress><Name>X31</Name></AttributeList></SW.Tags.PlcTag>';

    assert.deepEqual(readTagTable(xml), [{ name: 'X31', area: 'M', bit: 81 }]);
  });

  it('skips tags that are not single bits', () => {
    assert.deepEqual(readTagTable('Speed %MW20\nX1 %M0.1\n'), [{ name: 'X1', area: 'M', bit: 1 }]);
  });

  it('writes a bit index back as the address it came from', () => {
    const parsed = parseBitAddress('%M11.7')!;

    assert.equal(formatBitAddress(parsed.area, parsed.bit), '%M11.7');
  });
});
