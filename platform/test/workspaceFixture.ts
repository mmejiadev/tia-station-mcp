import type { PlatformDatabase } from '../src/db/connection.ts';
import { change, compilation, compilationMessage, project, station } from '../src/db/schema.ts';
import { aPerson, addMember, anOrganization } from './people.ts';

/** The rows a workspace test starts from. */
export type WorkspaceFixture = {
  readonly stationId: number;
  readonly projectId: number;
};

/**
 * Two organisations, five people, and one imported station with one project, not yet linked.
 *
 * @remarks
 * `class` has an admin, a supervisor, an engineer and a viewer; `other-class` has its own admin; the
 * outsider belongs to neither. The station's history is in the database — a project, two changes,
 * two compilations — and nobody can see it until an admin links the station.
 */
export async function aWorkspace(database: PlatformDatabase): Promise<WorkspaceFixture> {
  await anOrganization(database, 'class');
  await anOrganization(database, 'other-class');

  for (const [person, organization, role] of [
    ['owner', 'class', 'admin'],
    ['teacher', 'class', 'supervisor'],
    ['student', 'class', 'engineer'],
    ['reader', 'class', 'viewer'],
    ['other-owner', 'other-class', 'admin']
  ] as const) {
    await aPerson(database, person);
    await addMember(database, organization, person, role);
  }

  await aPerson(database, 'outsider');

  const [created] = await database.insert(station).values({ name: 'MANUELA' }).returning({ id: station.id });
  const stationId = created?.id ?? -1;
  const projectId = await aProject(database, stationId);

  return { stationId, projectId };
}

async function aProject(database: PlatformDatabase, stationId: number): Promise<number> {
  const [created] = await database
    .insert(project)
    .values({ stationId, tiaPath: 'C:\\Projects\\Cell\\Cell.ap20', name: 'Cell', tiaAuthor: 'mamem' })
    .returning({ id: project.id });
  const projectId = created?.id ?? -1;

  await aCompilation(database, { stationId, projectId, at: '2026-10-01T10:00:00Z', errors: 2, line: 1 });
  await aCompilation(database, { stationId, projectId, at: '2026-10-02T10:00:00Z', errors: 0, line: 2 });

  for (const [index, outcome] of ['Applied', 'Refused'].entries()) {
    await database.insert(change).values({
      stationId,
      projectId,
      entryKey: `key-${index}`,
      lineNumber: index + 1,
      timestamp: '2026-10-02T09:00:00+00:00',
      occurredAt: new Date('2026-10-02T09:00:00Z'),
      planId: `PLAN-${index}`,
      mode: 'Study',
      tool: 'WriteScl',
      target: 'PLC_1/Blocks/FC_Motor',
      value: '',
      backupPath: '',
      origin: 'agent',
      outcome,
      detail: '',
      documentation: '',
      projectPath: 'C:\\Projects\\Cell\\Cell.ap20'
    });
  }

  return projectId;
}

async function aCompilation(
  database: PlatformDatabase,
  values: { stationId: number; projectId: number; at: string; errors: number; line: number }
): Promise<void> {
  const [created] = await database
    .insert(compilation)
    .values({
      stationId: values.stationId,
      projectId: values.projectId,
      entryKey: `compilation-${values.line}`,
      lineNumber: values.line,
      compiledAt: new Date(values.at),
      softwarePath: 'PLC_1',
      severity: values.errors > 0 ? 'Error' : 'Success',
      errorCount: values.errors,
      warningCount: 1
    })
    .returning({ id: compilation.id });

  if (values.errors > 0 && created !== undefined) {
    await database
      .insert(compilationMessage)
      .values({ compilationId: created.id, position: 0, severity: 'Error', path: 'Main', description: 'Clock_1Hz is not defined' });
  }
}
