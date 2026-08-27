import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

/** Whether the page is being told about changes as they happen. */
export type LiveState = {
  /**
   * Goes up by one every time the store changes.
   *
   * @remarks
   * A view lists it among the things its read depends on, and the read repeats. That is the whole
   * mechanism: the event says *that* something changed, never what, so every number on the page
   * still comes from the endpoint that already serves it. A stream carrying its own copy of a
   * measurement would be a second way to produce it, and two ways to produce a number become two
   * different numbers the day one of them is changed.
   */
  readonly revision: number;
  /** Whether the stream is connected right now. Shown, never assumed. */
  readonly connected: boolean;
};

const Live = createContext<LiveState>({ revision: 0, connected: false });

/** What the page is watching, so one connection serves every view on it. */
export function LiveProvider({ children }: { readonly children: ReactNode }): ReactNode {
  const [revision, setRevision] = useState(0);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    // EventSource reconnects on its own after the API restarts, which it does rather a lot while the
    // API is being written. That is most of why this is server-sent events and not a WebSocket.
    const stream = new EventSource('/api/live');

    stream.addEventListener('watching', () => setConnected(true));
    stream.addEventListener('changed', () => setRevision((previous) => previous + 1));
    stream.addEventListener('error', () => setConnected(false));

    return () => stream.close();
  }, []);

  const state = useMemo(() => ({ revision, connected }), [revision, connected]);

  return <Live.Provider value={state}>{children}</Live.Provider>;
}

/**
 * What the page knows about live changes.
 *
 * @remarks
 * The default outside a provider is "not connected, revision 0", which is honest: nothing is
 * watching. It never claims to be live when it is not, because the indicator built on this is the
 * only thing telling somebody whether what they are looking at is still true.
 */
export function useLive(): LiveState {
  return useContext(Live);
}
