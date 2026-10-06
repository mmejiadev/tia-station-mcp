import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { FolderView } from '../../platform/src/workspace/folders.ts';
import type { OrganizationView, ProjectSummaryView } from '../../platform/src/workspace/workspaceRead.ts';
import { chooseOrganization } from '../src/controlRoom/chooseOrganization.ts';
import { hashForPlace, placeFromHash } from '../src/controlRoom/controlRoomRoute.ts';
import { describeShown } from '../src/controlRoom/describeShown.ts';
import { organizationSlug, randomSlugSuffix } from '../src/controlRoom/organizationSlug.ts';
import { StatusLooks, statusOfCounts } from '../src/controlRoom/projectStatus.ts';
import { canLinkStations, canOrganize } from '../src/controlRoom/roleActions.ts';
import { buildTree, folderChoices, projectCount } from '../src/controlRoom/workspaceTree.ts';
import { viewFromHash } from '../src/viewRoute.ts';

const Base = '#/control-room';

function folder(id: number, parentId: number | null, name = `Folder ${id}`): FolderView {
  return { id, organizationId: 'org', parentId, name };
}

function project(id: number, folderId: number | null, name = `Project ${id}`): ProjectSummaryView {
  return { id, name, folderId, stationName: 'MANUELA', status: 'unknown', errorCount: 0, warningCount: 0, lastCompiledAt: null };
}

function organization(folders: FolderView[], projects: ProjectSummaryView[], id = 'org'): OrganizationView {
  return { id, name: id, role: 'engineer', folders, projects };
}

describe('where the control room is pointed', () => {
  it('opens a project from a link somebody sent', () => {
    // The teacher reading a project from another machine is what phase 4 is done by.
    assert.deepEqual(placeFromHash('#/control-room/projects/12/changes', Base), { kind: 'project', projectId: 12, section: 'changes' });
  });

  it('opens the summary when the link names no section, or one that does not exist', () => {
    assert.deepEqual(placeFromHash('#/control-room/projects/12', Base), { kind: 'project', projectId: 12, section: 'summary' });
    assert.deepEqual(placeFromHash('#/control-room/projects/12/hardware', Base), { kind: 'project', projectId: 12, section: 'summary' });
  });

  it('opens the workspace for anything that names no project', () => {
    assert.deepEqual(placeFromHash(Base, Base), { kind: 'workspace' });
    assert.deepEqual(placeFromHash('#/control-room/projects/abc', Base), { kind: 'workspace' });
    // Beyond PostgreSQL's integer range it cannot be a project, so it is not sent to the server.
    assert.deepEqual(placeFromHash('#/control-room/projects/12345678901', Base), { kind: 'workspace' });
  });

  it('writes the fragment it reads', () => {
    for (const hash of [Base, `${Base}/projects/7`, `${Base}/projects/7/compilations`]) {
      assert.equal(hashForPlace(placeFromHash(hash, Base), Base), hash);
    }
  });

  it('gives the control room everything below its own fragment, and nothing that only starts like it', () => {
    const views = ['Overview', 'Control room'];

    assert.equal(viewFromHash('#/control-room/projects/12', views), 'Control room');
    assert.equal(viewFromHash('#/control-roomy', views), 'Overview');
  });
});

