import { parseArgs } from 'node:util';
import { openDatabase } from '../db/connection.ts';
import { issuePairingCode } from './stationPairing.ts';

/**
 * `npm run pair -- --station <name>`
 *
 * Prints a pairing code for a station. Run it on the station's own machine — the one whose history
 * was imported — and type the code into the web, as an admin of the organisation it belongs to.
 */
const { values } = parseArgs({ options: { station: { type: 'string' } } });

if (values.station === undefined) {
  console.error('Usage: npm run pair -- --station <name>');
  process.exit(2);
}

const connection = openDatabase(process.env['DATABASE_URL']);

try {
  const result = await issuePairingCode(connection.database, values.station);

  if (result.kind === 'refused') {
    console.error(result.reason);
    process.exitCode = 1;
  } else {
    console.log(`Pairing code for ${values.station}: ${result.value.code}`);
    console.log(`Valid until ${result.value.expiresAt.toLocaleTimeString()}, once. Type it into the web as an admin of the organisation.`);
  }
} finally {
  await connection.close();
}
