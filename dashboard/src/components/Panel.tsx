import type { ReactNode } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type Properties = {
  readonly title: string;
  /**
   * What this panel means, in one sentence.
   *
   * @remarks
   * Required, not optional, and that is the point. A dashboard is read by somebody who was not in
   * the room when the number was chosen — a teacher, an examiner, Samreen in three months. A chart
   * with a title and no sentence saying what it is claiming is a chart that gets misread, and the
   * cheapest place to prevent that is here, where the panel is declared.
   */
  readonly explanation: string;
  readonly children: ReactNode;
  /** Shown on the right of the header: a sample size, a live badge, a filter. */
  readonly aside?: ReactNode;
};

/** One thing on the page, with a title, a sentence explaining it, and whatever it is showing. */
export function Panel({ title, explanation, children, aside }: Properties): ReactNode {
  return (
    <Card className="gap-4">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <CardTitle className="text-base">{title}</CardTitle>
            <CardDescription className="max-w-2xl leading-relaxed">{explanation}</CardDescription>
          </div>
          {aside === undefined ? undefined : <div className="shrink-0">{aside}</div>}
        </div>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}
