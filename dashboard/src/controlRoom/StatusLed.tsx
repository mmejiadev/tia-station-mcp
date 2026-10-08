import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import type { ProjectStatus } from '../../../platform/src/workspace/workspaceRead.ts';
import { StatusLooks } from './projectStatus.ts';

/**
 * A project's status as a LED and its word.
 *
 * @param status The status.
 * @param className How the word is set, when not the sidebar's small capitals.
 * @remarks
 * The word is always there, never only the colour (`docs/WEB-PLATFORM.md`); the sentence behind it
 * is the tooltip, so "No data" says why there is none. Every LED on the page is drawn by this, so
 * that rule lives in one place.
 */
export function StatusLed({ status, className }: { readonly status: ProjectStatus; readonly className?: string }): ReactNode {
  const look = StatusLooks[status];

  return (
    <span className={cn('flex items-center gap-1.5 font-mono text-[11px] tracking-wide uppercase', className)} title={look.meaning}>
      <span className={`led ${look.led}`} aria-hidden="true" />
      {look.word}
    </span>
  );
}
