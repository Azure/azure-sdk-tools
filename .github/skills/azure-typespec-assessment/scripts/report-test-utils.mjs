import assert from "node:assert/strict";

export function reportSection(html, id) {
  const start = html.indexOf(`<section id="${id}">`);
  assert.notEqual(start, -1, `Missing report section: ${id}`);
  const end = html.indexOf("</section>", start);
  assert.ok(end > start, `Unclosed report section: ${id}`);
  return html.slice(start, end + "</section>".length);
}
