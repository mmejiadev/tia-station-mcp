import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Joins class names, letting a later Tailwind utility win over an earlier one of the same kind.
 *
 * @remarks
 * shadcn's own convention, and every generated component in `components/ui/` imports it by this
 * name. Plain `clsx` would leave `p-2 p-6` both in the string and let the stylesheet order decide,
 * which makes a component's `className` override work by accident or not at all.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
