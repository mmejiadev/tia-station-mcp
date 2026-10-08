import { useState } from 'react';

/** A change somebody asked for: whether it is running, and why it failed when it did. */
export type Action = {
  readonly pending: boolean;
  /** The server's reason, or empty while nothing has failed. */
  readonly failure: string;
  /**
   * Runs the change.
   *
   * @returns Whether it succeeded, so the form can close or stay open with its reason.
   */
  readonly run: (change: () => Promise<unknown>) => Promise<boolean>;
};

/**
 * Runs one change at a time and keeps the reason it was refused.
 *
 * @remarks
 * A refusal is shown where it was asked for, in the server's words, and the form stays as it was so
 * nothing typed is lost. One at a time, because a double click on "Create" would otherwise create
 * two folders.
 */
export function useAction(): Action {
  const [pending, setPending] = useState(false);
  const [failure, setFailure] = useState('');

  const run = async (change: () => Promise<unknown>): Promise<boolean> => {
    if (pending) {
      return false;
    }

    setPending(true);
    setFailure('');

    try {
      await change();
      return true;
    } catch (error: unknown) {
      setFailure(error instanceof Error ? error.message : String(error));
      return false;
    } finally {
      setPending(false);
    }
  };

  return { pending, failure, run };
}
