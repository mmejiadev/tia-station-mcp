// Better Auth keys an organisation by its slug, so two with the same name still need two slugs.
const SuffixLength = 6;

// Long enough to recognise the name in a URL, short enough to stay one.
const MaximumStemLength = 40;

/**
 * The slug a new organisation is created with.
 *
 * @param name The name the person typed.
 * @param suffix Random characters that keep it unique; passed in so the rule can be tested.
 * @returns Lower case letters, digits and hyphens: `grau-superior-2n-k3x9qa`.
 * @remarks
 * Accents are dropped rather than the letters carrying them, so "Sistemes programables avançats"
 * reads as itself. A name with no letters or digits at all still gets a slug: the suffix alone.
 */
export function organizationSlug(name: string, suffix: string): string {
  const stem = name
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, MaximumStemLength)
    .replace(/-+$/, '');

  return stem.length === 0 ? suffix : `${stem}-${suffix}`;
}

/**
 * Random characters for {@link organizationSlug}.
 *
 * @returns Six lower-case letters and digits.
 */
export function randomSlugSuffix(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(SuffixLength));

  return Array.from(bytes, (byte) => (byte % 36).toString(36)).join('');
}
