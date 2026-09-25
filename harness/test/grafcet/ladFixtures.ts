/**
 * LAD documents shaped exactly like the ones TIA Portal V20 exported from a class project on
 * 2026-09-24, with the names made generic. The faulty ones reproduce the faults that project really
 * had; a test built on an invented fault would only prove the checker finds what its author imagined.
 */

/** Wraps networks in the header and footer TIA Portal writes around a function. */
export function fc(name: string, networks: readonly string[]): string {
  const body = networks.map((network) => `    { S7_Language := "LAD" }\n    NETWORK\n${network}    END_NETWORK\n`).join('');
  return `{\n    S7_Optimized := "TRUE";\n    S7_PreferredLanguage := "LAD";\n    S7_Version := "0.1"\n}\nFUNCTION "${name}" : Void\n${body}END_FUNCTION\n`;
}

/** One transition in the set/reset method: sources and contacts, S targets, R sources. */
export function transition(sources: readonly string[], contacts: readonly string[], targets: readonly string[]): string {
  const series = [...sources.map((s) => `Contact( "${s}" )`), ...contacts].map((c) => `            ${c}\n`).join('');
  const sets = targets.map((t, i) => (i === 0 ? '' : `        RUNG wire#w1\n            S_Coil( "${t}" )\n        END_RUNG\n`)).join('');
  const resets = sources.map((s) => `        RUNG wire#w1\n            R_Coil( "${s}" )\n        END_RUNG\n`).join('');
  return `        RUNG wire#powerrail\n${series}            wire#w1\n            S_Coil( "${targets[0]}" )\n        END_RUNG\n${sets}${resets}`;
}

/** The feeder conveyor as the student first wrote it: an unquoted operand, and a step cleared by a plain coil. */
export const FaultyFeeder = fc('FEEDER', [
  transition(['X0'], ['Contact( I )'], ['X1']),
  transition(['X1'], ['Contact( "PART" )'], ['X2']),
  `        RUNG wire#powerrail
            Contact( "X2" )
            Contact( "PART" )
            Contact( "HOME" )
            wire#w1
            S_Coil( "X3" )
        END_RUNG
        RUNG wire#w1
            Coil( "X2" )
        END_RUNG
`,
  transition(['X3'], ['I_Contact( "PART" )'], ['X0']),
]);

/** The same conveyor as corrected: every step is set and reset. */
export const Feeder = fc('FEEDER', [
  transition(['X0'], ['Contact( "START" )'], ['X1']),
  transition(['X1'], ['Contact( "PART" )'], ['X2']),
  transition(['X2'], ['Contact( "PART" )', 'Contact( "HOME" )'], ['X3']),
  transition(['X3'], ['I_Contact( "PART" )'], ['X0']),
]);

/** A transfer with an on-delay and a selection, and a sequence that waits on another's step. */
export const Transfer = fc('TRANSFER', [
  transition(['X30'], ['Contact( "START" )'], ['X31']),
  `        RUNG wire#powerrail
            Contact( "X31" )
            "IEC_Timer_0_DB".TON(
                pt := T#30S,
                et =>
            )
            wire#w1
            S_Coil( "X32" )
        END_RUNG
        RUNG wire#w1
            R_Coil( "X31" )
        END_RUNG
`,
  transition(['X32'], ['Contact( "SCALE" )'], ['X33']),
  transition(['X32'], ['I_Contact( "SCALE" )'], ['X34']),
  transition(['X33'], ['Contact( "DONE" )'], ['X30']),
  transition(['X34'], ['Contact( "DONE" )'], ['X30']),
]);

/** A second conveyor started by step 33 of the transfer. */
export const Outfeed = fc('OUTFEED', [
  transition(['X50'], ['Contact( "X33" )'], ['X51']),
  transition(['X51'], ['I_Contact( "EXIT" )'], ['X50']),
]);

/** Outputs as plain coils OR-ing the steps, the way the class project wrote them. */
export const Outputs = fc('OUTPUTS', [
  `        RUNG wire#powerrail
            Contact( "X1" )
            wire#w1
            Coil( "MOTOR" )
        END_RUNG
        RUNG wire#powerrail
            Contact( "X3" )
        END_RUNG wire#w1
`,
]);

/** Initialisation with bit ranges, as the class project had it. */
export const Init = fc('INIT', [
  `        RUNG wire#powerrail
            Contact( "FirstScan" )
            wire#w1
            S_Coil( "X0" )
        END_RUNG
        RUNG wire#w1
            R_BitfieldCoil(
                operand => "X1",
                n := 3
            )
        END_RUNG
`,
  `        RUNG wire#powerrail
            Contact( "FirstScan" )
            wire#w1
            S_Coil( "X30" )
        END_RUNG
        RUNG wire#w1
            R_BitfieldCoil(
                operand => "X31",
                n := 4
            )
        END_RUNG
`,
  `        RUNG wire#powerrail
            Contact( "FirstScan" )
            wire#w1
            S_Coil( "X50" )
        END_RUNG
        RUNG wire#w1
            R_Coil( "X51" )
        END_RUNG
`,
]);

/** Step addresses where the transfer's steps are consecutive: the ranges above are right. */
export const ConsecutiveTags = `X0 %M0.0
X1 %M0.1
X2 %M0.2
X3 %M0.3
X30 %M10.0
X31 %M10.1
X32 %M10.2
X33 %M10.3
X34 %M10.4
X50 %M4.0
X51 %M4.1
`;

/** The class project's mistake: the outfeed's steps sit between the transfer's. */
export const InterleavedTags = `X0 %M0.0
X1 %M0.1
X2 %M0.2
X3 %M0.3
X30 %M3.2
X31 %M3.3
X32 %M3.4
X50 %M3.5
X51 %M3.6
X33 %M3.7
X34 %M4.0
`;