describe('the sidebar tree', () => {
  it('nests folders and files each project in its folder', () => {
    const tree = buildTree(organization([folder(1, null), folder(2, 1)], [project(10, 2), project(11, null)]), '');

    assert.equal(tree.roots[0]?.children[0]?.projects[0]?.id, 10);
    assert.deepEqual(tree.unfiled.map((item) => item.id), [11]);
    assert.equal(projectCount(tree.roots[0]!), 1);
  });

  it('draws a folder whose parent is missing at the top level instead of dropping it', () => {
    const tree = buildTree(organization([folder(5, 99)], []), '');

    assert.deepEqual(tree.roots.map((node) => node.folder.id), [5]);
  });

  it('draws each folder of a loop exactly once', () => {
    // The server prevents loops; if one reached the page anyway, it must neither hang nor duplicate.
    const tree = buildTree(organization([folder(1, 2), folder(2, 1)], []), '');

    assert.deepEqual(folderChoices(tree).map((choice) => choice.id).sort(), [1, 2]);
  });

  it('shows a project filed in a folder it cannot see as unfiled', () => {
    const tree = buildTree(organization([], [project(10, 42)]), '');

    assert.deepEqual(tree.unfiled.map((item) => item.id), [10]);
  });

  it('keeps only the folders that hold a match while searching', () => {
    const tree = buildTree(organization([folder(1, null), folder(2, null)], [project(10, 1, 'Motor'), project(11, 2, 'Semàfor')]), 'mot');

    assert.deepEqual(tree.roots.map((node) => node.folder.id), [1]);
  });

  it('does not offer a folder, or anything inside it, as the place to move it to', () => {
    const tree = buildTree(organization([folder(1, null), folder(2, 1), folder(3, null)], []), '');

    assert.deepEqual(folderChoices(tree, 1).map((choice) => choice.id), [3]);
  });
});

describe('which buttons a role is shown', () => {
  it('lets an engineer and above organise folders, and not a viewer', () => {
    assert.equal(canOrganize('engineer'), true);
    assert.equal(canOrganize('viewer'), false);
  });

  it('lets only an admin link a station', () => {
    assert.equal(canLinkStations('admin'), true);
    assert.equal(canLinkStations('supervisor'), false);
  });

  it('shows a role it does not know nothing', () => {
    // The server grants an unknown role nothing; the page must not offer it more.
    assert.equal(canOrganize('Admin'), false);
    assert.equal(canLinkStations('owner'), false);
  });

  it('reads several roles separated by commas, as Better Auth stores them', () => {
    assert.equal(canLinkStations('viewer, admin'), true);
  });
});

describe('how a status is shown', () => {
  it('gives every status a word, never only a colour', () => {
    for (const look of Object.values(StatusLooks)) {
      assert.ok(look.word.length > 0);
    }
  });

  it('does not show a project never compiled as healthy', () => {
    assert.notEqual(StatusLooks.unknown.led, StatusLooks.ok.led);
  });

  it('ranks errors over warnings, as the platform does', () => {
    assert.equal(statusOfCounts({ errorCount: 1, warningCount: 3 }), 'error');
    assert.equal(statusOfCounts({ errorCount: 0, warningCount: 3 }), 'warning');
    assert.equal(statusOfCounts({ errorCount: 0, warningCount: 0 }), 'ok');
  });
});

describe('which organisation the sidebar shows', () => {
  const first = organization([], [project(1, null)], 'first');
  const second = organization([], [project(2, null)], 'second');

  it("shows an open project's organisation, whichever was picked last", () => {
    assert.equal(chooseOrganization([first, second], { kind: 'project', projectId: 2, section: 'summary' }, 'first')?.id, 'second');
  });

  it('shows the one picked when no project is open', () => {
    assert.equal(chooseOrganization([first, second], { kind: 'workspace' }, 'second')?.id, 'second');
  });

  it('falls back to the first when the one picked is gone', () => {
    assert.equal(chooseOrganization([first, second], { kind: 'workspace' }, 'deleted')?.id, 'first');
  });
});

describe('a list cut at its page size', () => {
  it('says how many it shows out of how many there are', () => {
    // A tab counting 120 over a list of 50 said nothing about the other 70 (review, 2026-10-06).
    assert.equal(describeShown(100, 120, 'compilations'), 'Showing the 100 newest of 120 compilations.');
  });

  it('says nothing when nothing was left out', () => {
    assert.equal(describeShown(12, 12, 'compilations'), '');
  });
});

describe('the slug a new organisation gets', () => {
  it('reads as the name, accents dropped rather than the letters', () => {
    assert.equal(organizationSlug('Sistemes programables avançats', 'abc123'), 'sistemes-programables-avancats-abc123');
  });

  it('still makes one from a name with no letters', () => {
    assert.equal(organizationSlug('···', 'abc123'), 'abc123');
  });

  it('keeps the suffix random and short', () => {
    assert.match(randomSlugSuffix(), /^[a-z0-9]{6}$/);
  });
});
