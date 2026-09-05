/**
 * Repairs the control characters PDF extraction leaves inside a page.
 *
 * @remarks
 * Two of the three manuals in the corpus come out of `pdfjs` with C0 control characters in their
 * text - 1626 of them across 165 pages, all in the SICK C4000 and the Festo DSBC. They are a broken
 * glyph-to-character map in those documents' own fonts rather than a bug in the extractor, and they
 * land in the worst possible place: **inside technical references.**
 *
 * ```
 * EN ISO 13855   is stored as   EN ISO 13\u0001855
 * IEC 61496-1    is stored as   IEC 61\u0001496D1
 * VDMA 24562     is stored as   VDMA\u000224\u0002562
 * ```
 *
 * That defeats both halves of the search at once. BM25 tokenises `13\u0001855` as something no query
 * will ever contain, and the trigram vector - which exists precisely so that `6ES7214-1AG40` matches
 * `6ES7 214-1AG40-0XB0` - sees trigrams no clean query produces. The retrieval gate found this on
 * its first run, while its ground truth was being written.
 *
 * **What this does not do is guess what the character was.** The codes are not consistent: the same
 * `\u0002` stands for a space in `VDMA\u000224\u0002562` and for a hyphen in `NF\u0002E\u000249\u0002003.1`.
 * Putting the hyphen back would be authoring text into a corpus whose whole value is that it is
 * quoted verbatim, so the hyphen stays lost and the reference becomes findable, which is the half
 * that can be had honestly.
 */

/**
 * The C0 range, minus the three characters that are legitimate layout.
 *
 * @remarks
 * Tab, newline and carriage return are kept: the extractor uses newlines to mark where the PDF had
 * a line break, and an excerpt is more readable for it.
 */
const ControlCharacters = /[\u0000-\u0008\u000b\u000c\u000e-\u001f]+/g;

/** Runs of horizontal whitespace, which the repair leaves behind where a bullet used to be. */
const RepeatedSpaces = /[ \t]{2,}/g;

/**
 * Cleans one page of extracted text.
 *
 * @param text The page exactly as the extractor produced it.
 * @returns The page with control characters resolved and no doubled spaces.
 * @remarks
 * A control character **between two digits is removed**, because there it is splitting one number:
 * `13\u0001855` is EN ISO 13855, and it has to be a single token to be searchable at all. Anywhere
 * else it becomes a space, because there it stands in for a separator - a bullet, a symbol, the gap
 * in `VDMA 24562` - and joining the words around it would manufacture a token the document does not
 * contain.
 *
 * The rule is read off this corpus rather than derived from the PDF specification, and the tests
 * beside it are the cases it was read off. If a fourth document arrives whose corruption does not
 * fit, the failing test is the place to start.
 */
export function repairPageText(text: string): string {
  const repaired = text.replace(ControlCharacters, (match: string, offset: number) => {
    const before = text[offset - 1] ?? '';
    const after = text[offset + match.length] ?? '';

    return isDigit(before) && isDigit(after) ? '' : ' ';
  });

  return repaired.replace(RepeatedSpaces, ' ');
}

function isDigit(character: string): boolean {
  return character >= '0' && character <= '9';
}
