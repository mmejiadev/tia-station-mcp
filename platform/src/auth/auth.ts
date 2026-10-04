import { betterAuth } from 'better-auth';
import { drizzleAdapter } from 'better-auth/adapters/drizzle';
import { organization } from 'better-auth/plugins';
import type { PlatformDatabase } from '../db/connection.ts';
import * as schema from '../db/schema.ts';
import type { AuthSettings } from './authSettings.ts';
import { accessControl, CreatorRole, roles } from './permissions.ts';

/**
 * Builds the sign-in service.
 *
 * @param database Where users, sessions and organisations are kept.
 * @param settings The secret, the address and the two providers.
 * @returns The Better Auth instance; its handler serves `/api/auth/*`.
 * @remarks
 * Sign-in only through Google and GitHub: no passwords of our own to store, leak or reset. E-mail
 * and password can be added later without a migration, should a company need it.
 *
 * **Telemetry is off.** Better Auth can report usage to its authors; a tool a company runs does not
 * send anything to a third party that nobody decided to send.
 */
export function createAuth(database: PlatformDatabase, settings: AuthSettings) {
  return betterAuth({
    secret: settings.secret,
    baseURL: settings.baseUrl,
    trustedOrigins: [settings.baseUrl],
    telemetry: { enabled: false },
    database: drizzleAdapter(database, { provider: 'pg', schema }),
    socialProviders: {
      google: { clientId: settings.google.clientId, clientSecret: settings.google.clientSecret },
      github: { clientId: settings.github.clientId, clientSecret: settings.github.clientSecret }
    },
    plugins: [
      organization({
        ac: accessControl,
        roles,
        creatorRole: CreatorRole
      })
    ]
  });
}

/** The sign-in service. */
export type PlatformAuth = ReturnType<typeof createAuth>;
