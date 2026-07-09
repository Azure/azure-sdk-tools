// @ts-check
/**
 * app.mjs — dashboard orchestration.
 *
 * Flow: fetch data/manifest.json (relative) -> fetch each data/<file> ->
 * filter via isValidRun -> aggregate -> render trends + per-run table.
 * Relative URLs only, so it works locally and under a Pages subpath.
 */
import {
  aggregate,
  isValidRun,
  perRunHeadline,
  HEADLINE_RATE_KEYS,
} from "./aggregate.mjs";
import { lineChart, barChart, destroyCharts } from "./charts.mjs";

/** Rates rendered as percentages (0..1 ratios). Others shown as raw numbers. */
const PERCENT_RATES = new Set([
  "missRate",
  "ccrCoverage",
  "bugFixPrRate",
  "addressedRate",
  "rejectedRate",
  "ignoredRate",
  "criticalCatchRate",
]);

/** Human-friendly labels for the rate keys. */
const RATE_LABELS = {
  missRate: "Miss rate",
  ccrCoverage: "CCR coverage",
  bugFixPrRate: "Bug-fix PR rate",
  addressedRate: "Addressed rate",
  rejectedRate: "Rejected rate",
  ignoredRate: "Ignored rate",
  criticalCatchRate: "Critical catch rate",
  humanCommentsPerPr: "Human comments / PR",
  prCycleTime: "PR cycle time (h)",
  iterationsPerPr: "Iterations / PR",
};

/**
 * Plain-language explanations shown in the hoverable info icon on each column.
 * Keyed by rate key, plus a few table-structure columns.
 */
const COLUMN_TIPS = {
  repo: "The repository whose PRs were mined for this run.",
  windowEnd:
    "End of the count-based sampling window for this run. Each run is one window (a data point), so read metrics as a trend across runs.",
  prCount: "Number of PRs evaluated in this window.",
  missRate:
    "Of the locally-detectable substantive human asks CCR had a real chance to catch, the share CCR did NOT raise. Lower is better.",
  ccrCoverage:
    "Share of eligible PRs (post-enablement, non-bot) that received any CCR review. Frames every other quality number. Higher is better.",
  bugFixPrRate:
    "Share of merged PRs classified as bug-fix. A proxy for escaped bugs, not proof. Read next to miss rate; lower/stable is better.",
  addressedRate:
    "Share of CCR comments the author acted on (changed the code at those lines). Higher is better.",
  rejectedRate:
    "Share of CCR comments the author explicitly declined (by design / with a reason).",
  ignoredRate:
    "Share of CCR comments with no change and no engagement. A high value on critical comments is a real problem.",
  criticalCatchRate:
    "Share of critical-severity human asks that CCR also raised. Higher is better.",
  humanCommentsPerPr:
    "Average distinct substantive human review asks per PR. Lower suggests less human review burden. Read the trend within one repo, not across repos.",
  prCycleTime:
    "Median hours from PR open to merge. Contextual only: noisy and confounded (PR size, CI, reviewer availability).",
  iterationsPerPr:
    "Median commits after the first review event. Contextual only: noisy and confounded.",
};

/** @type {any[]} All valid runs loaded from data/. */
let ALL_RUNS = [];
/** @type {Set<string>} Currently-selected repos. */
let SELECTED_REPOS = new Set();
/** @type {number} Count of files skipped during load (invalid / unreadable). */
let TOTAL_SKIPPED = 0;

/**
 * @param {string} id
 * @returns {HTMLElement}
 */
function el(id) {
  const node = document.getElementById(id);
  if (!node) throw new Error(`missing element #${id}`);
  return node;
}

/**
 * @param {number|null} value
 * @param {string} key
 * @returns {string}
 */
function fmt(value, key) {
  if (value == null) return "n/a";
  if (PERCENT_RATES.has(key)) return `${(value * 100).toFixed(1)}%`;
  return String(Math.round(value * 100) / 100);
}

/** @param {string} generatedAt */
function dateLabel(generatedAt) {
  return generatedAt.slice(0, 10);
}

/** @param {string} repo */
function repoShort(repo) {
  const parts = repo.split("/");
  return parts[parts.length - 1] ?? repo;
}

/**
 * Escape a string for safe use inside an HTML attribute value.
 * @param {string} s
 */
