import { Link2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ActionFailure } from './ActionFailure.tsx';
import { useControlRoom } from './ControlRoomContext.tsx';
import { linkStation } from './platformApi.ts';
import { useAction } from './useAction.ts';

/**
 * Links a station to the organisation with the code printed on the station's own machine.
 *
 * @remarks
 * The code is the proof the station is theirs — anybody can create an organisation and be its admin
 * — so it is asked for here and never shown or stored by the page. When it fails, the server's one
 * answer for every failure is shown as it came: a different message for "no such station" would
 * tell anybody which machines exist.
 */
export function PairStation(): ReactNode {
  const { organization, refresh } = useControlRoom();
  const [stationName, setStationName] = useState('');
  const [pairingCode, setPairingCode] = useState('');
  const [linked, setLinked] = useState('');
  const action = useAction();

  const pair = async (): Promise<void> => {
    if (await action.run(() => linkStation(organization.id, { stationName, pairingCode }))) {
      setLinked(stationName.trim());
      setPairingCode('');
      refresh();
    }
  };

  return (
    <form
      className="space-y-3"
      onSubmit={(event) => {
        event.preventDefault();
        void pair();
      }}
    >
      <p className="text-muted-foreground text-sm">
        On the station, run <code className="bg-muted rounded px-1.5 py-0.5 font-mono text-xs">npm run pair -- --station NAME</code> in{' '}
        <code className="bg-muted rounded px-1.5 py-0.5 font-mono text-xs">platform/</code>. It prints a code that works once, for 30 minutes.
      </p>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="block space-y-1">
          <span className="text-sm font-medium">Station name</span>
          <Input placeholder="MANUELA" value={stationName} onChange={(event) => setStationName(event.target.value)} />
        </label>
        <label className="block space-y-1">
          <span className="text-sm font-medium">Pairing code</span>
          <Input className="font-mono tracking-widest uppercase" autoComplete="off" spellCheck={false} value={pairingCode} onChange={(event) => setPairingCode(event.target.value)} />
        </label>
      </div>
      <Button type="submit" disabled={action.pending || stationName.trim().length === 0 || pairingCode.trim().length === 0}>
        <Link2 aria-hidden="true" />
        Link station
      </Button>
      <ActionFailure action={action} />
      {linked.length > 0 && action.failure.length === 0 ? (
        <p role="status" className="text-sm text-[var(--status-good)]">
          {linked} is linked to {organization.name}. Its projects appear in the sidebar once the station has imported them.
        </p>
      ) : undefined}
    </form>
  );
}
