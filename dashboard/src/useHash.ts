import { useEffect, useState } from 'react';

/**
 * The address bar's fragment, kept in step with it.
 *
 * @returns `location.hash` as it is now.
 * @remarks
 * Listening to `hashchange` rather than only reading once is what makes the browser's own back
 * button work. Without it the address bar and the page disagree after one press, which is the kind
 * of small wrongness that makes a tool feel untrustworthy about everything else it says.
 */
export function useHash(): string {
  const [hash, setHash] = useState(() => window.location.hash);

  useEffect(() => {
    const follow = (): void => setHash(window.location.hash);

    window.addEventListener('hashchange', follow);

    return () => window.removeEventListener('hashchange', follow);
  }, []);

  return hash;
}
