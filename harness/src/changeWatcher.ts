/** Told when the recorded data has changed. The token is opaque: only its equality means anything. */
export type ChangeListener = (token: string) => void;

/** Told when the token could not be read at all, so a broken watch is never a quiet one. */
export type WatchFailure = (reason: string) => void;

/**
 * Notices that the store changed and tells whoever is watching.
 *
 * @remarks
 * It carries no data, and that is the design rather than an omission. What it sends is "something is
 * different now", never what — so the numbers on a screen still come from the endpoints that already
 * serve them, through the one path that computes them. A stream that carried its own copy of a
 * measurement would be a second way to produce it, and two ways to produce a number are two numbers
 * as soon as one of them is changed.
 *
 * It polls. The alternative would be for the process that writes the store to push events to the
 * process that serves it, and that trades a cheap query for a second source of truth plus a run that
 * behaves differently depending on whether a dashboard happens to be open. SQLite in WAL mode lets a
 * reader ask this question a hundred times a second without touching the writer at all.
 *
 * Nothing here is timed: `poll` is called by whoever owns the clock. That keeps every rule below
 * testable without waiting for a real second to pass.
 */
export class ChangeWatcher {
  private readonly readToken: () => string;
  private readonly listeners = new Set<ChangeListener>();
  private lastToken: string;

  /**
   * @param readToken How to ask the store what state it is in.
   * @remarks
   * The current token is read here, at construction. A watcher that started with no token would
   * treat its first poll as a change and tell everybody to reload for nothing.
   */
  constructor(readToken: () => string) {
    this.readToken = readToken;
    this.lastToken = readToken();
  }

  /** How many are listening. The server stops polling when this reaches zero. */
  get listenerCount(): number {
    return this.listeners.size;
  }

  /**
   * Adds a listener.
   *
   * @returns How to remove it. Call it when the connection closes, or the watcher keeps writing to a
   * socket that is gone.
   * @remarks
   * A listener joining does not get told anything. It has just read the endpoints it cares about, so
   * an immediate event would make every new connection reload the page it had only just loaded.
   */
  subscribe(listener: ChangeListener): () => void {
    this.listeners.add(listener);

    return () => {
      this.listeners.delete(listener);
    };
  }

  /**
   * Asks the store whether anything has changed, and tells the listeners if it has.
   *
   * @param onFailure Told when the store could not be read, once per call.
   * @remarks
   * A listener that throws does not stop the others being told. One dead socket among five open
   * dashboards must not silence the four that are fine — and the throw is reported, not swallowed,
   * because a listener failing every second is something whoever is watching the log needs to see.
   */
  poll(onFailure: WatchFailure): void {
    let token: string;

    try {
      token = this.readToken();
    } catch (error) {
      onFailure(error instanceof Error ? error.message : String(error));

      return;
    }

    if (token === this.lastToken) {
      return;
    }

    this.lastToken = token;

    for (const listener of [...this.listeners]) {
      this.tell(listener, token, onFailure);
    }
  }

  private tell(listener: ChangeListener, token: string, onFailure: WatchFailure): void {
    try {
      listener(token);
    } catch (error) {
      onFailure(error instanceof Error ? error.message : String(error));
    }
  }
}
