import { HelpCircle, ShieldAlert, ShieldCheck } from 'lucide-react';
import type { ReactNode } from 'react';
import { readMode } from '../api.ts';
import { useLive } from '../live.tsx';
import { useLoaded } from '../useLoaded.ts';

/** What each answer looks like. Anything unrecognised is unknown, never a fourth kind of safe. */
type Appearance = {
  readonly icon: typeof ShieldCheck;
  readonly headline: string;
  readonly className: string;
};

const Appearances: Readonly<Record<string, Appearance>> = {
  Study: {
    icon: ShieldCheck,
    headline: 'Study Mode — everything recorded here targeted PLCSIM Advanced.',
    className: 'bg-[color-mix(in_oklab,var(--status-good)_12%,transparent)] text-[var(--status-good)]'
  },
  Workshop: {
    icon: ShieldAlert,
    headline:
      'Workshop Mode — physical hardware may have been commanded. A supervisor must be present at the cell.',
    className: 'bg-[color-mix(in_oklab,var(--status-critical)_14%,transparent)] text-[var(--status-critical)]'
  }
};

const Unknown: Appearance = {
  icon: HelpCircle,
  headline: 'Mode unknown — nothing recorded says which, so assume nothing.',
  className: 'bg-[color-mix(in_oklab,var(--status-warning)_14%,transparent)] text-[var(--status-warning)]'
};

/**
 * The permanent banner: which mode the recorded operations were carried out in.
 *
 * @remarks
 * Permanent in the sense the roadmap means — on every view, never dismissible, never below the fold.
 * Three things it deliberately does not do:
 *
 * It does not assume. While the mode is being read it says so, and if it cannot be read it says that
 * instead of falling back to Study. A banner that shows the safe answer when it does not know is
 * worse than no banner, because it is trusted.
 *
 * It does not claim to describe the live session. What the API can see is the audit trail, so this
 * says what has been *recorded*; the authority on a session in progress is `GetOperationMode` on the
 * server, and the banner names it.
 *
 * And it never softens Workshop. One recorded operation in Workshop Mode colours the whole banner,
 * because the question it answers is whether a physical machine may have been commanded.
 */
export function ModeBanner(): ReactNode {
  const { revision } = useLive();
  const loaded = useLoaded(readMode, [revision]);

  if (loaded.state === 'loading') {
    return <Banner appearance={Unknown} detail="Reading which mode the recorded work was done in…" />;
  }

  if (loaded.state === 'failed') {
    return <Banner appearance={Unknown} detail={`The mode could not be read, so it is not known: ${loaded.reason}`} />;
  }

  const appearance = Appearances[loaded.value.mode] ?? Unknown;

  return <Banner appearance={appearance} detail={`Read from ${loaded.value.source}.`} />;
}

function Banner({ appearance, detail }: { appearance: Appearance; detail: string }): ReactNode {
  const Icon = appearance.icon;

  return (
    <div className={`flex items-start gap-3 px-6 py-2.5 text-sm ${appearance.className}`} role="status">
      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
      <p>
        <span className="font-semibold">{appearance.headline}</span>{' '}
        <span className="text-muted-foreground">{detail}</span>
      </p>
    </div>
  );
}
