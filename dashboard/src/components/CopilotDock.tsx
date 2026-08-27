import { AlertTriangle, Bot, KeyRound, Loader2, Send, X } from 'lucide-react';
import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { ChatTurn, CopilotStatus } from '../api.ts';
import { askCopilot, readCopilotStatus } from '../api.ts';
import { useLoaded } from '../useLoaded.ts';

/** Questions worth asking, offered because an empty text box tells nobody what this can answer. */
const Suggestions: readonly string[] = [
  'How many runs are recorded?',
  'Which specification passes least often?',
  'Which phase takes longest?',
  'Which gate criteria are not met?'
];

/** One exchange, with what the answer cost when there was a price for it. */
type Exchange = {
  readonly question: string;
  readonly answer: string;
  readonly costDollars: number | undefined;
  readonly inputTokens: number;
  readonly outputTokens: number;
};

/**
 * The plant copilot, docked in the corner of every view.
 *
 * @remarks
 * The half of the roadmap's copilot that was cut on 2026-08-27 for want of an API key, built the
 * same day once there was one. It began as a seventh tab and was moved here on request: a copilot
 * you have to navigate away from the numbers to reach is one you stop asking, and the question
 * somebody wants to ask is nearly always about the view they are looking at right now.
 *
 * **It lives outside the tabs on purpose.** Rendered by `App` as a sibling of the whole tab strip,
 * so switching views does not unmount it and a conversation survives moving from Metrics to Runs to
 * the gate. It is not in a route and it is not in the address bar, because it is not a place - it is
 * a thing that is always there.
 *
 * Three properties are kept from the tab it replaced, and none of them are cosmetic.
 *
 * **It says what it costs, per turn and in total.** Every answer carries its tokens and its price. A
 * chat whose bill is invisible is one nobody can reason about, and this one is billed to an account
 * with a balance somebody topped up by hand.
 *
 * **It says when there is nothing to talk to.** No API key means the panel opens and explains
 * itself rather than offering a text box that fails on the first question.
 *
 * **The conversation lives in this tab of the browser and nowhere else.** Nothing is written to the
 * store, nothing reaches the audit trail, and a reload ends it. What somebody typed into a box is
 * not a measurement, and this project keeps a hard line between the two.
 */
export function CopilotDock(): ReactNode {
  const [open, setOpen] = useState(false);
  const status = useLoaded(readCopilotStatus, []);

  // Kept here rather than inside the panel, so closing the dock does not throw the conversation
  // away. Somebody who asks a question, goes to look at the table it was about and comes back
  // expects to find the answer still there.
  const [exchanges, setExchanges] = useState<readonly Exchange[]>([]);

  useEffect(() => {
    if (!open) {
      return;
    }

    const closeOnEscape = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };

    window.addEventListener('keydown', closeOnEscape);

    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [open]);

  return (
    <div className="fixed right-4 bottom-4 z-50 flex flex-col items-end gap-3 print:hidden">
      {open ? (
        <Panel
          status={status.state === 'loaded' ? status.value : undefined}
          failure={status.state === 'failed' ? status.reason : undefined}
          exchanges={exchanges}
          onExchanges={setExchanges}
          onClose={() => setOpen(false)}
        />
      ) : undefined}

      <OpenButton open={open} turns={exchanges.length} onToggle={() => setOpen((was) => !was)} />
    </div>
  );
}

/** The round button that is always there. */
function OpenButton({
  open,
  turns,
  onToggle
}: {
  readonly open: boolean;
  readonly turns: number;
  readonly onToggle: () => void;
}): ReactNode {
  return (
    <Button
      onClick={onToggle}
      size="lg"
      className="size-14 rounded-full shadow-lg"
      aria-expanded={open}
      aria-label={open ? 'Close the copilot' : 'Ask the copilot about what was recorded'}
      title={open ? 'Close the copilot' : 'Ask the copilot about what was recorded'}
    >
      {open ? <X className="size-6" aria-hidden="true" /> : <Bot className="size-6" aria-hidden="true" />}
      {/* A conversation left behind an open button would otherwise be invisible from the page. */}
      {!open && turns > 0 ? (
        <span className="bg-background text-foreground absolute -top-1 -right-1 rounded-full border px-1.5 text-xs">
          {turns}
        </span>
      ) : undefined}
    </Button>
  );
}

