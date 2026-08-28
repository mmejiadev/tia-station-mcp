import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { getDocument } from 'pdfjs-dist/legacy/build/pdf.mjs';

/**
 * Extracts the text of a PDF, one string per page.
 *
 * @remarks
 * Page by page rather than as one document, because a citation names a page and the only way to
 * name it honestly is never to have merged them. The index is the PDF page number, which is what a
 * reader can jump to; the number printed in the page furniture often differs and is not extracted.
 *
 * No external font data is fetched and nothing outside the file is read. A manual is a document from
 * outside this machine, and the brief is explicit that retrieved content is data and never
 * instructions — that starts at the parser, before anything is indexed.
 */

/**
 * Where pdfjs finds the fourteen standard PDF fonts, which it needs to map glyphs to characters.
 *
 * @remarks
 * Resolved from the installed package rather than written down, because a hardcoded path is banned
 * here and would be wrong on the next machine anyway. Without it a document that relies on those
 * fonts warns and extracts a page of nothing, which is a silent hole in a citation index: the page
 * is there, it is empty, and nothing says why.
 */
const StandardFontDirectory =
  `${join(dirname(createRequire(import.meta.url).resolve('pdfjs-dist/package.json')), 'standard_fonts').replaceAll('\\', '/')}/`;

/** Puts a line break where the PDF had one, and nothing anywhere else. */
function joinItems(items: readonly { str?: string; hasEOL?: boolean }[]): string {
  let text = '';

  for (const item of items) {
    text += item.str ?? '';

    if (item.hasEOL === true) {
      text += '\n';
    }
  }

  return text;
}

/**
 * Reads every page of a PDF as text.
 *
 * @param path A local file. Stage 1 ingests documents supplied by hand; fetching is stage 4.
 * @returns One string per page, in order. A page with no extractable text yields an empty string
 * rather than being skipped, so page numbers stay aligned with the document.
 */
export async function readPdfPages(path: string): Promise<string[]> {
  const file = await readFile(path);
  const document = await getDocument({
    data: new Uint8Array(file),
    useSystemFonts: false,
    standardFontDataUrl: StandardFontDirectory,
  }).promise;

  try {
    const pages: string[] = [];

    for (let pageNumber = 1; pageNumber <= document.numPages; pageNumber += 1) {
      const page = await document.getPage(pageNumber);
      const content = await page.getTextContent();
      pages.push(joinItems(content.items as { str?: string; hasEOL?: boolean }[]));
      page.cleanup();
    }

    return pages;
  }
  finally {
    await document.destroy();
  }
}
