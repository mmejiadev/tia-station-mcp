import { readFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { openDatabase } from '../db/connection.ts';
import { importAuditTrail } from './auditImport.ts';

/**
 * `npm run import:audit -- --station <name> --file <audit.jsonl>`
 *
 * Imports one station's audit trail into DATABASE_URL. Exits with 1 when the trail is refused, so a
 * scheduled import that meets a tampered trail fails visibly rather than reporting nothing new.
 */
const { values } = parseArgs({
  options: {
    station: { type: 'string' },
    file: { type: 'string' }
  }
});

if (values.station === undefined || values.file === undefined) {
  console.error('Usage: npm run import:audit -- --station <name> --file <path to audit.jsonl>');
  process.exit(2);
}

const lines = readFileSync(values.file, 'utf8').split('\n');
const connection = openDatabase(process.env['DATABASE_URL']);

try {
  const result = await importAuditTrail(connection.database, { stationName: values.station, lines });

  if (result.kind === 'refused') {
    console.error(`Refused, nothing imported: line ${result.line}: ${result.reason}`);
    process.exitCode = 1;
  } else {
    console.log(
      `Imported ${result.inserted} new entr(ies) of ${result.read}; ${result.alreadyPresent} were already there` +
        (result.unchained > 0 ? `; ${result.unchained} predate chaining and are not attested.` : '.')
    );
  }
} finally {
  await connection.close();
}
