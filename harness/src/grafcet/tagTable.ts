/**
 * Reads PLC tag names and addresses, from a SimaticML tag table export or from a plain list.
 *
 * @remarks
 * Only what the checks need: a name and a bit address. The SimaticML export that
 * `ExportSourceSnapshot` writes holds each tag as an element with `<Name>` and `<LogicalAddress>`;
 * a plain list is one `NAME %M10.1` per line, which is what a person types in a hurry.
 */

/** A tag with a bit address, e.g. `X31` at `%M10.1`. Byte and word tags are skipped. */
export interface BitTag {
  readonly name: string;
  readonly area: string;
  readonly bit: number;
}

const BitAddressPattern = /^%([IQM])(\d+)\.([0-7])$/;
const XmlTagPattern = /<SW\.Tags\.PlcTag\b[\s\S]*?<\/SW\.Tags\.PlcTag>/g;

/** Parses `%M10.1` into an area and an absolute bit index; null for anything that is not a bit. */
export function parseBitAddress(address: string): { area: string; bit: number } | null {
  const match = BitAddressPattern.exec(address.trim());
  return match === null ? null : { area: match[1]!, bit: Number(match[2]) * 8 + Number(match[3]) };
}

/** Writes an absolute bit index back as an address, e.g. (M, 81) → `%M10.1`. */
export function formatBitAddress(area: string, bit: number): string {
  return `%${area}${Math.floor(bit / 8)}.${bit % 8}`;
}

/** Reads a tag table, deciding from the content whether it is SimaticML or a plain list. */
export function readTagTable(text: string): readonly BitTag[] {
  const entries = text.includes('<SW.Tags.PlcTag') ? readXml(text) : readList(text);
  return entries.flatMap(([name, address]) => {
    const parsed = parseBitAddress(address);
    return parsed === null ? [] : [{ name, ...parsed }];
  });
}

function readXml(text: string): readonly [string, string][] {
  return [...text.matchAll(XmlTagPattern)].flatMap((match) => {
    const name = /<Name>([^<]*)<\/Name>/.exec(match[0])?.[1];
    const address = /<LogicalAddress>([^<]*)<\/LogicalAddress>/.exec(match[0])?.[1];
    return name === undefined || address === undefined ? [] : [[name, address] as [string, string]];
  });
}

function readList(text: string): readonly [string, string][] {
  return text.split(/\r?\n/).flatMap((line) => {
    const match = /^\s*(\S+)\s+(%\S+)/.exec(line);
    return match === null ? [] : [[match[1]!, match[2]!] as [string, string]];
  });
}
