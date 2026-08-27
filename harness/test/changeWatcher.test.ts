import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { ChangeWatcher } from '../src/changeWatcher.ts';

/**
 * What the live stream promises is that a screen showing a run in progress keeps up with it.
 *
 * Every rule below is one of the ways that promise breaks quietly: telling nobody, telling everybody
 * for nothing, or telling four of five because the fifth had gone. None of them needs a clock — the
 * watcher does not own one — and none needs a database.
 */
describe('ChangeWatcher', () => {
  it('says nothing while the store is unchanged', () => {
    const store = stubStore('r1:0/i1:0/p1');
    const watcher = new ChangeWatcher(store.read);
    const seen: string[] = [];

    watcher.subscribe((token) => seen.push(token));
    watcher.poll(fail);
    watcher.poll(fail);

    assert.deepEqual(seen, []);
  });

  it('tells the listeners once each time the token moves', () => {
    const store = stubStore('r1:0/i1:0/p1');
    const watcher = new ChangeWatcher(store.read);
    const seen: string[] = [];

    watcher.subscribe((token) => seen.push(token));

    store.token = 'r1:0/i1:0/p2';
    watcher.poll(fail);
    watcher.poll(fail);
    store.token = 'r1:0/i2:1/p2';
    watcher.poll(fail);

    assert.deepEqual(seen, ['r1:0/i1:0/p2', 'r1:0/i2:1/p2']);
  });

  it('tells a listener that joins nothing at all until something changes', () => {
    // It has just read the endpoints it cares about. An event on joining would make every new
    // connection reload the page it had only finished loading.
    const store = stubStore('r1:0/i1:0/p1');
    const watcher = new ChangeWatcher(store.read);
    const seen: string[] = [];

    watcher.poll(fail);
    watcher.subscribe((token) => seen.push(token));
    watcher.poll(fail);

    assert.deepEqual(seen, []);
  });

  it('keeps telling the others when one listener throws', () => {
    // One dead socket among five open dashboards must not silence the four that are fine.
    const store = stubStore('first');
    const watcher = new ChangeWatcher(store.read);
    const failures: string[] = [];
    const seen: string[] = [];

    watcher.subscribe(() => {
      throw new Error('this socket is gone');
    });
    watcher.subscribe((token) => seen.push(token));

    store.token = 'second';
    watcher.poll((reason) => failures.push(reason));

    assert.deepEqual(seen, ['second']);
    // And the throw is reported rather than swallowed: a listener failing every second is something
    // whoever is reading the log needs to see.
    assert.deepEqual(failures, ['this socket is gone']);
  });

  it('reports a store that breaks while it is being watched, rather than going quiet', () => {
    // The difference matters: "nothing is happening" and "I can no longer see what is happening" look
    // identical on a screen, and only one of them is true. A store that is already unreadable when the
    // watch starts is a different case and throws from the constructor, which is right — the server
    // has nothing to serve and should say so at startup rather than sit there watching nothing.
    const store = stubStore('first');
    const watcher = new ChangeWatcher(store.read);
    const failures: string[] = [];
    const seen: string[] = [];

    watcher.subscribe((token) => seen.push(token));
    store.failure = 'the store is gone';
    watcher.poll((reason) => failures.push(reason));

    assert.deepEqual(failures, ['the store is gone']);
    assert.deepEqual(seen, []);
  });

  it('stops telling a listener that has unsubscribed', () => {
    const store = stubStore('first');
    const watcher = new ChangeWatcher(store.read);
    const seen: string[] = [];

    const stop = watcher.subscribe((token) => seen.push(token));

    stop();
    store.token = 'second';
    watcher.poll(fail);

    assert.deepEqual(seen, []);
    assert.equal(watcher.listenerCount, 0);
  });
});

/** A store whose state is a variable, so a test can move it — or break it — between polls. */
function stubStore(token: string): { token: string; failure: string | undefined; read: () => string } {
  const store = {
    token,
    failure: undefined as string | undefined,
    read: (): string => {
      if (store.failure !== undefined) {
        throw new Error(store.failure);
      }

      return store.token;
    }
  };

  return store;
}

/** For the polls that are not supposed to fail: a failure here is the test failing. */
function fail(reason: string): void {
  assert.fail(`The watcher reported a failure it should not have: ${reason}`);
}