/** The panel itself: header, conversation, question box. */
function Panel({
  status,
  failure,
  exchanges,
  onExchanges,
  onClose
}: {
  readonly status: CopilotStatus | undefined;
  readonly failure: string | undefined;
  readonly exchanges: readonly Exchange[];
  readonly onExchanges: (exchanges: readonly Exchange[]) => void;
  readonly onClose: () => void;
}): ReactNode {
  return (
    <section
      className="bg-card text-card-foreground flex max-h-[min(34rem,70vh)] w-[min(24rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-xl border shadow-2xl"
      aria-label="Plant copilot"
    >
      <header className="flex items-start justify-between gap-2 border-b px-4 py-3">
        <div className="space-y-0.5">
          <p className="flex items-center gap-2 text-sm font-semibold">
            <Bot className="size-4" aria-hidden="true" />
            Plant copilot
          </p>
          <Spend model={status?.model} exchanges={exchanges} />
        </div>

        <Button variant="ghost" size="icon" onClick={onClose} aria-label="Close the copilot">
          <X className="size-4" />
        </Button>
      </header>

      {failure !== undefined ? (
        <Unreachable reason={failure} />
      ) : status === undefined ? (
        <p className="text-muted-foreground flex items-center gap-2 px-4 py-6 text-sm">
          <Loader2 className="size-4 animate-spin" aria-hidden="true" />
          Reading…
        </p>
      ) : status.available ? (
        <Conversation model={status.model} exchanges={exchanges} onExchanges={onExchanges} />
      ) : (
        <Unavailable status={status} />
      )}
    </section>
  );
}

/** The API could not be asked at all, which is a different thing from having no key. */
function Unreachable({ reason }: { readonly reason: string }): ReactNode {
  return (
    <div className="p-4">
      <Alert variant="destructive" role="alert">
        <AlertTriangle aria-hidden="true" />
        <AlertTitle>The copilot could not be reached.</AlertTitle>
        <AlertDescription>{reason}</AlertDescription>
      </Alert>
    </div>
  );
}

/**
 * Why there is nothing to talk to, and what to do about it.
 *
 * @remarks
 * The reason comes from the server verbatim, and it already names the file to put the key in. A
 * message written here instead would be a second copy of that instruction, free to drift from the
 * one the harness prints when a run fails for the same reason.
 */
function Unavailable({ status }: { readonly status: CopilotStatus }): ReactNode {
  return (
    <div className="space-y-3 p-4">
      <p className="flex items-center gap-2 text-sm font-medium">
        <KeyRound className="size-4" aria-hidden="true" />
        There is no copilot on this machine.
      </p>
      <p className="text-muted-foreground text-sm">{status.reason}</p>
      <p className="text-muted-foreground text-xs">
        Every view works without one: they read what was recorded, and only this needs a model.
      </p>
    </div>
  );
}

/** The conversation, and the box to add to it. */
function Conversation({
  model,
  exchanges,
  onExchanges
}: {
  readonly model: string;
  readonly exchanges: readonly Exchange[];
  readonly onExchanges: (exchanges: readonly Exchange[]) => void;
}): ReactNode {
  const [question, setQuestion] = useState('');
  const [asking, setAsking] = useState(false);
  const [failure, setFailure] = useState<string | undefined>(undefined);
  const box = useRef<HTMLInputElement>(null);
  const list = useRef<HTMLDivElement>(null);

  // The newest exchange is the one somebody is waiting for, and in a panel this size it is below the
  // fold the moment there are two of them.
  useEffect(() => {
    const shown = list.current;

    if (shown !== null) {
      shown.scrollTop = shown.scrollHeight;
    }
  }, [exchanges.length, asking]);

  useEffect(() => box.current?.focus(), []);

  const ask = async (asked: string): Promise<void> => {
    const trimmed = asked.trim();

    if (trimmed.length === 0 || asking) {
      return;
    }

    setAsking(true);
    setFailure(undefined);
    setQuestion('');

    try {
      const answer = await askCopilot(trimmed, historyOf(exchanges));

      onExchanges([
        ...exchanges,
        {
          question: trimmed,
          answer: answer.answer,
          costDollars: answer.costDollars,
          inputTokens: answer.usage.inputTokens,
          outputTokens: answer.usage.outputTokens
        }
      ]);
    } catch (error) {
      // The question goes back in the box. A failed turn that also loses what was typed makes the
      // person retype it to find out whether the failure was about them or about the connection.
      setQuestion(trimmed);
      setFailure(error instanceof Error ? error.message : String(error));
    } finally {
      setAsking(false);
      box.current?.focus();
    }
  };

  const submit = (event: FormEvent): void => {
    event.preventDefault();
    void ask(question);
  };

  return (
    <>
      <div ref={list} className="flex-1 space-y-4 overflow-y-auto px-4 py-3">
        {exchanges.length === 0 ? <Opening onPick={(asked) => void ask(asked)} disabled={asking} /> : undefined}

        <ol className="space-y-4">
          {exchanges.map((exchange, index) => (
            <li key={index} className="space-y-2">
              <p className="bg-muted ml-auto w-fit max-w-[85%] rounded-lg px-3 py-2 text-sm">{exchange.question}</p>
              <div className="space-y-1">
                <p className="text-sm leading-relaxed whitespace-pre-wrap">{exchange.answer}</p>
                <p className="text-muted-foreground tabular text-xs">
                  {exchange.inputTokens} in, {exchange.outputTokens} out
                  {exchange.costDollars === undefined
                    ? ' — no price on file'
                    : ` — ${formatCost(exchange.costDollars)}`}
                </p>
              </div>
            </li>
          ))}
        </ol>

        {asking ? (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <Loader2 className="size-4 animate-spin" aria-hidden="true" />
            Asking {model}…
          </p>
        ) : undefined}

        {failure === undefined ? undefined : (
          <Alert variant="destructive" role="alert">
            <AlertTriangle aria-hidden="true" />
            <AlertTitle>That question was not answered.</AlertTitle>
            <AlertDescription>{failure}</AlertDescription>
          </Alert>
        )}
      </div>

      <form onSubmit={submit} className="flex gap-2 border-t p-3">
        <Input
          ref={box}
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="Ask about the runs, timings or the gate"
          aria-label="Your question for the copilot"
          disabled={asking}
        />
        <Button type="submit" size="icon" disabled={asking || question.trim().length === 0} aria-label="Ask">
          <Send className="size-4" aria-hidden="true" />
        </Button>
      </form>
    </>
  );
}

