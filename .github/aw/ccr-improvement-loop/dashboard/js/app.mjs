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
  poolSlices,
  PR_TYPE_ORDER,
  SEVERITY_ORDER,
} from "./aggregate.mjs";
import {
  lineChart,
  barChart,
  groupedBar,
  destroyCharts,
  OUTCOME_COLORS,
} from "./charts.mjs";

/** Rates rendered as percentages (0..1 ratios). Others shown as raw numbers. */
const PERCENT_RATES = new Set([
  "ccrRecallRate",
  "ccrCoverage",
  "bugFixPrRate",
  "addressedRate",
  "rejectedRate",
  "ignoredRate",
  "criticalCatchRate",
]);

/** Human-friendly labels for the rate keys. */
const RATE_LABELS = {
  ccrRecallRate: "CCR catch rate",
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
    "End of the settled time window this run measured. Each run is one window (a data point), and trends are plotted by this date — so read metrics as a trend across windows.",
  prCount: "Number of PRs evaluated in this window.",
  ccrRecallRate:
    "Of the substantive, diff-visible issues human reviewers raised on CCR-reviewed PRs, the share CCR independently flagged the same concern. CCR's recall against human reviewers. Higher is better.",
  ccrCoverage:
    "Share of eligible PRs (post-enablement, non-bot) that received any CCR review. Frames every other quality number. Higher is better.",
  bugFixPrRate:
    "Share of merged PRs classified as bug-fix. A proxy for escaped bugs, not proof. Read next to CCR catch rate; lower/stable is better.",
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

/** @param {string} isoDate — an ISO date/time; only the YYYY-MM-DD prefix is kept. */
function dateLabel(isoDate) {
  return isoDate.slice(0, 10);
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
 * Build the sorted union of window-end date labels across trend points.
 * @param {{windowEnd: string}[]} points
 * @returns {string[]}
 */
function unionDateLabels(points) {
  const set = new Set(points.map((p) => dateLabel(p.windowEnd)));
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
    m.set(dateLabel(p.windowEnd), p.value);
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

  // Per-rate trend charts (one chart per rate, one line per repo).
  renderRateTrends(agg);

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

  // Slice breakdowns pooled across the filtered runs (denominator-weighted).
  renderSliceCharts(runs);

  renderTable(runs);

  // Drill-down examples: specific PRs / review threads behind the aggregates.
  renderNotableExamples(runs);
  renderThemeCards(runs);
}

/**
 * Render the four pooled slice-breakdown charts. Each pools per-slice counts
 * across all filtered runs (denominator-weighted), so small per-run cells add up
 * to usable samples. Bars whose pooled denominator is 0 are dropped, and a chart
 * with no data at all is left blank.
 *
 * @param {any[]} runs
 */
function renderSliceCharts(runs) {
  // 1. CCR comment outcomes (addressed / rejected / ignored) by severity.
  {
    const addressed = poolSlices(
      runs,
      "addressedRate",
      "severity",
      SEVERITY_ORDER,
    );
    const rejected = poolSlices(
      runs,
      "rejectedRate",
      "severity",
      SEVERITY_ORDER,
    );
    const ignored = poolSlices(runs, "ignoredRate", "severity", SEVERITY_ORDER);
    const cats = SEVERITY_ORDER.filter((s) =>
      addressed.some((r) => r.category === s && r.denominator > 0),
    );
    const pick = (rows, cat) =>
      rows.find((r) => r.category === cat)?.value ?? null;
    groupedBar(
      /** @type {HTMLCanvasElement} */ (el("chart-outcome-severity")),
      cats,
      [
        {
          label: "addressed",
          data: cats.map((c) => pick(addressed, c)),
          color: OUTCOME_COLORS.addressed,
        },
        {
          label: "rejected",
          data: cats.map((c) => pick(rejected, c)),
          color: OUTCOME_COLORS.rejected,
        },
        {
          label: "ignored",
          data: cats.map((c) => pick(ignored, c)),
          color: OUTCOME_COLORS.ignored,
        },
      ],
      { yLabel: "share of CCR comments", yMax: 1 },
    );
  }

  // 2. Addressed vs ignored rate by PR type.
  {
    const addressed = poolSlices(
      runs,
      "addressedRate",
      "prType",
      PR_TYPE_ORDER,
    );
    const ignored = poolSlices(runs, "ignoredRate", "prType", PR_TYPE_ORDER);
    const cats = addressed
      .filter((r) => r.denominator > 0)
      .map((r) => r.category);
    const pick = (rows, cat) =>
      rows.find((r) => r.category === cat)?.value ?? null;
    groupedBar(
      /** @type {HTMLCanvasElement} */ (el("chart-outcome-prtype")),
      cats,
      [
        {
          label: "addressed",
          data: cats.map((c) => pick(addressed, c)),
          color: OUTCOME_COLORS.addressed,
        },
        {
          label: "ignored",
          data: cats.map((c) => pick(ignored, c)),
          color: OUTCOME_COLORS.ignored,
        },
      ],
      { yLabel: "share of CCR comments", yMax: 1 },
    );
  }

  // 3. Human asks per PR by PR type (an average count, not a ratio).
  {
    const pooled = poolSlices(
      runs,
      "humanCommentsPerPr",
      "prType",
      PR_TYPE_ORDER,
    ).filter((r) => r.denominator > 0);
    groupedBar(
      /** @type {HTMLCanvasElement} */ (el("chart-humancomments-prtype")),
      pooled.map((r) => r.category),
      [{ label: "human asks / PR", data: pooled.map((r) => r.value) }],
      { yLabel: "asks per PR" },
    );
  }

  // 4. CCR catch rate (recall vs reviewers) by PR type.
  {
    const pooled = poolSlices(
      runs,
      "ccrRecallRate",
      "prType",
      PR_TYPE_ORDER,
    ).filter((r) => r.denominator > 0);
    groupedBar(
      /** @type {HTMLCanvasElement} */ (el("chart-recall-prtype")),
      pooled.map((r) => r.category),
      [{ label: "CCR catch rate", data: pooled.map((r) => r.value) }],
      { yLabel: "catch rate", yMax: 1 },
    );
  }
}

/**
 * Render one trend line chart per rate key into #rate-trends. Percentage rates
 * are capped to a 0..1 y-axis; raw-number rates (comments/PR, cycle time,
 * iterations) auto-scale. Rates with no measured value in any shown run are
 * skipped so empty charts don't add noise.
 *
 * @param {import("./aggregate.mjs").Aggregation} agg
 */
function renderRateTrends(agg) {
  const grid = el("rate-trends");
  grid.innerHTML = "";
  for (const key of HEADLINE_RATE_KEYS) {
    const points = agg.ratesOverTime[key] ?? [];
    if (!points.some((p) => p.value != null)) continue;

    const box = document.createElement("div");
    box.className = "trend-item";
    const title = document.createElement("h3");
    title.textContent = RATE_LABELS[key] ?? key;
    const chartBox = document.createElement("div");
    chartBox.className = "chart-box";
    const canvas = document.createElement("canvas");
    chartBox.append(canvas);
    box.append(title, chartBox);
    const tip = COLUMN_TIPS[key];
    if (tip) {
      const note = document.createElement("p");
      note.className = "chart-note";
      note.textContent = tip;
      box.append(note);
    }
    grid.append(box);

    const labels = unionDateLabels(points);
    lineChart(canvas, labels, seriesByRepo(points, labels), {
      yLabel: RATE_LABELS[key] ?? key,
      yMax: PERCENT_RATES.has(key) ? 1 : undefined,
    });
  }
}

/**
 * @param {import("./aggregate.mjs").SeverityTrendPoint[]} points
 */
function renderSeverityChart(points) {
  const multi = SELECTED_REPOS.size > 1;
  const sorted = [...points].sort((a, b) =>
    a.windowEnd.localeCompare(b.windowEnd),
  );
  const labels = sorted.map((p) =>
    multi
      ? `${dateLabel(p.windowEnd)} ${repoShort(p.repo)}`
      : dateLabel(p.windowEnd),
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
        a.repo.localeCompare(b.repo) || a.windowEnd.localeCompare(b.windowEnd),
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

/**
 * Collect PRs where CCR missed a human-raised concern across the given runs.
 * A "missed ask" = a substantive, diff-detectable human ask on a CCR-reviewed
 * PR that was judged and that CCR did NOT independently raise
 * (ccrAddressedConcern === false). Grouped by PR, sorted by missed count (desc).
 *
 * @param {any[]} runs
 * @returns {{repo: string, windowEnd: string, prNumber: number, prUrl: string,
 *   prTitle: string, author: string|null, prType: string|null,
 *   missed: {url: string, path: string, lineStart: number, category: string|null,
 *   authorLogin: string|null}[]}[]}
 */
function collectMissedExamples(runs) {
  /** @type {Map<string, any>} */
  const groups = new Map();
  for (const run of runs) {
    const repo = run.run.repo;
    const windowEnd = run.run.windowEnd ? dateLabel(run.run.windowEnd) : "";
    /** @type {Map<number, any>} */
    const prById = new Map(
      (run.prs ?? []).map((/** @type {any} */ p) => [p.number, p]),
    );
    for (const c of run.comments ?? []) {
      if (
        c.authorKind === "human" &&
        c.kind === "ask" &&
        c.isSubstantive === true &&
        c.diffDetectable === true &&
        !c.pathExcluded &&
        c.ccrAddressedConcern === false
      ) {
        const key = `${repo}#${String(c.pr)}`;
        let g = groups.get(key);
        if (!g) {
          const pr = prById.get(c.pr);
          g = {
            repo,
            windowEnd,
            prNumber: c.pr,
            prUrl: pr?.url ?? `https://github.com/${repo}/pull/${String(c.pr)}`,
            prTitle: pr?.title ?? `PR #${String(c.pr)}`,
            author: pr?.author ?? null,
            prType: pr?.prType ?? null,
            missed: [],
          };
          groups.set(key, g);
        }
        g.missed.push({
          url: c.url,
          path: c.path,
          lineStart: c.lineStart,
          category: c.category,
          authorLogin: c.authorLogin,
        });
      }
    }
  }
  return [...groups.values()].sort((a, b) => b.missed.length - a.missed.length);
}

/**
 * Render the "Notable examples" drill-down: top PRs (by missed-ask count) where
 * CCR did not independently raise a concern a human reviewer did. Each PR is an
 * expandable row deep-linking to the exact GitHub review threads. Text comes
 * from structured fields only (comment bodies are not stored in the run JSON).
 *
 * @param {any[]} runs
 */
function renderNotableExamples(runs) {
  const container = el("examples");
  container.innerHTML = "";
  const groups = collectMissedExamples(runs);
  if (groups.length === 0) {
    const p = document.createElement("p");
    p.className = "hint";
    p.textContent =
      "No missed human asks in the selected runs (nothing where a human raised a diff-visible concern CCR didn't).";
    container.append(p);
    return;
  }

  const LIMIT = 15;
  const shown = groups.slice(0, LIMIT);
  const totalMissed = groups.reduce((n, g) => n + g.missed.length, 0);
  const intro = document.createElement("p");
  intro.className = "hint";
  intro.textContent =
    `${String(groups.length)} PR(s) where CCR missed a human-raised concern ` +
    `(${String(totalMissed)} asks total). Showing the top ${String(
      shown.length,
    )} by missed count — expand a PR to open the exact review threads.`;
  container.append(intro);

  for (const g of shown) {
    const det = document.createElement("details");
    det.className = "example";

    const sum = document.createElement("summary");
    const count = document.createElement("span");
    count.className = "example-count";
    count.textContent = `${String(g.missed.length)}×`;
    const meta = document.createElement("span");
    meta.className = "example-meta";
    meta.textContent =
      ` ${repoShort(g.repo)} · ${g.windowEnd}` +
      `${g.prType ? ` · ${g.prType}` : ""} · `;
    const link = document.createElement("a");
    link.href = g.prUrl;
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.textContent = `#${String(g.prNumber)} ${g.prTitle}`;
    sum.append(count, meta, link);
    det.append(sum);

    const ul = document.createElement("ul");
    ul.className = "example-list";
    for (const m of g.missed) {
      const li = document.createElement("li");
      const tag = document.createElement("span");
      tag.className = "tag";
      tag.textContent = m.category ?? "uncategorized";
      const a = document.createElement("a");
      a.href = m.url;
      a.target = "_blank";
      a.rel = "noopener noreferrer";
      a.textContent = `${m.path}:${String(m.lineStart)}`;
      li.append(tag, document.createTextNode(" "), a);
      if (m.authorLogin) {
        const who = document.createElement("span");
        who.className = "example-who";
        who.textContent = ` — ${m.authorLogin}`;
        li.append(who);
      }
      ul.append(li);
    }
    det.append(ul);
    container.append(det);
  }
}

/**
 * Render "Recurring themes": the promoted issue clusters the loop identified in
 * each selected window, grouped by run, each theme card deep-linking to the PRs
 * that sourced it. Themes are per-run and PR numbers are repo-scoped, so runs
 * are shown as separate collapsible groups.
 *
 * @param {any[]} runs
 */
function renderThemeCards(runs) {
  const container = el("themes");
  container.innerHTML = "";
  const withThemes = [...runs]
    .filter(
      (r) =>
        Array.isArray(r.themes) &&
        r.themes.some((/** @type {any} */ t) => t.promoted),
    )
    .sort(
      (a, b) =>
        a.run.repo.localeCompare(b.run.repo) ||
        String(a.run.windowEnd).localeCompare(String(b.run.windowEnd)),
    );

  if (withThemes.length === 0) {
    const p = document.createElement("p");
    p.className = "hint";
    p.textContent = "No promoted themes in the selected runs.";
    container.append(p);
    return;
  }

  for (const run of withThemes) {
    const repo = run.run.repo;
    const windowEnd = run.run.windowEnd ? dateLabel(run.run.windowEnd) : "";
    const promoted = run.themes
      .filter((/** @type {any} */ t) => t.promoted)
      .sort(
        (/** @type {any} */ a, /** @type {any} */ b) =>
          (b.priorityScore ?? 0) - (a.priorityScore ?? 0),
      );

    const det = document.createElement("details");
    det.className = "theme-run";
    const sum = document.createElement("summary");
    sum.textContent = `${repoShort(repo)} · ${windowEnd} — ${String(
      promoted.length,
    )} theme(s)`;
    det.append(sum);

    const grid = document.createElement("div");
    grid.className = "theme-grid";
    for (const t of promoted) {
      const card = document.createElement("div");
      card.className = "theme-card";
      const h = document.createElement("h4");
      h.textContent = t.label ?? "theme";
      const stats = document.createElement("p");
      stats.className = "theme-stats";
      stats.textContent =
        `${String(t.askCount ?? 0)} asks · ${String(t.gapCount ?? 0)} gaps · ` +
        `${String(t.distinctReviewers ?? 0)} reviewers · priority ${String(
          t.priorityScore ?? 0,
        )}${t.promotedVia ? ` · via ${t.promotedVia}` : ""}`;
      card.append(h, stats);

      const prs = Array.isArray(t.sourcePrs) ? t.sourcePrs : [];
      if (prs.length) {
        const chips = document.createElement("div");
        chips.className = "theme-prs";
        for (const n of prs) {
          const a = document.createElement("a");
          a.className = "chip";
          a.href = `https://github.com/${repo}/pull/${String(n)}`;
          a.target = "_blank";
          a.rel = "noopener noreferrer";
          a.textContent = `#${String(n)}`;
          chips.append(a);
        }
        card.append(chips);
      }
      grid.append(card);
    }
    det.append(grid);
    container.append(det);
  }
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
