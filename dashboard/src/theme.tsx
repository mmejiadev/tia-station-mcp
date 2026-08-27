import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';

/** The two themes. Nothing here follows the operating system: the choice is explicit and it sticks. */
export type Theme = 'light' | 'dark';

const StorageKey = 'tia-dashboard-theme';

type ThemeState = {
  readonly theme: Theme;
  readonly toggle: () => void;
};

const ThemeContext = createContext<ThemeState>({ theme: 'light', toggle: () => undefined });

/**
 * Puts the stored theme on the document before anything renders.
 *
 * @returns The theme that was applied.
 * @remarks
 * Called from `main.tsx` ahead of the first render, and that ordering is a bug fix rather than
 * tidiness. The charts paint onto a canvas, so they read their colours out of the document with
 * `getComputedStyle` — and a class applied in an effect lands *after* the render that read them.
 * The dashboard opened in the dark theme and drew every chart in the light palette: dark grey labels
 * on a near-black surface, invisible, and a grid meant to recede at 12% opacity glaring at full
 * strength. Everything else on the page was fine, which is exactly why it took a screenshot to see.
 */
export function applyStoredTheme(): Theme {
  const theme = readStoredTheme();

  applyTheme(theme);

  return theme;
}

/**
 * Holds the theme.
 *
 * @remarks
 * The document is written *in the toggle*, synchronously, before the state change that re-renders
 * anybody. Same reason as above: by the time a chart re-reads the palette, the class it reads
 * through has to be the new one already.
 *
 * The dark palette is a chosen set of steps, not an inversion — the same hues re-stepped so they
 * still separate from each other and from a dark surface. That is why the two are declared side by
 * side in the stylesheet rather than computed from one another.
 */
export function ThemeProvider({ children }: { readonly children: ReactNode }): ReactNode {
  const [theme, setTheme] = useState<Theme>(readStoredTheme);

  const toggle = useCallback(() => {
    setTheme((current) => {
      const next = current === 'dark' ? 'light' : 'dark';

      applyTheme(next);
      remember(next);

      return next;
    });
  }, []);

  return <ThemeContext.Provider value={{ theme, toggle }}>{children}</ThemeContext.Provider>;
}

/** The theme, and how to change it. */
export function useTheme(): ThemeState {
  return useContext(ThemeContext);
}

function applyTheme(theme: Theme): void {
  document.documentElement.classList.toggle('dark', theme === 'dark');
}

/**
 * Remembers the choice, and does not mind if it cannot.
 *
 * @remarks
 * Wrapped because a browser with site data blocked throws on the very first access rather than
 * returning nothing, and a dashboard that fails to render over a preference has its priorities
 * backwards.
 */
function remember(theme: Theme): void {
  try {
    window.localStorage.setItem(StorageKey, theme);
  } catch {
    // A preference that cannot be remembered is not a failure worth showing anybody.
  }
}

function readStoredTheme(): Theme {
  try {
    return window.localStorage.getItem(StorageKey) === 'dark' ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}
