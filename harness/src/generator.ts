import { join } from 'node:path';
import type { McpServerConnection } from './mcpClient.ts';
import type { Specification } from './specification.ts';
import type { TokenUsage } from './telemetry.ts';

/** What the loop tells a generator before it produces a source. */
export type GenerationRequest = {
  readonly specification: Specification;
  /** Which attempt this is, counting from one. */
  readonly attempt: number;
  /** The compiler errors of the previous attempt, empty on the first. */
  readonly previousErrors: readonly string[];
};

/**
 * One generation, and what it cost.
 *
 * @remarks
 * The cost travels with the source rather than being read off the generator afterwards. A
 * generator that remembered its last usage would be a generator with state, and the loop calls it
 * once per attempt inside a timed phase - so "the last one" is a question with a right answer only
 * as long as nothing runs two attempts at once.
 */
export type Generation = {
  readonly source: string;
  /** What the API reported, when a model was asked. Absent for a generator that costs nothing. */
  readonly usage?: TokenUsage;
};

/**
 * A generation that was paid for and produced nothing the loop can use.
 *
 * @remarks
 * It exists to carry the cost out of a failure. Until it did, `createModelGenerator` threw a plain
 * error when the answer held no SCL, the usage went with it, and the store recorded the attempt with
 * no cost at all — so the bill was understated **by exactly the failures**, which is the direction
 * that flatters. The five-run sample of 2026-08-28 paid for 46 generations and recorded 31.
 *
 * `usage` is optional because a generator that costs nothing can still fail; it is the model one
 * that must never lose a cost it incurred.
 */
export class UnusableGeneration extends Error {
  readonly usage: TokenUsage | undefined;

  constructor(message: string, usage?: TokenUsage) {
    super(message);
    this.name = 'UnusableGeneration';
    this.usage = usage;
  }
}

/** Something that produces SCL for a specification. */
export type Generator = {
  /** Recorded with the run, so a number can be attributed to what produced the code. */
  readonly name: string;
  generate(request: GenerationRequest): Promise<Generation>;
};

/**
 * A generator that produces the repository's own patterns instead of asking a model.
 *
 * @remarks
 * **This proves nothing about generation, and it is not meant to.** It exists so the loop can be
 * measured before a model is involved: if a run does not reach a running cell with this, the fault
 * is in the loop, the server or the specification, and no amount of prompt work would have found
 * that out. Which is the user's decision of 2026-08-21 and the reason it was built first.
 *
 * It goes through `ExpandCellScl`, so it exercises the same tool an agent would call, and it asks
 * for the entry point — without the instance data block and the `Main` OB the cell is downloaded
 * and never executed, and every acceptance step would then time out on a program that is simply
 * not running.
 *
 * When the specification asks for a broken first attempt it breaks the `Main` OB, and **breaking an
 * existing block rather than adding a new one is the whole point**. The first version appended a
 * block that could not compile, which failed in a way worth writing down: a block written into a
 * project stays in it, so the next attempt omitting it from the source repaired nothing. The project
 * kept two compiler errors for every remaining attempt — and for the next specification too, which
 * runs on the same project. Regenerating a block that already exists overwrites it; not mentioning
 * one does not delete it.
 */
export function createStubGenerator(connection: McpServerConnection, repositoryRoot: string): Generator {
  return {
    name: 'stub',

    async generate(request: GenerationRequest): Promise<Generation> {
      const result = await connection.callTool('ExpandCellScl', {
        cellPath: join(repositoryRoot, request.specification.cellPath),
        patternDirectory: join(repositoryRoot, 'spec', 'patterns'),
        includeEntryPoint: true
      });

      if (result.isError) {
        throw new Error(`ExpandCellScl failed: ${result.text}`);
      }

      const source = readScl(result.payload, result.text);

      if (request.specification.breakFirstAttempt && request.attempt === 1) {
        return { source: breakTheEntryPoint(source) };
      }

      return { source };
    }
  };
}

const EntryPointEnd = 'END_ORGANIZATION_BLOCK';

/**
 * Puts an error into the `Main` OB, for the case that tests recovery.
 *
 * @remarks
 * An undeclared variable, because it is the error SCL reports most plainly. In the entry point and
 * not in the cell, for two reasons: the OB is one line long so there is no doubt about what broke
 * it, and every attempt regenerates it, so the next attempt overwrites the damage rather than
 * leaving it in the project.
 */
function breakTheEntryPoint(source: string): string {
  if (!source.includes(EntryPointEnd)) {
    throw new Error(
      'The generated source has no Main OB to break, so the recovery case cannot be set up. ' +
        'Was includeEntryPoint passed?'
    );
  }

  return source.replace(EntryPointEnd, `    #ThisVariableWasNeverDeclared := 1;
${EntryPointEnd}`);
}

/** Pulls the SCL out of an ExpandCellScl response. */
function readScl(payload: unknown, text: string): string {
  const scl = (payload as { scl?: unknown } | undefined)?.scl;

  if (typeof scl !== 'string' || scl.length === 0) {
    throw new Error(`ExpandCellScl returned no source: ${text}`);
  }

  return scl;
}