/** What to ask, before anything has been asked. */
function Opening({
  onPick,
  disabled
}: {
  readonly onPick: (question: string) => void;
  readonly disabled: boolean;
}): ReactNode {
  return (
    <div className="space-y-3">
      <p className="text-muted-foreground text-sm">
        It answers from the same recorded numbers these views are drawn from, and nothing else. It has no
        tools, it cannot change anything, and it will not answer a question about safety — those go to the
        machine documentation and to the supervisor at the cell.
      </p>
      <div className="flex flex-wrap gap-2">
        {Suggestions.map((suggestion) => (
          <Button
            key={suggestion}
            variant="outline"
            size="sm"
            disabled={disabled}
            onClick={() => onPick(suggestion)}
            className="h-auto py-1 text-xs whitespace-normal"
          >
            {suggestion}
          </Button>
        ))}
      </div>
    </div>
  );
}

/** Which model answers, and what has been spent talking to it. */
function Spend({
  model,
  exchanges
}: {
  readonly model: string | undefined;
  readonly exchanges: readonly Exchange[];
}): ReactNode {
  const priced = exchanges.filter((exchange) => exchange.costDollars !== undefined);
  const total = priced.reduce((sum, exchange) => sum + (exchange.costDollars ?? 0), 0);

  return (
    <p className="text-muted-foreground text-xs">
      {model ?? 'reading…'}
      {exchanges.length === 0 ? '' : ` · ${exchanges.length} turn(s)`}
      {/* Only the turns that have a price are added up, and it says so when some do not. A total
          that quietly skipped the unpriced ones would understate the bill by exactly them. */}
      {priced.length === 0 ? '' : ` · ${formatCost(total)}`}
      {priced.length === exchanges.length ? '' : ` · ${exchanges.length - priced.length} unpriced`}
    </p>
  );
}

/**
 * Everything said so far, in the order it was said.
 *
 * @remarks
 * Rebuilt from the exchanges rather than kept as a second list. Two structures holding the same
 * conversation is two structures that can disagree about it, and the one sent to the model is the
 * one that would silently be wrong.
 */
function historyOf(exchanges: readonly Exchange[]): readonly ChatTurn[] {
  return exchanges.flatMap((exchange) => [
    { role: 'user' as const, text: exchange.question },
    { role: 'assistant' as const, text: exchange.answer }
  ]);
}

/**
 * A cost, at enough decimal places to be a number rather than a rounding.
 *
 * @remarks
 * A turn costs under a cent, so two decimal places would print every one of them as $0.00 — which
 * reads as free. Four places is the difference between "this is cheap" and "this is nothing", and
 * only one of those is true.
 */
function formatCost(dollars: number): string {
  return `$${dollars.toFixed(4)}`;
}
