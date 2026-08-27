import { AlertTriangle, Loader2 } from 'lucide-react';
import type { ReactNode } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import type { Loaded } from '../useLoaded.ts';

/** What one panel needs in order to show any of the three states of a read. */
type Properties<T> = {
  readonly loaded: Loaded<T>;
  readonly children: (value: T) => ReactNode;
};

/**
 * Renders a panel once its data is there, and says so plainly when it is not.
 *
 * @remarks
 * Every view goes through this so that a failure is impossible to mistake for an empty result. The
 * reason the API gave is shown verbatim: "There is no metrics store at C:\..." tells whoever is
 * looking what to do, and "could not load" does not.
 */
export function WhenLoaded<T>({ loaded, children }: Properties<T>): ReactNode {
  if (loaded.state === 'loading') {
    return (
      <p className="text-muted-foreground flex items-center gap-2 py-6 text-sm">
        <Loader2 className="size-4 animate-spin" aria-hidden="true" />
        Reading…
      </p>
    );
  }

  if (loaded.state === 'failed') {
    return (
      <Alert variant="destructive" role="alert">
        <AlertTriangle aria-hidden="true" />
        <AlertTitle>This could not be read, so nothing below it is being shown.</AlertTitle>
        <AlertDescription>{loaded.reason}</AlertDescription>
      </Alert>
    );
  }

  return children(loaded.value);
}
