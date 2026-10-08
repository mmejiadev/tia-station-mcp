import type { ReactNode } from 'react';
import type { FolderChoice } from './workspaceTree.ts';

type Properties = {
  readonly label: string;
  readonly choices: readonly FolderChoice[];
  /** The chosen folder, or null for the top level. */
  readonly value: number | null;
  readonly onChange: (folderId: number | null) => void;
};

// A select's value is text; the top level needs one no folder id can be.
const TopLevel = 'top';

/**
 * Picks a folder, or the top level, from the organisation's tree.
 *
 * @remarks
 * A native select rather than dragging: it works from a keyboard and on a phone, and dragging can be
 * added on top of it later without taking it away.
 */
export function FolderSelect({ label, choices, value, onChange }: Properties): ReactNode {
  return (
    <label className="block space-y-1">
      <span className="text-muted-foreground block text-xs">{label}</span>
      <select
        className="border-input bg-background h-8 w-full rounded-md border px-2 text-sm"
        value={value === null ? TopLevel : String(value)}
        onChange={(event) => onChange(event.target.value === TopLevel ? null : Number(event.target.value))}
      >
        <option value={TopLevel}>Top level</option>
        {choices.map((choice) => (
          <option key={choice.id} value={choice.id}>
            {`${'  '.repeat(choice.depth + 1)}${choice.name}`}
          </option>
        ))}
      </select>
    </label>
  );
}
