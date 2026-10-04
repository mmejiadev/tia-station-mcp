import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { readAuthSettings } from '../src/auth/authSettings.ts';

const Complete = {
  BETTER_AUTH_SECRET: 'a'.repeat(32),
  BETTER_AUTH_URL: 'http://localhost:5173',
  GOOGLE_CLIENT_ID: 'google-id',
  GOOGLE_CLIENT_SECRET: 'google-secret',
  GITHUB_CLIENT_ID: 'github-id',
  GITHUB_CLIENT_SECRET: 'github-secret'
};

describe('auth settings', () => {
  it('reads a complete environment', () => {
    const settings = readAuthSettings(Complete);

    assert.equal(settings.github.clientId, 'github-id');
  });

  it('names every missing variable at once', () => {
    assert.throws(
      () => readAuthSettings({ ...Complete, GOOGLE_CLIENT_SECRET: '', GITHUB_CLIENT_ID: undefined }),
      /GOOGLE_CLIENT_SECRET, GITHUB_CLIENT_ID/
    );
  });

  it('refuses a short secret', () => {
    assert.throws(() => readAuthSettings({ ...Complete, BETTER_AUTH_SECRET: 'short' }), /at least 32/);
  });

  it('never puts a value in the error', () => {
    assert.throws(
      () => readAuthSettings({ ...Complete, BETTER_AUTH_URL: '' }),
      (failure: Error) => !failure.message.includes('google-secret') && !failure.message.includes('github-secret')
    );
  });
});
