import { Moon, Radio, RadioTower, Sun } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ModeBanner } from './components/ModeBanner.tsx';
import { useLive } from './live.tsx';
import { useTheme } from './theme.tsx';
import { CopilotDock } from './components/CopilotDock.tsx';
import { AuditView } from './views/AuditView.tsx';
import { GateView } from './views/GateView.tsx';
import { GuideView } from './views/GuideView.tsx';
import { LiveRunView } from './views/LiveRunView.tsx';
import { MetricsView } from './views/MetricsView.tsx';
import { OverviewView } from './views/OverviewView.tsx';
import { RunsView } from './views/RunsView.tsx';
import { hashFor, viewFromHash } from './viewRoute.ts';

/**
 * The views, in the order somebody asking "what has this thing been doing" reads them.
 *
 * @remarks
 * A dictionary rather than a switch over a string, so a view added later cannot be forgotten by the
 * navigation: the tabs are these keys and so are the routes.
 *
 * The roadmap's plant copilot was two halves: a live loop phase and a chat. The first is *Live run*.
 * The second is not in here and never will be — it is `CopilotDock`, docked in the corner of every
 * view at once, because a copilot you have to navigate away from the numbers to reach is one you
 * stop asking.
 */
const Views: Readonly<Record<string, () => ReactNode>> = {
  Overview: OverviewView,
  'Live run': LiveRunView,
  Runs: RunsView,
  Metrics: MetricsView,
  'Audit trail': AuditView,
  'Workshop gate': GateView,
  Guide: GuideView
};

const ViewNames = Object.keys(Views);

/** The whole page: the permanent banner, the tabs, and whichever view the address bar asks for. */
export function App(): ReactNode {
  const openView = useHashView();

  return (
    <div className="min-h-screen">
      <ModeBanner />

      <div className="mx-auto max-w-7xl px-6 pb-16">
        <header className="flex flex-wrap items-center justify-between gap-4 py-6">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">TIA station — harness</h1>
            <p className="text-muted-foreground text-sm">
              Everything here is read from what was recorded. Nothing on this page changes a project.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <LiveIndicator />
            <ThemeButton />
          </div>
        </header>

        <Tabs value={openView} onValueChange={(name) => (window.location.hash = hashFor(name))}>
          <TabsList>
            {ViewNames.map((name) => (
              <TabsTrigger key={name} value={name}>
                {name}
              </TabsTrigger>
            ))}
          </TabsList>

          {ViewNames.map((name) => {
            const View = Views[name];

            return (
              <TabsContent key={name} value={name} className="mt-6">
                {View === undefined ? undefined : <View />}
              </TabsContent>
            );
          })}
        </Tabs>

        <footer className="text-muted-foreground mt-12 border-t pt-4 text-xs">
          Writes go through the guard in the MCP server, and confirming one is done there. This dashboard
          has no endpoint that changes anything, and is not going to have one — the copilot included: it
          is given the recorded numbers and no tools, so there is nothing it can do but answer.
        </footer>
      </div>

      {/* Outside the tabs, and a sibling of the whole page: that is what makes one conversation
          survive moving between views instead of being unmounted with the tab it was started on. */}
      <CopilotDock />
    </div>
  );
}

/**
 * Whether the page is being told about changes as they happen.
 *
 * @remarks
 * It says which of the two it is rather than only lighting up when connected. "Not live" has to be
 * as visible as "live": somebody watching a run go past needs to know when they have stopped being
 * shown it, and a quiet indicator looks exactly like nothing happening.
 */
function LiveIndicator(): ReactNode {
  const { connected, revision } = useLive();
  const Icon = connected ? RadioTower : Radio;

  return (
    <span
      className={`flex items-center gap-2 rounded-md border px-3 py-1.5 text-xs ${
        connected ? 'border-[var(--status-good)] text-[var(--status-good)]' : 'text-muted-foreground'
      }`}
      title={
        connected
          ? 'Connected to the API. Anything a run records shows up here on its own.'
          : 'Not connected. What is on screen is whatever was read last; start npm run api and it will reconnect.'
      }
    >
      <Icon className="size-3.5" aria-hidden="true" />
      {connected ? 'Live' : 'Not live'}
      {revision > 0 ? <span className="tabular opacity-70">· {revision} update(s)</span> : undefined}
    </span>
  );
}

/** The theme switch. The dark palette is a chosen set of steps, not an inversion of the light one. */
function ThemeButton(): ReactNode {
  const { theme, toggle } = useTheme();

  return (
    <Button variant="outline" size="sm" onClick={toggle} aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} theme`}>
      {theme === 'dark' ? <Sun className="size-4" /> : <Moon className="size-4" />}
      {theme === 'dark' ? 'Light' : 'Dark'}
    </Button>
  );
}

/**
 * The view the address bar is asking for, kept in step with it.
 *
 * @remarks
 * Listening to `hashchange` rather than only reading once is what makes the browser's own back
 * button work. Without it the address bar and the page disagree after one press, which is the kind
 * of small wrongness that makes a tool feel untrustworthy about everything else it says.
 */
function useHashView(): string {
  const [openView, setOpenView] = useState(() => viewFromHash(window.location.hash, ViewNames));

  useEffect(() => {
    const follow = (): void => setOpenView(viewFromHash(window.location.hash, ViewNames));

    window.addEventListener('hashchange', follow);

    return () => window.removeEventListener('hashchange', follow);
  }, []);

  return openView;
}
