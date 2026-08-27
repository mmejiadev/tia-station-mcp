import { useEffect, useState } from 'react';

/**
 * What a read from the API is doing right now.
 *
 * @remarks
 * Three states, and the third one is the point: a failed read is its own state, not an empty value.
 * A view that could not tell "the API is down" from "there is nothing recorded" would show an empty
 * audit table for both, and one of those is a lie about whether anything was changed.
 */
export type Loaded<T> =
  | { readonly state: 'loading' }
  | { readonly state: 'failed'; readonly reason: string }
  | { readonly state: 'loaded'; readonly value: T };

/**
 * Reads once, and again whenever the dependencies change.
 *
 * @param load What to read.
 * @param dependencies What the read depends on, as React compares them.
 * @returns The state of that read.
 * @remarks
 * A read that finishes after its inputs changed is discarded rather than shown. Without that, a
 * filter typed quickly enough leaves whichever request happened to return last on the screen, which
 * is a table that does not match the filter above it.
 */
export function useLoaded<T>(load: () => Promise<T>, dependencies: readonly unknown[]): Loaded<T> {
  const [loaded, setLoaded] = useState<Loaded<T>>({ state: 'loading' });

  useEffect(() => {
    let current = true;

    setLoaded({ state: 'loading' });

    load()
      .then((value) => {
        if (current) {
          setLoaded({ state: 'loaded', value });
        }
      })
      .catch((error: unknown) => {
        if (current) {
          setLoaded({ state: 'failed', reason: error instanceof Error ? error.message : String(error) });
        }
      });

    return () => {
      current = false;
    };
    // The caller states what the read depends on; `load` is rebuilt every render and is not one of
    // them. Listing it would re-read on every render, for ever.
  }, dependencies);

  return loaded;
}
