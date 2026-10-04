import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { roleCan } from '../src/auth/permissions.ts';

/** The role table, read the way the platform's endpoints read it. */
describe('permissions', () => {
  it('lets every role read projects', () => {
    const readers = ['viewer', 'engineer', 'supervisor', 'admin'].map((role) => roleCan(role, { project: ['read'] }));

    assert.deepEqual(readers, [true, true, true, true]);
  });

  it('lets only an engineer and above describe projects', () => {
    const describers = ['viewer', 'engineer', 'supervisor', 'admin'].map((role) => roleCan(role, { project: ['describe'] }));

    assert.deepEqual(describers, [false, true, true, true]);
  });

  it('lets only a supervisor or an admin confirm an identity', () => {
    const confirmers = ['viewer', 'engineer', 'supervisor', 'admin'].map((role) => roleCan(role, { identity: ['confirm'] }));

    assert.deepEqual(confirmers, [false, false, true, true]);
  });

  it('lets only an admin link a station to the organisation', () => {
    const linkers = ['viewer', 'engineer', 'supervisor', 'admin'].map((role) => roleCan(role, { station: ['link'] }));

    assert.deepEqual(linkers, [false, false, false, true]);
  });

  it('grants nothing to a role it does not know', () => {
    // A role renamed, mistyped or written into the database by hand is a refusal, never a permission.
    assert.equal(roleCan('owner', { project: ['read'] }), false);
    assert.equal(roleCan('', { project: ['read'] }), false);
  });

  it('grants what any one of several roles grants', () => {
    assert.equal(roleCan('viewer, supervisor', { identity: ['confirm'] }), true);
  });
});
