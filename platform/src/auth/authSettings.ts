/** What sign-in needs from the environment. */
export type AuthSettings = {
  /** Signs sessions and cookies. Long and random; never in Git. */
  readonly secret: string;
  /** Where the web is reached, as the browser sees it: the address the sign-in providers return to. */
  readonly baseUrl: string;
  readonly google: { readonly clientId: string; readonly clientSecret: string };
  readonly github: { readonly clientId: string; readonly clientSecret: string };
};

const Required = [
  'BETTER_AUTH_SECRET',
  'BETTER_AUTH_URL',
  'GOOGLE_CLIENT_ID',
  'GOOGLE_CLIENT_SECRET',
  'GITHUB_CLIENT_ID',
  'GITHUB_CLIENT_SECRET'
] as const;

// Better Auth refuses shorter secrets in production; refusing them everywhere means a development
// .env cannot carry a weak one into a deployment unnoticed.
const MinimumSecretLength = 32;

/**
 * Reads the sign-in settings from the environment.
 *
 * @param environment Usually `process.env`.
 * @returns The settings.
 * @remarks
 * Every missing variable is named at once, rather than the first one and then the next on the
 * following start: setting up sign-in means visiting two providers, and a list is what tells
 * somebody which visits are left. The values themselves are never printed.
 */
export function readAuthSettings(environment: Readonly<Record<string, string | undefined>>): AuthSettings {
  const missing = Required.filter((name) => (environment[name] ?? '').trim().length === 0);

  if (missing.length > 0) {
    throw new Error(`Sign-in is not configured. Set these in .env (see .env.example): ${missing.join(', ')}.`);
  }

  const value = (name: (typeof Required)[number]): string => (environment[name] ?? '').trim();

  if (value('BETTER_AUTH_SECRET').length < MinimumSecretLength) {
    throw new Error(`BETTER_AUTH_SECRET must be at least ${MinimumSecretLength} characters long.`);
  }

  return {
    secret: value('BETTER_AUTH_SECRET'),
    baseUrl: value('BETTER_AUTH_URL'),
    google: { clientId: value('GOOGLE_CLIENT_ID'), clientSecret: value('GOOGLE_CLIENT_SECRET') },
    github: { clientId: value('GITHUB_CLIENT_ID'), clientSecret: value('GITHUB_CLIENT_SECRET') }
  };
}
