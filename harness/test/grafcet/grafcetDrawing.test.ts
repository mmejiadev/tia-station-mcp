import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { layOutSequence } from '../../src/grafcet/grafcetLayout.ts';
import { renderGrafcetPage } from '../../src/grafcet/grafcetPage.ts';
import { renderGrafcetSvg } from '../../src/grafcet/grafcetSvg.ts';
import { parseGrafcet } from '../../src/grafcet/grafcetText.ts';
import type { Grafcet } from '../../src/grafcet/grafcetModel.ts';

function read(text: string): Grafcet {
  const parsed = parseGrafcet(text);
  assert.ok(parsed.ok, parsed.ok ? '' : parsed.errors.join('; '));

  return parsed.grafcet;
}

/** A conveyor like the one on the class whiteboard: two ways back to 0 and one plain cycle. */
const Conveyor = read(`sequence Conveyor
step 0 initial
step 1 : MOTOR
step 2
step 3 : MOTOR
0 -> 1 : START
1 -> 2 : PART
1 -> 0 : /START
2 -> 3 : PART.HOME
2 -> 0 : /START
3 -> 0 : /PART
`);

/** A selection that splits on a scale reading and returns from both branches. */
const Selection = read(`sequence Transfer
step 30 initial
30 -> 31 : GO
31 -> 38 : OK
31 -> 42 : /OK
38 -> 39 : A
42 -> 43 : B
39 -> 30 : /A
43 -> 30 : /B
`);

describe('GRAFCET layout', () => {
  it('runs the main sequence down the first lane', () => {
    const layout = layOutSequence(Conveyor.sequences[0]!);

    assert.deepEqual([0, 1, 2, 3].map((step) => layout.cells.get(step)), [0, 1, 2, 3].map((row) => ({ lane: 0, row })));
  });

  it('draws the plain cycle back to the initial step as a loop', () => {
    const layout = layOutSequence(Conveyor.sequences[0]!);

    assert.deepEqual(layout.routes.filter((route) => route.kind === 'loop').map((route) => route.transition.from), [[3]]);
  });

  it('draws a return that shares its step with another way out as a jump', () => {
    // A loop from a step that also continues downwards would have to cross the main line.
    const layout = layOutSequence(Conveyor.sequences[0]!);

    assert.deepEqual(layout.routes.filter((route) => route.kind === 'jump').map((route) => route.transition.from[0]), [1, 2]);
  });

  it('opens a lane to the right for each further branch of a selection', () => {
    const layout = layOutSequence(Selection.sequences[0]!);

    assert.equal(layout.cells.get(38)?.lane, 0);
    assert.equal(layout.cells.get(42)?.lane, 1);
    assert.equal(layout.cells.get(42)?.row, layout.cells.get(38)?.row);
  });

  it('places a convergence below the longest branch that reaches it', () => {
    const merge = read('sequence M\nstep 0 initial\n0 -> 1 : A\n0 -> 5 : /A\n1 -> 2 : B\n2 -> 9 : C\n5 -> 9 : D\n9 -> 0 : E\n');

    const layout = layOutSequence(merge.sequences[0]!);

    assert.equal(layout.cells.get(9)?.row, 3);
  });
});

describe('GRAFCET drawing', () => {
  it('doubles the frame of an initial step and only of it', () => {
    const svg = renderGrafcetSvg(Conveyor);

    assert.equal((svg.match(/<rect class="box" x="[\d.]+" y="[\d.]+" width="32"/g) ?? []).length, 1);
  });

  it('draws NOT as an overbar, as on paper', () => {
    const svg = renderGrafcetSvg(Conveyor);

    assert.match(svg, /<tspan text-decoration="overline">START<\/tspan>/);
  });

  it('draws an unconfirmed reading in the warning style, keeping its question mark', () => {
    const svg = renderGrafcetSvg(read('sequence S\nstep 0 initial\n0 -> 1 : ?/OK\n1 -> 0 : OK\n'));

    assert.match(svg, /<tspan class="uncertain" text-decoration="overline">OK\?<\/tspan>/);
  });

  it('names the target of a jump', () => {
    assert.match(renderGrafcetSvg(Conveyor), />X0<\/text>/);
  });

  it('escapes names so a title cannot break the page', () => {
    const page = renderGrafcetPage({ title: 'A <b> & "C"', sequences: Conveyor.sequences }, [], 'test');

    assert.ok(page.includes('A &lt;b&gt; &amp;'));
    assert.ok(!page.includes('<b>'));
  });

  it('shows every finding on the page', () => {
    const page = renderGrafcetPage(Conveyor, [{ severity: 'error', where: 'Conveyor', message: 'something wrong' }], 'test');

    assert.match(page, /<b>Error<\/b> · Conveyor — something wrong/);
  });
});
