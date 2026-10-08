import type { ProjectStatus } from '../../../platform/src/workspace/workspaceRead.ts';

/** How one status is shown: its word, its LED, and the sentence behind it. */
export type StatusLook = {
  readonly word: string;
  /** The LED's class in styles.css. */
  readonly led: 'led-ok' | 'led-warning' | 'led-error' | 'led-unknown';
  readonly meaning: string;
};

/**
 * Every status the platform reports, and how it looks.
 *
 * @remarks
 * A record over the type rather than a switch, so a fifth status added to the platform fails to
 * compile here instead of being drawn as one of the four.
 *
 * The colours are the ones `docs/WEB-PLATFORM.md` fixes for the whole web — green compiles, amber
 * has warnings, red has errors, grey has no data — and **never colour alone**: each has its word.
 * "Unknown" is grey, not green: a project never compiled through the MCP has not been shown to work.
 */
export const StatusLooks: Readonly<Record<ProjectStatus, StatusLook>> = {
  ok: { word: 'Compiles', led: 'led-ok', meaning: 'The latest compilation had no errors and no warnings.' },
  warning: { word: 'Warnings', led: 'led-warning', meaning: 'The latest compilation had warnings and no errors.' },
  error: { word: 'Errors', led: 'led-error', meaning: 'The latest compilation had errors.' },
  unknown: { word: 'No data', led: 'led-unknown', meaning: 'Never compiled through the MCP server, so nothing is known.' }
};

/**
 * The status one compilation gives, by the rule the platform applies to a project's latest.
 *
 * @param counts Its errors and warnings.
 * @returns Errors first, then warnings, then ok; never unknown, because a compilation is known.
 */
export function statusOfCounts(counts: { readonly errorCount: number; readonly warningCount: number }): ProjectStatus {
  if (counts.errorCount > 0) {
    return 'error';
  }

  return counts.warningCount > 0 ? 'warning' : 'ok';
}
