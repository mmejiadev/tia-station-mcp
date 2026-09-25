/**
 * `npm run grafcet` — read a chart from text notation or from exported LAD, check it, and draw it.
 *
 * @remarks
 * Input is one of:
 * - `--text <file>`: a chart transcribed in the text notation (see grafcetText.ts);
 * - `--s7dcl <folder>`: the `.s7dcl` documents of a PLC program exported from TIA Portal V20, from
 *   which the chart the program really runs is recovered.
 *
 * `--tags <file>` adds the tag table (SimaticML or `NAME %ADDR` lines) so bit ranges can be checked.
 * `--html <file>` writes the drawing with the findings; `--svg <file>` writes the drawing alone;
 * `--lad <folder>` writes set/reset LAD for the chart as `.s7dcl` documents.
 *
 * Exit code 0 when nothing is wrong, 2 when there are findings, 1 when the input cannot be read.
 */

import { mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { parseFlags } from '../options.ts';
import { checkBitfieldRanges } from './bitfieldCheck.ts';
import { checkGrafcet } from './grafcetCheck.ts';
import { renderGrafcetPage, type PageFinding } from './grafcetPage.ts';
import { renderGrafcetSvg } from './grafcetSvg.ts';
import { formatGrafcet, parseGrafcet } from './grafcetText.ts';
import { grafcetToLad } from './grafcetToLad.ts';
import type { Grafcet } from './grafcetModel.ts';
import { ladToGrafcet } from './ladToGrafcet.ts';
import { readS7dcl } from './s7dclReader.ts';
import { readTagTable } from './tagTable.ts';

const Usage = 'Usage: npm run grafcet -- (--text <file> | --s7dcl <folder>) [--tags <file>] [--title <name>] [--html <file>] [--svg <file>] [--lad <folder>]. Every flag takes a value.';

interface Loaded {
  readonly grafcet: Grafcet;
  readonly findings: readonly PageFinding[];
  readonly source: string;
}

function main(): number {
  const flags = parseFlags(process.argv.slice(2), Usage);
  const loaded = load(flags);

  if (loaded === null) {
    return 1;
  }

  const findings = [...loaded.findings, ...checkGrafcet(loaded.grafcet).map((finding) => ({ severity: finding.severity, where: `${finding.sequence} [${finding.rule}]`, message: finding.message }))];
  try {
    writeOutputs(flags, loaded.grafcet, findings, loaded.source);
  } catch (error) {
    // The generator refuses a chart it must not turn into code; that refusal is the answer, not a crash.
    console.error(`Stopped: ${(error as Error).message}`);
    return 1;
  }

  console.log(formatGrafcet(loaded.grafcet));
  findings.forEach((finding) => console.log(`${finding.severity.toUpperCase()} ${finding.where}: ${finding.message}`));
  console.log(findings.length === 0 ? 'No findings.' : `${findings.length} finding(s).`);
  return findings.length === 0 ? 0 : 2;
}

function load(flags: ReadonlyMap<string, string>): Loaded | null {
  const text = flags.get('--text');
  const folder = flags.get('--s7dcl');

  if ((text === undefined) === (folder === undefined)) {
    console.error(`Give exactly one of --text and --s7dcl. ${Usage}`);
    return null;
  }

  return text !== undefined ? loadText(text) : loadLad(folder!, flags);
}

function loadText(file: string): Loaded | null {
  const parsed = parseGrafcet(readFileSync(file, 'utf8'));

  if (!parsed.ok) {
    parsed.errors.forEach((error) => console.error(`${file}: ${error}`));
    return null;
  }

  return { grafcet: parsed.grafcet, findings: [], source: basename(file) };
}

function loadLad(folder: string, flags: ReadonlyMap<string, string>): Loaded {
  const blocks = readdirSync(folder).filter((name) => name.endsWith('.s7dcl')).sort().map((name) => readS7dcl(readFileSync(join(folder, name), 'utf8')));
  const chart = ladToGrafcet(blocks, flags.get('--title') ?? basename(folder));
  const tagsFile = flags.get('--tags');
  const ranges = tagsFile === undefined ? [] : checkBitfieldRanges(blocks.flatMap((block) => block.operations), readTagTable(readFileSync(tagsFile, 'utf8')), chart.grafcet);
  const findings = [...chart.findings, ...ranges].map((finding) => ({ severity: finding.severity, where: `${finding.block} network ${finding.network}`, message: finding.message }));
  return { grafcet: chart.grafcet, findings, source: `${blocks.length} LAD block(s) in ${basename(folder)}` };
}

function writeOutputs(flags: ReadonlyMap<string, string>, grafcet: Grafcet, findings: readonly PageFinding[], source: string): void {
  const html = flags.get('--html');
  const svg = flags.get('--svg');
  const lad = flags.get('--lad');

  if (html !== undefined) {
    writeFileSync(html, renderGrafcetPage(grafcet, findings, source));
  }

  if (svg !== undefined) {
    writeFileSync(svg, renderGrafcetSvg(grafcet));
  }

  if (lad !== undefined) {
    mkdirSync(lad, { recursive: true });
    grafcetToLad(grafcet).forEach((document) => writeFileSync(join(lad, `${document.name}.s7dcl`), document.text));
  }
}

process.exitCode = main();
