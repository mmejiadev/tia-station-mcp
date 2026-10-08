import type { ReactNode } from 'react';
import type { Action } from './useAction.ts';

/** The reason a change was refused, in the server's words, where it was asked for. */
export function ActionFailure({ action }: { readonly action: Action }): ReactNode {
  return action.failure.length === 0 ? undefined : (
    <p role="alert" className="text-xs text-[var(--status-critical)]">
      {action.failure}
    </p>
  );
}
