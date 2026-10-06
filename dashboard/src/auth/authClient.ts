import { organizationClient } from 'better-auth/client/plugins';
import { createAuthClient } from 'better-auth/react';

/**
 * The browser's half of sign-in.
 *
 * @remarks
 * No base address: requests go to `/api/auth` on the page's own origin, and the development server
 * forwards them to the platform (see `vite.config.ts`). One origin means the session cookie is the
 * page's own, and no cross-origin request has to be allowed anywhere.
 *
 * The organisation plugin is how an organisation is created: Better Auth's own endpoint, which makes
 * its creator the admin. Who may then do what inside it is the platform's to decide.
 */
export const authClient = createAuthClient({ plugins: [organizationClient()] });

/** The sign-in providers, in the order the buttons show them. */
export const Providers = [
  { id: 'google', label: 'Google' },
  { id: 'github', label: 'GitHub' }
] as const;
