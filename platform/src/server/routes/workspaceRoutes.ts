import { createFolder, deleteFolder, fileProject, updateFolder } from '../../workspace/folders.ts';
import { listChanges, listCompilations, MaximumPage, readProject } from '../../workspace/projectRead.ts';
import { linkStation } from '../../workspace/stations.ts';
import { readWorkspace } from '../../workspace/workspaceRead.ts';
import { readJsonObject, sendJson } from '../httpJson.ts';
import { folderIdField, numberParameter, textField } from '../requestFields.ts';
import { decodedParameter, sendResult, wholeNumber, type Route } from '../router.ts';

/**
 * The workspace: what a person may see, and the folders they organise it in.
 *
 * @remarks
 * Every route passes the signed-in person to the workspace functions, which decide; none of them
 * decides anything itself. An id in the path that is not a number is answered as an id that does
 * not exist; a field or a parameter of the wrong type is a 400 (see `requestFields.ts`).
 */
export const workspaceRoutes: readonly Route[] = [
  {
    method: 'GET',
    pattern: /^\/api\/platform\/workspace$/,
    handle: async ({ database, response, person }) => {
      sendJson(response, 200, { organizations: await readWorkspace(database, person.userId) });
    }
  },
  {
    method: 'GET',
    pattern: /^\/api\/platform\/projects\/(?<id>\d+)$/,
    handle: async ({ database, response, person, params }) => {
      sendResult(response, await readProject(database, { userId: person.userId, projectId: idOf(params) }));
    }
  },
  {
    method: 'GET',
    pattern: /^\/api\/platform\/projects\/(?<id>\d+)\/changes$/,
    handle: async ({ database, response, person, params, query }) => {
      const outcome = query.get('outcome');
      const limit = numberParameter(query, 'limit', MaximumPage);
      const before = numberParameter(query, 'before', Number.MAX_SAFE_INTEGER);
      const result = await listChanges(database, {
        userId: person.userId,
        projectId: idOf(params),
        ...(outcome === null ? {} : { outcome }),
        ...(limit === undefined ? {} : { limit }),
        ...(before === undefined ? {} : { before })
      });

      sendResult(response, result);
    }
  },
  {
    method: 'GET',
    pattern: /^\/api\/platform\/projects\/(?<id>\d+)\/compilations$/,
    handle: async ({ database, response, person, params, query }) => {
      const limit = numberParameter(query, 'limit', MaximumPage);

      sendResult(response, await listCompilations(database, { userId: person.userId, projectId: idOf(params), ...(limit === undefined ? {} : { limit }) }));
    }
  },
  {
    method: 'PATCH',
    pattern: /^\/api\/platform\/projects\/(?<id>\d+)$/,
    handle: async ({ database, request, response, person, params }) => {
      const folderId = folderIdField(await readJsonObject(request), 'folderId') ?? null;

      sendResult(response, await fileProject(database, { userId: person.userId, projectId: idOf(params), folderId }));
    }
  },
  {
    method: 'POST',
    pattern: /^\/api\/platform\/folders$/,
    handle: async ({ database, request, response, person }) => {
      const body = await readJsonObject(request);
      const result = await createFolder(database, {
        userId: person.userId,
        organizationId: textField(body, 'organizationId'),
        parentId: folderIdField(body, 'parentId') ?? null,
        name: textField(body, 'name')
      });

      sendResult(response, result, 201);
    }
  },
  {
    method: 'PATCH',
    pattern: /^\/api\/platform\/folders\/(?<id>\d+)$/,
    handle: async ({ database, request, response, person, params }) => {
      const body = await readJsonObject(request);
      const parentId = folderIdField(body, 'parentId');
      const result = await updateFolder(database, {
        userId: person.userId,
        folderId: idOf(params),
        ...('name' in body ? { name: textField(body, 'name') } : {}),
        ...(parentId === undefined ? {} : { parentId })
      });

      sendResult(response, result);
    }
  },
  {
    method: 'DELETE',
    pattern: /^\/api\/platform\/folders\/(?<id>\d+)$/,
    handle: async ({ database, response, person, params }) => {
      sendResult(response, await deleteFolder(database, { userId: person.userId, folderId: idOf(params) }));
    }
  },
  {
    method: 'POST',
    pattern: /^\/api\/platform\/organizations\/(?<id>[^/]+)\/stations$/,
    handle: async ({ database, request, response, person, params }) => {
      const body = await readJsonObject(request);
      const result = await linkStation(database, {
        userId: person.userId,
        organizationId: decodedParameter(params['id']),
        stationName: textField(body, 'stationName'),
        pairingCode: textField(body, 'pairingCode')
      });

      sendResult(response, result);
    }
  }
];

function idOf(params: Readonly<Record<string, string>>): number {
  return wholeNumber(params['id']) ?? -1;
}
