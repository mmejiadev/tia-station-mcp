/**
 * Checks that every edge memory bit belongs to exactly one edge contact and to nothing else.
 *
 * @remarks
 * A P or N contact keeps the operand's previous state in its memory bit and rewrites it each time it
 * is executed. Two contacts on one bit see a single edge between them: the first updates the bit and
 * the second finds no change. A coil on the bit corrupts it the same way. Both compile without a
 * word; the first is what a class exercise did on 2026-10-08, three P contacts on `MARXA_FP1`.
 *
 * The check runs over every block at once, since a memory bit is global. A write on the first scan is
 * the generator's initialisation of the bit to its operand's state, and is not a conflict.
 */

import type { Condition } from './grafcetModel.ts';
import type { EdgeContact, LadBlock, LadFinding, LadOperation } from './s7dclReader.ts';

/** Every edge memory bit shared by two contacts, or written by anything but the first scan. */
export function checkEdgeBits(blocks: readonly LadBlock[], isFirstScan: (condition: Condition) => boolean): readonly LadFinding[] {
  const contacts = blocks.flatMap((block) => block.edgeContacts);
  return [...sharedBits(contacts), ...writtenBits(blocks, contacts, isFirstScan)];
}

function sharedBits(contacts: readonly EdgeContact[]): readonly LadFinding[] {
  return contacts.flatMap((contact, index) => {
    const first = contacts.findIndex((other) => other.bit === contact.bit);

    if (first === index) {
      return [];
    }

    const owner = contacts[first]!;
    return [{ ...contact, severity: 'error' as const, message: `edge memory bit ${contact.bit} is already used by an edge contact in ${owner.block} network ${owner.network}: contacts sharing a bit see a single edge between them, so give each its own bit` }];
  });
}

function writtenBits(blocks: readonly LadBlock[], contacts: readonly EdgeContact[], isFirstScan: (condition: Condition) => boolean): readonly LadFinding[] {
  const bits = new Set(contacts.map((contact) => contact.bit));
  return blocks
    .flatMap((block) => block.operations)
    .filter((operation): operation is Write => 'operand' in operation)
    .filter((operation) => bits.has(operation.operand) && !isFirstScan(operation.condition))
    .map((operation) => ({ block: operation.block, network: operation.network, severity: 'error' as const, message: `${operation.operand} is an edge memory bit and is also written here, which corrupts the edge it keeps` }));
}

type Write = LadOperation & { readonly operand: string };
