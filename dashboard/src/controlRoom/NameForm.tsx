import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ActionFailure } from './ActionFailure.tsx';
import type { Action } from './useAction.ts';

type Properties = {
  /** What the field is, for whoever cannot see the layout: "New folder name". */
  readonly label: string;
  readonly initial: string;
  readonly submitLabel: string;
  readonly action: Action;
  readonly onSubmit: (name: string) => Promise<boolean>;
  readonly onCancel: () => void;
};

/**
 * One name, typed in place: creating a folder or renaming one.
 *
 * @remarks
 * The server trims and bounds the name and says so when it refuses; this does not repeat those
 * rules, so there is one place they live. Escape cancels, as it does everywhere else.
 */
export function NameForm({ label, initial, submitLabel, action, onSubmit, onCancel }: Properties): ReactNode {
  const [name, setName] = useState(initial);

  return (
    <form
      className="space-y-1.5 py-1"
      onSubmit={(event) => {
        event.preventDefault();
        void onSubmit(name);
      }}
    >
      <Input
        aria-label={label}
        autoFocus
        className="h-8"
        value={name}
        onChange={(event) => setName(event.target.value)}
        onKeyDown={(event) => (event.key === 'Escape' ? onCancel() : undefined)}
      />
      <div className="flex gap-1.5">
        <Button type="submit" size="xs" disabled={action.pending}>
          {submitLabel}
        </Button>
        <Button type="button" size="xs" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
      <ActionFailure action={action} />
    </form>
  );
}
