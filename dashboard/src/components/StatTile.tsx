import type { ReactNode } from 'react';
import { Card, CardContent } from '@/components/ui/card';

type Properties = {
  readonly label: string;
  /** The figure itself. Big, because it is the thing being said. */
  readonly value: string;
  /**
   * What the figure was computed from.
   *
   * @remarks
   * Required. A rate without its denominator is the roadmap's named forbidden thing — 83% hides
   * whether it was five of six or fifty of sixty — and a tile is exactly where a number stops being
   * questioned. Making this a mandatory parameter means the sample size cannot be left out by
   * being forgotten; it has to be left out on purpose, and there is nowhere to type that.
   */
  readonly basis: string;
  /** Optional state. It always ships with a word, never colour alone. */
  readonly tone?: 'good' | 'warning' | 'critical' | undefined;
};

const Tones: Readonly<Record<string, string>> = {
  good: 'text-[var(--status-good)]',
  warning: 'text-[var(--status-warning)]',
  critical: 'text-[var(--status-critical)]'
};

/** A single headline figure: the right form when the data is one number, not a one-bar chart. */
export function StatTile({ label, value, basis, tone }: Properties): ReactNode {
  const colour = tone === undefined ? '' : (Tones[tone] ?? '');

  return (
    <Card className="gap-0 py-5">
      <CardContent className="px-5">
        <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{label}</p>
        <p className={`tabular mt-2 text-3xl leading-none font-semibold ${colour}`}>{value}</p>
        <p className="text-muted-foreground mt-2 text-xs leading-relaxed">{basis}</p>
      </CardContent>
    </Card>
  );
}
