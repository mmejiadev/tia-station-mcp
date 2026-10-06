import type { ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';

/**
 * One audit outcome.
 *
 * @remarks
 * Refused is deliberately not painted as a failure. A refusal is the governance layer working, and
 * colouring it red would teach whoever reads this page to see the guard doing its job as something
 * going wrong.
 *
 * Shared by the harness's audit view and the control room's change history: the same outcome is
 * the same colour on both pages.
 */
export function OutcomeBadge({ outcome, count }: { outcome: string; count?: number }): ReactNode {
  const tone =
    outcome === 'Applied'
      ? 'border-[var(--status-good)] text-[var(--status-good)]'
      : outcome === 'Failed'
        ? 'border-[var(--status-critical)] text-[var(--status-critical)]'
        : outcome === 'Refused'
          ? 'border-[var(--status-warning)] text-[var(--status-warning)]'
          : '';

  return (
    <Badge variant="outline" className={`tabular ${tone}`}>
      {outcome}
      {count === undefined ? '' : ` ${count}`}
    </Badge>
  );
}