function escapeAttr(s) {
  return s
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

/**
 * Build a table header label with a hoverable info icon explaining the column.
 * The icon uses a CSS tooltip (data-tip) and a native title as a fallback.
 * @param {string} label
 * @param {string|undefined} tip
 */
function th(label, tip) {
  if (!tip) return label;
  const t = escapeAttr(tip);
  return `${label}<span class="info" tabindex="0" role="img" aria-label="${t}" data-tip="${t}">i</span>`;
}

/**
 * Wire up a single shared tooltip element for all `.info` icons. Positioned with
 * fixed coordinates relative to the hovered icon so it is never clipped by the
 * scrollable table container. Idempotent-safe: called once from main().
 */
function setupTooltips() {
  const tip = document.createElement("div");
  tip.id = "tooltip";
  tip.setAttribute("role", "tooltip");
  document.body.appendChild(tip);

  /** @param {EventTarget|null} target */
  function infoFrom(target) {
    if (!(target instanceof HTMLElement)) return null;
    return target.classList.contains("info") ? target : null;
  }

  /** @param {HTMLElement} icon */
  function show(icon) {
    const text = icon.getAttribute("data-tip");
    if (!text) return;
    tip.textContent = text;
    tip.classList.add("visible");
    const r = icon.getBoundingClientRect();
    // Measure after content is set.
    const tw = tip.offsetWidth;
    const th_ = tip.offsetHeight;
    const margin = 8;
    let left = r.left;
    if (left + tw + margin > window.innerWidth) {
      left = window.innerWidth - tw - margin;
    }
    if (left < margin) left = margin;
    let top = r.bottom + 6;
    if (top + th_ + margin > window.innerHeight) {
      top = r.top - th_ - 6; // flip above if no room below
    }
    tip.style.left = `${String(Math.round(left))}px`;
    tip.style.top = `${String(Math.round(top))}px`;
  }

  function hide() {
    tip.classList.remove("visible");
  }

  document.addEventListener("mouseover", (e) => {
    const icon = infoFrom(e.target);
    if (icon) show(icon);
  });
  document.addEventListener("mouseout", (e) => {
    if (infoFrom(e.target)) hide();
  });
  document.addEventListener("focusin", (e) => {
    const icon = infoFrom(e.target);
    if (icon) show(icon);
  });
  document.addEventListener("focusout", (e) => {
    if (infoFrom(e.target)) hide();
  });
}

/** Load manifest + all listed run files. Skips invalid files (does not abort). */
async function loadRuns() {
  const manifestRes = await fetch("data/manifest.json");
  if (!manifestRes.ok) {
    throw new Error(
      `failed to fetch data/manifest.json (${manifestRes.status})`,
    );
  }
  const manifest = await manifestRes.json();
  /** @type {string[]} */
  const files = Array.isArray(manifest.runs) ? manifest.runs : [];

  const runs = [];
  let skipped = 0;
  for (const file of files) {
    try {
      const res = await fetch(`data/${file}`);
      if (!res.ok) {
        skipped += 1;
        continue;
      }
      const obj = await res.json();
      if (isValidRun(obj)) runs.push(obj);
      else skipped += 1;
    } catch {
      skipped += 1;
    }
  }
  return { runs, skipped, scanned: files.length };
}

/**
 * Build the sorted union of date labels across trend points.
 * @param {{generatedAt: string}[]} points
 * @returns {string[]}
 */
function unionDateLabels(points) {
  const set = new Set(points.map((p) => dateLabel(p.generatedAt)));
  return [...set].sort();
}

/**
 * Group trend points into one series per repo, aligned to `labels`.
 * @param {import("./aggregate.mjs").TrendPoint[]} points
 * @param {string[]} labels
 */
function seriesByRepo(points, labels) {
  /** @type {Map<string, Map<string, number|null>>} */
  const byRepo = new Map();
  for (const p of points) {
    let m = byRepo.get(p.repo);
    if (!m) {
      m = new Map();
      byRepo.set(p.repo, m);
    }
    m.set(dateLabel(p.generatedAt), p.value);
  }
  return [...byRepo.entries()]
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([repo, m]) => ({
      label: repoShort(repo),
      data: labels.map((l) => (m.has(l) ? (m.get(l) ?? null) : null)),
    }));
}

/** Render everything for the currently-selected repos. */
function render() {
  destroyCharts();
  const runs = ALL_RUNS.filter((r) => SELECTED_REPOS.has(r.run.repo));
  const agg = aggregate(runs, 0, runs.length);

  // Summary line
  const span =
    agg.dateSpan.earliest && agg.dateSpan.latest
      ? `${agg.dateSpan.earliest.slice(0, 10)} … ${agg.dateSpan.latest.slice(0, 10)}`
      : "n/a";
  el("summary").textContent =
    `Runs kept: ${String(agg.runsKept)} · skipped: ${String(TOTAL_SKIPPED)} · ` +
    `repos shown: ${String(SELECTED_REPOS.size)} · span: ${span}`;

  // Miss rate over time (one line per repo)
  const missLabels = unionDateLabels(agg.missRateOverTime);
  lineChart(
    /** @type {HTMLCanvasElement} */ (el("chart-miss")),
    missLabels,
    seriesByRepo(agg.missRateOverTime, missLabels),
    { yLabel: "miss rate", yMax: 1 },
  );

  // Bug-fix PR rate over time (one line per repo)
  const bugLabels = unionDateLabels(agg.bugFixPrRateOverTime);
  lineChart(
    /** @type {HTMLCanvasElement} */ (el("chart-bugfix")),
    bugLabels,
    seriesByRepo(agg.bugFixPrRateOverTime, bugLabels),
    { yLabel: "bug-fix PR rate", yMax: 1 },
  );

  // Addressed rate by severity (one line per severity) over the filtered runs.
  renderSeverityChart(agg.addressedRateBySeverityOverTime);

  // Bug-fix rate by repo (latest run per repo)
  barChart(
    /** @type {HTMLCanvasElement} */ (el("chart-byrepo")),
    agg.bugFixPrRateByRepo.map((r) => ({
      label: repoShort(r.repo),
      value: r.value,
    })),
    { xLabel: "bug-fix PR rate" },
  );

  renderTable(runs);
}

