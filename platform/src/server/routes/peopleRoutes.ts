import { claimIdentity, confirmIdentity } from '../../people/identityClaims.ts';
import { readProfile, updateProfile, type ProfileFields } from '../../people/profiles.ts';
import { readJsonObject, sendJson } from '../httpJson.ts';
import { optionalTextField, textField } from '../requestFields.ts';
import { wholeNumber, type Route } from '../router.ts';

const ProfileFieldNames = ['jobTitle', 'specialty', 'company', 'bio', 'languages', 'certifications'] as const;

/** The signed-in person, their profile, and the TIA identities they claim or confirm. */
export const peopleRoutes: readonly Route[] = [
  {
    method: 'GET',
    pattern: /^\/api\/platform\/me$/,
    handle: async ({ database, response, person }) => {
      sendJson(response, 200, { user: person, profile: await readProfile(database, person.userId) });
    }
  },
  {
    method: 'PATCH',
    pattern: /^\/api\/platform\/me\/profile$/,
    handle: async ({ database, request, response, person }) => {
      const result = await updateProfile(database, person.userId, profileFieldsOf(await readJsonObject(request)));

      sendJson(response, result.kind === 'updated' ? 200 : 400, result);
    }
  },
  {
    method: 'POST',
    pattern: /^\/api\/platform\/identities$/,
    handle: async ({ database, request, response, person }) => {
      const body = await readJsonObject(request);
      const result = await claimIdentity(database, {
        userId: person.userId,
        organizationId: textField(body, 'organizationId'),
        stationName: textField(body, 'stationName'),
        tiaAuthor: textField(body, 'tiaAuthor')
      });

      sendJson(response, result.kind === 'claimed' ? 201 : 400, result);
    }
  },
  {
    method: 'POST',
    pattern: /^\/api\/platform\/identities\/(?<id>\d+)\/confirm$/,
    handle: async ({ database, response, person, params }) => {
      const result = await confirmIdentity(database, { confirmerId: person.userId, identityId: wholeNumber(params['id']) ?? -1 });

      sendJson(response, result.kind === 'confirmed' ? 200 : 403, result);
    }
  }
];

/**
 * The profile fields a body sends: each text, null to clear it, or absent to leave it.
 *
 * @remarks Read field by field rather than cast, so `{"jobTitle": 5}` is a 400 rather than a crash.
 */
function profileFieldsOf(body: Readonly<Record<string, unknown>>): ProfileFields {
  const fields: Record<string, string | null> = {};

  for (const name of ProfileFieldNames) {
    const value = optionalTextField(body, name);

    if (value !== undefined) {
      fields[name] = value;
    }
  }

  return fields;
}
