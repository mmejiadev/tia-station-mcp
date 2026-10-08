import { useRef } from 'react';
import type { Loaded } from '../useLoaded.ts';

/**
 * A read that keeps showing its last value while it is read again.
 *
 * @param loaded The read, as `useLoaded` reports it.
 * @returns The last loaded value while a new read is under way; otherwise the read itself.
 * @remarks
 * The control room reads the workspace again after every change. Shown as "Reading…", that
 * unmounted the form that made the change, and with it the sentence saying it had worked — measured
 * on 2026-10-06, linking `MANUELA`: the station was linked and the page said nothing.
 *
 * Only the loading state is bridged. A read that fails is shown as failed, never covered by the
 * value before it: an old workspace on screen after the platform stopped answering would be a lie.
 */
export function useKeptWhileReloading<T>(loaded: Loaded<T>): Loaded<T> {
  const last = useRef<Loaded<T>>(loaded);

  if (loaded.state !== 'loading') {
    last.current = loaded;
  }

  return loaded.state === 'loading' && last.current.state === 'loaded' ? last.current : loaded;
}