/**
 * @param {import("./aggregate.mjs").SeverityTrendPoint[]} points
 */
function renderSeverityChart(points) {
  const multi = SELECTED_REPOS.size > 1;
  const sorted = [...points].sort(
    (a, b) => Date.parse(a.generatedAt) - Date.parse(b.generatedAt),
  );
  const labels = sorted.map((p) =>
    multi
      ? `${dateLabel(p.generatedAt)} ${repoShort(p.repo)}`
      : dateLabel(p.generatedAt),
  );
  const severities = ["critical", "substantive", "nit"];
  const series = severities.map((sev) => ({
    label: sev,
    data: sorted.map((p) =>
      Object.prototype.hasOwnProperty.call(p.bySeverity, sev)
        ? (p.bySeverity[sev] ?? null)
        : null,
    ),
  }));
  lineChart(
    /** @type {HTMLCanvasElement} */ (el("chart-severity")),
    labels,
    series,
    { yLabel: "addressed rate", yMax: 1, colorBySeverity: true },
  );
}

/**
 * Render the per-run headline metrics table (null -> "n/a", low-confidence flagged).
 * @param {any[]} runs
 */
function renderTable(runs) {
  const headlines = runs
    .map(perRunHeadline)
    .sort(
      (a, b) =>
        a.repo.localeCompare(b.repo) ||
        a.generatedAt.localeCompare(b.generatedAt),
    );

  const head =
    `<tr><th>${th("Repo", COLUMN_TIPS.repo)}</th>` +
    `<th>${th("Window end", COLUMN_TIPS.windowEnd)}</th>` +
    `<th>${th("PRs", COLUMN_TIPS.prCount)}</th>` +
    HEADLINE_RATE_KEYS.map(
      (k) => `<th>${th(RATE_LABELS[k] ?? k, COLUMN_TIPS[k])}</th>`,
    ).join("") +
    "<th>Notes</th></tr>";

  const rows = headlines
    .map((h) => {
      const byKey = new Map(h.rates.map((r) => [r.key, r]));
      const cells = HEADLINE_RATE_KEYS.map((k) => {
        const r = byKey.get(k);
        if (!r) return `<td class="na">—</td>`;
        const cls = r.value == null ? "na" : "";
        const badge = r.lowConfidence
          ? ` <span class="lc" title="low confidence">lc</span>`
          : "";
        return `<td class="${cls}">${fmt(r.value, k)}${badge}</td>`;
      }).join("");
      const notes = [];
      if (h.hasExperiment) notes.push("experiment");
      if (h.coverageWarnings.length) {
        notes.push(
          `<span class="warn" title="${h.coverageWarnings
            .map((w) => w.replace(/"/g, "&quot;"))
            .join(
              "&#10;",
            )}">${String(h.coverageWarnings.length)} warning(s)</span>`,
        );
      }
      const prCell = h.prCount == null ? "—" : String(h.prCount);
      return (
        `<tr><td>${repoShort(h.repo)}</td><td>${h.windowEnd}</td>` +
        `<td>${prCell}</td>` +
        cells +
        `<td>${notes.join(", ") || "—"}</td></tr>`
      );
    })
    .join("");

  el("table").innerHTML = `<table>${head}${rows}</table>`;
}

/** Build the repo filter checkboxes. */
function buildRepoFilter() {
  const repos = [...new Set(ALL_RUNS.map((r) => r.run.repo))].sort();
  SELECTED_REPOS = new Set(repos);
  const container = el("repo-filter");
  container.innerHTML = "";
  for (const repo of repos) {
    const label = document.createElement("label");
    const cb = document.createElement("input");
    cb.type = "checkbox";
    cb.checked = true;
    cb.value = repo;
    cb.addEventListener("change", () => {
      if (cb.checked) SELECTED_REPOS.add(repo);
      else SELECTED_REPOS.delete(repo);
      render();
    });
    label.append(cb, document.createTextNode(` ${repo}`));
    container.append(label);
  }
}

async function main() {
  try {
    const { runs, skipped } = await loadRuns();
    ALL_RUNS = runs;
    TOTAL_SKIPPED = skipped;
    if (runs.length === 0) {
      el("summary").textContent =
        "No valid runs found in data/. Add run-<id>.json files and list them in data/manifest.json.";
      return;
    }
    buildRepoFilter();
    render();
    setupTooltips();
  } catch (err) {
    el("summary").textContent = `Error loading data: ${
      err instanceof Error ? err.message : String(err)
    }`;
  }
}

document.addEventListener("DOMContentLoaded", () => {
  void main();
});
