import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The environment variable that overrides where the server executable is. */
const ServerPathVariable = 'TIA_MCP_SERVER';

const RepositoryRelativeBuild = join('src', 'TiaMcpServer', 'bin', 'Debug', 'net48', 'TiaMcpServer.exe');

/**
 * Finds TiaMcpServer.exe.
 *
 * @returns The full path to the executable.
 * @throws If neither the override nor the repository's own build is there.
 * @remarks
 * Repository-relative, not machine-absolute: the default is the debug build this repository
 * produces, resolved from this file's own location, so it works wherever the repository is checked
 * out. That is the difference the no-hardcoded-paths rule is about — the forked code pinned
 * `D:\Siemens\...` and was dead on every other machine.
 *
 * `TIA_MCP_SERVER` overrides it, which is what a release build or an installed server needs.
 */
export function resolveServerExecutable(): string {
  const override = process.env[ServerPathVariable];

  if (override !== undefined && override.length > 0) {
    if (!existsSync(override)) {
      throw new Error(`${ServerPathVariable} points at '${override}', which does not exist.`);
    }

    return override;
  }

  const candidate = join(repositoryRoot(), RepositoryRelativeBuild);

  if (!existsSync(candidate)) {
    throw new Error(
      `The server is not built: '${candidate}' does not exist. Run 'dotnet build TiaMcpServer.sln', ` +
        `or set ${ServerPathVariable} to an executable elsewhere.`
    );
  }

  return candidate;
}

/**
 * Why the server executable cannot be found, if it cannot.
 *
 * @returns The reason, worded for a person, or undefined when the executable is there.
 * @remarks
 * For the suites that start the real server. They cannot run on a machine where it is not built,
 * and on a hosted CI runner it never can be: TiaMcpServer.csproj references the Openness resolver,
 * which needs an installed TIA Portal at build time. Before this existed those suites threw in
 * their `before` hook, which the test runner reports as seven failures and one red build — the
 * wrong answer to "this cannot be checked here".
 *
 * It asks the same question `resolveServerExecutable` answers, rather than a second copy of it, so
 * the two cannot drift apart.
 */
export function serverExecutableAbsence(): string | undefined {
  try {
    resolveServerExecutable();

    return undefined;
  } catch (failure) {
    return failure instanceof Error ? failure.message : String(failure);
  }
}

/** The repository root, found by walking up from this file until the solution is there. */
export function repositoryRoot(): string {
  let directory = dirname(fileURLToPath(import.meta.url));

  while (!existsSync(join(directory, 'TiaMcpServer.sln'))) {
    const parent = resolve(directory, '..');

    if (parent === directory) {
      throw new Error('Could not find the repository root: no TiaMcpServer.sln in any parent directory.');
    }

    directory = parent;
  }

  return directory;
}
