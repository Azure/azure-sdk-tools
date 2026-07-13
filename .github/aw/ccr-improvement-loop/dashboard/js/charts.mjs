// @ts-check
/**
 * charts.mjs — thin wrappers over the globally-loaded Chart.js (vendored UMD).
 *
 * `index.html` loads `vendor/chart.umd.min.js` as a classic script first, exposing
 * the `Chart` global; this module references it. No npm imports.
 */

/** Stable per-repo line colors (kept small + deterministic). */
const REPO_COLORS = [
  "#0969da",
  "#cf222e",
  "#1a7f37",
  "#8250df",
  "#bf3989",
  "#9a6700",
];

/** Fixed severity colors for the addressed-by-severity chart. */
const SEVERITY_COLORS = {
  critical: "#cf222e",
  substantive: "#0969da",
  nit: "#6e7781",
};

/** Fixed colors for CCR comment outcomes (grouped-bar charts). */
export const OUTCOME_COLORS = {
  addressed: "#1a7f37",
  rejected: "#9a6700",
  ignored: "#6e7781",
};

/** @type {Chart[]} */
const liveCharts = [];

/** Destroy any charts from a previous render so re-render is clean. */
export function destroyCharts() {
  while (liveCharts.length) {
    const c = liveCharts.pop();
    if (c) c.destroy();
  }
}

/**
 * @param {number} i
 * @returns {string}
 */
function repoColor(i) {
  return REPO_COLORS[i % REPO_COLORS.length];
}

/**
 * Render a multi-series line chart over a shared category x-axis of date labels.
 * Using a category axis (not a time axis) avoids needing a Chart.js date adapter,
 * keeping the vendored footprint to a single file. Each series' `data` array is
 * aligned by index to `labels`; use `null` for missing points.
 * `spanGaps: false` so a `null` value renders as a GAP, never as 0.
 *
 * @param {HTMLCanvasElement} canvas
 * @param {string[]} labels
 * @param {{label: string, data: Array<number|null>}[]} series
 * @param {{yLabel?: string, yMax?: number, colorBySeverity?: boolean}} [opts]
 */
export function lineChart(canvas, labels, series, opts = {}) {
  const datasets = series.map((s, i) => {
    const color = opts.colorBySeverity
      ? (SEVERITY_COLORS[
          /** @type {keyof typeof SEVERITY_COLORS} */ (s.label)
        ] ?? repoColor(i))
      : repoColor(i);
    return {
      label: s.label,
      data: s.data,
      borderColor: color,
      backgroundColor: color,
      spanGaps: false,
      tension: 0.2,
      pointRadius: 3,
    };
  });

  const chart = new Chart(canvas, {
    type: "line",
    data: { labels, datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "nearest", intersect: false },
      scales: {
        x: {
          title: { display: true, text: "run date (generatedAt)" },
        },
        y: {
          beginAtZero: true,
          suggestedMax: opts.yMax,
          title: { display: !!opts.yLabel, text: opts.yLabel ?? "" },
        },
      },
      plugins: {
        legend: { position: "bottom" },
      },
    },
  });
  liveCharts.push(chart);
  return chart;
}

/**
 * Render a vertical grouped bar chart over a shared category x-axis. Each series
 * is one bar-group color; `data` aligns by index to `categories`. A `null` value
 * is dropped (Chart.js renders a gap, never a 0 bar). Legend shows when there is
 * more than one series.
 *
 * @param {HTMLCanvasElement} canvas
 * @param {string[]} categories
 * @param {{label: string, data: Array<number|null>, color?: string}[]} series
 * @param {{yLabel?: string, yMax?: number, xLabel?: string}} [opts]
 */
export function groupedBar(canvas, categories, series, opts = {}) {
  const datasets = series.map((s, i) => ({
    label: s.label,
    data: s.data,
    backgroundColor: s.color ?? repoColor(i),
  }));

  const chart = new Chart(canvas, {
    type: "bar",
    data: { labels: categories, datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          title: { display: !!opts.xLabel, text: opts.xLabel ?? "" },
        },
        y: {
          beginAtZero: true,
          suggestedMax: opts.yMax,
          title: { display: !!opts.yLabel, text: opts.yLabel ?? "" },
        },
      },
      plugins: {
        legend: { display: series.length > 1, position: "bottom" },
      },
    },
  });
  liveCharts.push(chart);
  return chart;
}

/**
 * Render a simple horizontal bar chart (one bar per category). Null values are
 * dropped (not drawn as 0).
 *
 * @param {HTMLCanvasElement} canvas
 * @param {{label: string, value: number|null}[]} rows
 * @param {{xLabel?: string}} [opts]
 */
export function barChart(canvas, rows, opts = {}) {
  const chart = new Chart(canvas, {
    type: "bar",
    data: {
      labels: rows.map((r) => r.label),
      datasets: [
        {
          label: opts.xLabel ?? "",
          data: rows.map((r) => r.value),
          backgroundColor: rows.map((_, i) => repoColor(i)),
        },
      ],
    },
    options: {
      indexAxis: "y",
      responsive: true,
      maintainAspectRatio: false,
      scales: { x: { beginAtZero: true } },
      plugins: { legend: { display: false } },
    },
  });
  liveCharts.push(chart);
  return chart;
}
