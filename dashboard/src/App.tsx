import { Moon, Radio, RadioTower, Sun } from 'lucide-react';
import type { ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { AccountMenu } from './components/AccountMenu.tsx';
import { ModeBanner } from './components/ModeBanner.tsx';
import { useLive } from './live.tsx';
import { useTheme } from './theme.tsx';
import { CopilotDock } from './components/CopilotDock.tsx';
import { useHash } from './useHash.ts';
import { AuditView } from './views/AuditView.tsx';
import { ControlRoomView } from './views/ControlRoomView.tsx';
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
 *
 * The control room is the web platform's: organisations, their projects and each project's history.
 * It sits beside the harness views rather than replacing them, as was decided for phase 4.
 */
const Views: Readonly<Record<string, () => ReactNode>> = {
  Overview: OverviewView,
  'Control room': ControlRoomView,
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
  const openView = viewFromHash(useHash(), ViewNames);

  return (
    <div className="min-h-screen">
      <ModeBanner />

      <div className="mx-auto max-w-7xl px-6 pb-16">
        <header className="flex flex-wrap items-center justify-between gap-4 py-6">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">TIA station — harness</h1>
            <p className="text-muted-foreground text-sm">
              Everything here is read from what was recorded. Nothing on this page changes a TIA Portal project or a controller.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <AccountMenu />
            <LiveIndicator />
            <ThemeButton />
          </div>
        </header>

        <Tabs value={openView} onValueChange={(name) => (window.location.hash = hashFor(name))}>
          <TabsList>
            {/* The click as well as the change: a view with places inside it — a project in the
                control room — goes back to its start when its own tab is pressed, which Tabs does
                not report because the value did not change. */}
            {ViewNames.map((name) => (
              <TabsTrigger key={name} value={name} onClick={() => (window.location.hash = hashFor(name))}>
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
          Writes to a project go through the guard in the MCP server, and confirming one is done there.
          This dashboard has no endpoint that reaches TIA Portal or a controller, and is not going to have
          one. What the control room changes is the platform's own record — organisations, folders, which
          station belongs to whom. The copilot is given the recorded numbers and no tools, so there is
          nothing it can do but answer.
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
