/**
 * Wraps a drawn chart into one self-contained HTML page: the drawing, what the checks found, and
 * the chart in text notation so it can be corrected and read back.
 *
 * @remarks
 * No script and no external resource, so the page opens the same from a disk, an e-mail or a
 * classroom projector. Colours follow the reader's light or dark preference.
 */

import { renderGrafcetSvg } from './grafcetSvg.ts';
import { escape } from './grafcetSvgText.ts';
import { formatGrafcet } from './grafcetText.ts';
import type { Grafcet } from './grafcetModel.ts';

/** A finding from any of the checks, reduced to what the page shows. */
export interface PageFinding {
  readonly severity: 'error' | 'warning';
  readonly where: string;
  readonly message: string;
}

/** Builds the page. */
export function renderGrafcetPage(grafcet: Grafcet, findings: readonly PageFinding[], source: string): string {
  const errors = findings.filter((finding) => finding.severity === 'error').length;
  const summary = findings.length === 0 ? 'No findings: the chart passes every check.' : `${errors} error(s), ${findings.length - errors} warning(s).`;
  const items = findings.map((finding) => `<li class="${finding.severity}"><b>${finding.severity === 'error' ? 'Error' : 'Warning'}</b> · ${escape(finding.where)} — ${escape(finding.message)}</li>`).join('\n');

  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escape(grafcet.title)}</title>
<style>${PageStyle}</style>
</head>
<body>
<header><h1>${escape(grafcet.title)}</h1><p class="meta">GRAFCET · IEC 60848:2013 symbols · from ${escape(source)}</p></header>
<section class="chart">${renderGrafcetSvg(grafcet)}</section>
<section><h2>Checks</h2><p>${summary}</p><ul class="findings">${items}</ul></section>
<section><h2>Text notation</h2><pre>${escape(formatGrafcet(grafcet))}</pre></section>
</body>
</html>
`;
}

const PageStyle = `
:root{--grafcet-ink:#1d2430;--grafcet-paper:#ffffff;--grafcet-warn:#b45309;--page:#f6f7f9;--muted:#5b6472;--error:#b42318}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--grafcet-ink:#e6e9ef;--grafcet-paper:#161a21;--grafcet-warn:#f5a524;--page:#0f1216;--muted:#9aa3b2;--error:#ff6b5e}}
:root[data-theme="dark"]{--grafcet-ink:#e6e9ef;--grafcet-paper:#161a21;--grafcet-warn:#f5a524;--page:#0f1216;--muted:#9aa3b2;--error:#ff6b5e}
body{margin:0;padding:24px 16px;background:var(--page);color:var(--grafcet-ink);font-family:system-ui,sans-serif}
h1{font-size:22px;margin:0}h2{font-size:17px;margin:28px 0 8px}
.meta{color:var(--muted);margin:4px 0 16px}
.chart{overflow-x:auto;background:var(--grafcet-paper);border-radius:8px;padding:12px}
.findings{padding-left:18px}.findings li{margin:6px 0;line-height:1.4}
.findings .error b{color:var(--error)}.findings .warning b{color:var(--grafcet-warn)}
pre{background:var(--grafcet-paper);padding:12px;border-radius:8px;overflow-x:auto;font-size:13px}`;
