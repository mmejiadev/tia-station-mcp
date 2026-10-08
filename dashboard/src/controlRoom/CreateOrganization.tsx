import { Building2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { authClient } from '../auth/authClient.ts';
import { ActionFailure } from './ActionFailure.tsx';
import { organizationSlug, randomSlugSuffix } from './organizationSlug.ts';
import { useAction } from './useAction.ts';

/**
 * Creates an organisation; whoever creates it is its admin.
 *
 * @param onCreated Called with the new organisation's id once it exists, to open it.
 * @remarks
 * Better Auth's own endpoint does it, so its rules apply — the name it refuses, the creator's role.
 * It says so when it worked: a page that looked the same afterwards invited a second press, and a
 * second organisation with a fresh slug (review of 2026-10-06).
 */
export function CreateOrganization({ onCreated }: { readonly onCreated: (organizationId: string) => void }): ReactNode {
  const [name, setName] = useState('');
  const [created, setCreated] = useState('');
  const action = useAction();

  const create = async (): Promise<void> => {
    const trimmed = name.trim();
    let createdId = '';

    const succeeded = await action.run(async () => {
      const result = await authClient.organization.create({ name: trimmed, slug: organizationSlug(trimmed, randomSlugSuffix()) });

      if (result.error !== null) {
        throw new Error(result.error.message ?? 'The organisation was not created.');
      }

      createdId = result.data.id;
    });

    if (succeeded) {
      setName('');
      setCreated(trimmed);
      onCreated(createdId);
    }
  };

  return (
    <form
      className="space-y-2"
      onSubmit={(event) => {
        event.preventDefault();
        void create();
      }}
    >
      <label className="block space-y-1">
        <span className="text-sm font-medium">Organisation name</span>
        <Input placeholder="e.g. Grau Superior · 2n" value={name} onChange={(event) => setName(event.target.value)} />
      </label>
      <Button type="submit" disabled={action.pending || name.trim().length === 0}>
        <Building2 aria-hidden="true" />
        Create organisation
      </Button>
      <ActionFailure action={action} />
      {created.length > 0 && action.failure.length === 0 ? (
        <p role="status" className="text-sm text-[var(--status-good)]">
          {created} was created and is now open. You are its admin.
        </p>
      ) : undefined}
    </form>
  );
}
