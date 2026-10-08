import { Chart, registerables } from "chart.js";
import {
  acceptSnapshot, aggregate, breakdown, filterLibraries, forRepository, history, loadIndex, mergeSnapshots, REPOSITORIES,
  sortLibraries, trendSeries,
  type Snapshot, type Library, type Filters, type SortKey, type Observation, type RepositoryName,
} from "./data.js";
import { inRange, isStale, loadReport, loadHistory, mergeObservations, type Report, type MonthCache } from "./report.js";

declare global {
  var customCodeMetricsSeed: unknown;
  var customCodeMetricsIndexUrl: unknown;
  var customCodeMetricsPreview: unknown;
}

Chart.register(...registerables);
Chart.defaults.font.family = '"Segoe UI", system-ui, sans-serif';
Chart.defaults.font.size = 12;
Chart.defaults.animation = false;

const integer = new Intl.NumberFormat("en-US");
const percent = (value: number | null) => value === null ? "N/A" : `${(value * 100).toFixed(2)}%`;
const categoryNames: Record<string, string> = { management: "Management", "data-plane": "Data-plane", provisioning: "Provisioning" };
const sortLabels: Record<SortKey, string> = {
  library: "Library", service: "Service", category: "Category",
  customRatio: "Custom percentage", customLines: "Custom lines", totalLines: "Total lines",
};
const sortKeys: readonly SortKey[] = ["library", "service", "category", "customRatio", "customLines", "totalLines"];
const byId = (id: string): HTMLElement => {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing dashboard element: ${id}.`);
  return element;
};
const select = (id: string): HTMLSelectElement => {
  const element = byId(id);
  if (!(element instanceof HTMLSelectElement)) throw new Error(`${id} is not a select.`);
  return element;
};
const input = (id: string): HTMLInputElement => {
  const element = byId(id);
  if (!(element instanceof HTMLInputElement)) throw new Error(`${id} is not an input.`);
  return element;
};
const canvas = (id: string): HTMLCanvasElement => {
  const element = byId(id);
  if (!(element instanceof HTMLCanvasElement)) throw new Error(`${id} is not a canvas.`);
  return element;
};
const text = (id: string, value: string) => { byId(id).textContent = value; };
const option = (value: string, label: string) => {
  const element = document.createElement("option");
  element.value = value;
  element.textContent = label;
  return element;
};
const cell = (row: HTMLTableRowElement, value: string) => {
  const element = row.insertCell();
  element.textContent = value;
  return element;
};
const colors = () => {
  const css = getComputedStyle(document.documentElement);
  return {
    custom: css.getPropertyValue("--custom").trim(),
    generated: css.getPropertyValue("--generated").trim(),
    text: getComputedStyle(document.body).color,
    grid: css.getPropertyValue("--border").trim(),
  };
};
let snapshots: Snapshot[] = [];
let selectedRepository: RepositoryName = REPOSITORIES[0].name;
let staticPreview = false;
let publishedHistory: Observation[] = [];
let report: Report | null = null;
let monthCache: MonthCache = new Map();
let loadedRange = "all";
let selectedId = "";
let detailId = "";
let sortKey: SortKey = "customLines";
let sortDescending = true;
let busy = false;
let breakdownChart: Chart<"bar"> | null = null;
let historyChart: Chart<"line", { x: number; y: number | null }[], number> | null = null;

function current(): Snapshot | undefined {
  return forRepository(snapshots, selectedRepository).find((snapshot) => snapshot.snapshotId === selectedId);
}
function filters(): Filters {
  return { category: select("category-filter").value, service: select("service-filter").value, search: input("library-search").value };
}
function setStatus(message: string, error = false): void {
  text("status", message);
  byId("status").classList.toggle("error", error);
  byId("status").setAttribute("role", error ? "alert" : "status");
  byId("status").hidden = !error && !busy;
}
async function perform(action: () => Promise<void>): Promise<void> {
  if (busy) return;
  busy = true;
  select("repository-select").disabled = true;
  byId("load-index").setAttribute("disabled", "");
  select("history-range").disabled = true;
  select("snapshot-select").disabled = true;
  try {
    await action();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    setStatus(`Could not load data: ${message} Existing observations were retained.`, true);
    console.error(message);
  } finally {
    busy = false;
    select("repository-select").disabled = false;
    byId("load-index").removeAttribute("disabled");
    select("history-range").disabled = false;
    select("snapshot-select").disabled = false;
    byId("status").hidden = !byId("status").classList.contains("error");
  }
}
function addSnapshots(incoming: Snapshot[], source: string): void {
  if (incoming.some((snapshot) => snapshot.repository.name !== selectedRepository)) {
    throw new Error(`Snapshot repository does not match the selected ${selectedRepository}.`);
  }
  const next = mergeSnapshots(snapshots, incoming);
  mergeObservations([...publishedHistory, ...next]);
  snapshots = next;
  selectedId = snapshots.at(-1)?.snapshotId || "";
  populateSnapshots();
  populateServices();
  populateHistoryScope();
  render();
  if (busy) setStatus(`${source}: ${snapshots.length} observations loaded.`);
}
function populateSnapshots(): void {
  const element = select("snapshot-select");
  element.replaceChildren(...forRepository(snapshots, selectedRepository).map((snapshot) => option(snapshot.snapshotId,
    `${new Date(snapshot.collectedAt).toISOString().replace("T", " ").slice(0, 19)} UTC - ${snapshot.repository.commit.slice(0, 8)}${snapshot.repository.isDirty ? " (dirty)" : ""}`)));
  element.value = selectedId;
}
function populateServices(): void {
  const element = select("service-filter");
  const value = element.value;
  element.replaceChildren(option("", "All services"),
    ...[...new Set(forRepository([...publishedHistory, ...snapshots], selectedRepository).flatMap((observation) =>
      observation.libraries.map((library) => library.service)))].sort().map((service) => option(service, service)));
  element.value = [...element.options].some((item) => item.value === value) ? value : "";
}
function populateHistoryScope(): void {
  const element = select("history-scope");
  const value = element.value;
  const ids = [...new Set(forRepository([...publishedHistory, ...snapshots], selectedRepository)
    .flatMap((snapshot) => snapshot.libraries.map((library) => library.library)))].sort();
  element.replaceChildren(option("", "Filtered portfolio"), ...ids.map((id) => option(id, id)));
  element.value = ids.includes(value) ? value : "";
}
function render(): void {
  const snapshot = current();
  const repository = REPOSITORIES.find((candidate) => candidate.name === selectedRepository);
  if (!repository) throw new Error("Unknown selected repository.");
  document.title = `${repository.language} - Azure SDK Metrics`;
  byId("measurements").hidden = !snapshot;
  byId("repository-state").hidden = !!snapshot;
  byId("repository-empty").hidden = !!snapshot;
  text("repository-state", repository.implemented ?
    `No observations loaded for ${repository.name}. Use a validated build-time baseline or a published index.` :
    `No observations collected for ${repository.name}. The ${repository.language} collector is not implemented. Measurements from other repositories are not shown.`);
  byId("index-loader").hidden = !repository.implemented;
  byId("data-context").hidden = !repository.implemented;
  byId("status").hidden = !busy && !byId("status").classList.contains("error");
  if (!snapshot) {
    renderFreshness();
    return;
  }
  const libraries = filterLibraries(snapshot?.libraries || [], filters());
  renderOverview(snapshot, libraries);
  renderBreakdown(libraries);
  renderHistory(snapshot);
  renderLibraries(libraries);
  renderDetail(snapshot);
  renderFreshness();
}
function renderFreshness(): void {
  const activeReport = report?.latest.repository.name === selectedRepository ? report : null;
  byId("freshness").hidden = !activeReport;
  text("deployment-info", activeReport ? "Published snapshots; history uses the selected observation's UTC date." :
    staticPreview ? "Embedded baseline. No automatic feed." : "Build-time observations. No automatic feed.");
  if (activeReport) {
    const stale = isStale(activeReport.latest);
    text("freshness", `Hosted feed collected ${new Date(activeReport.latest.collectedAt).toISOString()}${stale ?
      " - STALE: no successful observation in the last 36 hours." : "."}`);
    byId("freshness").classList.toggle("error", stale);
  }
}
function renderOverview(snapshot: Snapshot | undefined, libraries: Library[]): void {
  const totals = aggregate(libraries);
  text("custom-percent", percent(totals.customRatio));
  text("custom-fraction", `${integer.format(totals.customLines)} custom / ${integer.format(totals.totalLines)} total lines`);
  text("total-lines", integer.format(totals.totalLines));
  text("library-count", integer.format(totals.libraryCount));
  text("service-count", `${new Set(libraries.map((library) => library.service)).size} services`);
  text("generated-lines", integer.format(totals.generatedLines));
  text("generated-percent", totals.totalLines === 0 ? "No measured lines" :
    `${percent(totals.generatedLines / totals.totalLines)} of total source`);
  text("observation-info", snapshot ?
    `${snapshot.repository.name} | ${new Date(snapshot.collectedAt).toISOString()} | revision ${snapshot.repository.commit.slice(0, 12)} | ${snapshot.excludedLibraries.length} excluded projects` :
    "No observation loaded.");
  text("checkout-badge", snapshot ? snapshot.repository.isDirty ? "DIRTY CHECKOUT" : "CLEAN CHECKOUT" : "NO DATA");
  byId("checkout-badge").classList.toggle("dirty", !!snapshot?.repository.isDirty);
  text("contract-info", snapshot ?
    `Schema ${snapshot.schemaVersion} | Physical C# lines, including comments and blanks` :
    "Snapshot schema v1.0 / physical C# lines");
}
function renderBreakdown(libraries: Library[]): void {
  const group = select("breakdown-group").value === "service" ? "service" : "category";
  const rows = breakdown(libraries, group);
  const shown = group === "service" ? rows.slice(0, 12) : rows;
  const asPercent = select("breakdown-measure").value === "percent";
  text("breakdown-note", group === "service" ? `Top ${shown.length} of ${rows.length} matching services by total LOC.` :
    "Library-weighted source provenance by category.");
  const body = byId("breakdown-values");
  body.replaceChildren();
  for (const item of shown) {
    const row = document.createElement("tr");
    cell(row, categoryNames[item.name] || item.name);
    for (const key of ["customLines", "generatedLines"] as const) cell(row, integer.format(item.metrics[key]));
    cell(row, percent(item.metrics.customRatio));
    body.append(row);
  }
  breakdownChart?.destroy();
  const color = colors();
  const measures = [
    { key: "customLines", label: "Custom", color: color.custom },
    { key: "generatedLines", label: "Generated", color: color.generated },
  ] as const;
  const target = canvas("breakdown-chart");
  target.setAttribute("aria-label", `${group} provenance breakdown. Values are available in the table below.`);
  breakdownChart = new Chart<"bar">(target, {
    type: "bar",
    data: {
      labels: shown.map((item) => categoryNames[item.name] || item.name),
      datasets: measures.map((measure) => ({
        label: measure.label, backgroundColor: measure.color, borderRadius: 2,
        data: shown.map((item) => asPercent ?
          item.metrics.totalLines === 0 ? 0 : item.metrics[measure.key] / item.metrics.totalLines * 100 :
          item.metrics[measure.key]),
      })),
    },
    options: {
      responsive: true, maintainAspectRatio: false, indexAxis: "y",
      plugins: { legend: { display: false }, tooltip: { callbacks: {
        label: (context) => {
          const item = shown[context.dataIndex];
          const measure = measures[context.datasetIndex];
          return `${measure.label}: ${integer.format(item.metrics[measure.key])} lines${item.metrics.totalLines ? ` (${(item.metrics[measure.key] / item.metrics.totalLines * 100).toFixed(2)}%)` : " (N/A)"}`;
        },
      } } },
      scales: {
        x: { stacked: true, beginAtZero: true, max: asPercent ? 100 : undefined, grid: { color: color.grid }, ticks: { color: color.text } },
        y: { stacked: true, grid: { display: false }, ticks: { color: color.text } },
      },
    },
  });
}
function renderHistory(snapshot: Snapshot | undefined): void {
  historyChart?.destroy();
  historyChart = null;
  canvas("history-chart").hidden = !snapshot;
  byId("history-values").replaceChildren();
  if (!snapshot) {
    text("history-note", "Load observations to see history.");
    return;
  }
  const range = select("history-range").value;
  const observations = inRange(mergeObservations(forRepository([...publishedHistory, ...snapshots], selectedRepository)), snapshot,
    range === "all" ? null : Number(range));
  const result = history(observations, snapshot, filters(), {
    fixed: input("fixed-cohort").checked, includeDirty: input("include-dirty").checked,
    libraryId: select("history-scope").value,
  });
  const counts = `${result.points.length} observation${result.points.length === 1 ? "" : "s"} across ${result.distinctRevisions} measured revision${result.distinctRevisions === 1 ? "" : "s"}.`;
  let description = result.distinctRevisions < 2 ? `Baseline only: ${counts} No code-change trend yet.` : counts;
  if (result.fixedIds) description += ` Fixed cohort: ${result.fixedIds.size} libraries.`;
  if (result.excludedDirty) description += ` ${result.excludedDirty} dirty observations excluded.`;
  if (result.excludedIncompatible) description += ` ${result.excludedIncompatible} incompatible observations excluded.`;
  description += " Missing days and N/A values break the line.";
  text("history-note", description);
  if (!result.points.length) {
    canvas("history-chart").hidden = true;
    return;
  }
  for (const point of result.points) {
    const row = document.createElement("tr");
    cell(row, new Date(point.snapshot.collectedAt).toISOString().slice(0, 10));
    cell(row, `${point.snapshot.repository.commit.slice(0, 8)}${point.snapshot.repository.isDirty ? " (dirty)" : ""}`);
    cell(row, integer.format(point.metrics.libraryCount));
    cell(row, percent(point.metrics.customRatio));
    cell(row, `+${point.added} / -${point.removed}`);
    byId("history-values").append(row);
  }
  const series = trendSeries(result.points);
  const first = series[0].x;
  const last = series[series.length - 1].x;
  const singleDay = Math.floor(first / 86400000) === Math.floor(last / 86400000);
  const minimum = singleDay ? Math.floor(first / 86400000) * 86400000 : first;
  const maximum = singleDay ? minimum + 86400000 : last;
  const color = colors();
  historyChart = new Chart<"line", { x: number; y: number | null }[], number>(canvas("history-chart"), {
    type: "line",
    data: { datasets: [{
      label: "Inferred custom source (%)", data: series,
      borderColor: color.custom, backgroundColor: color.custom,
      borderWidth: 2, pointRadius: 4, pointHoverRadius: 6, spanGaps: false,
    }] },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: false }, tooltip: { callbacks: {
        title: (items) => {
          const time = items[0]?.parsed.x;
          return time === undefined || time === null ? "No observation" : new Date(time).toISOString();
        },
        label: (context) => `Custom: ${context.parsed.y?.toFixed(2) ?? "N/A"}%`,
      } } },
      scales: {
        x: {
          type: "linear", min: minimum, max: maximum, grid: { color: color.grid },
          title: { display: singleDay, text: `${new Date(first).toISOString().slice(0, 10)} (UTC)`, color: color.text },
          ticks: { color: color.text, maxTicksLimit: 5, callback: (value) => {
            const date = new Date(Number(value)).toISOString();
            return singleDay ? Number(value) === maximum ? "24:00" : date.slice(11, 16) : date.slice(0, 10);
          } },
        },
        y: { beginAtZero: true, max: 100, grid: { color: color.grid }, ticks: { color: color.text, callback: (value) => `${value}%` } },
      },
    },
  });
}
function renderLibraries(libraries: Library[]): void {
  const sorted = sortLibraries(libraries, sortKey, sortDescending);
  for (const button of document.querySelectorAll<HTMLButtonElement>("#library-table button[data-sort]")) {
    const key = sortKeys.find((candidate) => candidate === button.dataset.sort);
    const header = button.closest("th");
    if (!key || !header) throw new Error("Invalid library sort header.");
    const active = key === sortKey;
    header.setAttribute("aria-sort", active ? sortDescending ? "descending" : "ascending" : "none");
    button.setAttribute("aria-label", `${sortLabels[key]}, sort ${active && !sortDescending ? "descending" : "ascending"}`);
  }
  const body = byId("library-values");
  body.replaceChildren();
  text("library-table-note", `${sorted.length} matching libraries. Select one for details and its history. N/A sorts last.`);
  for (const library of sorted) {
    const row = document.createElement("tr");
    const button = document.createElement("button");
    button.type = "button";
    button.className = "library-link";
    button.textContent = library.library;
    button.addEventListener("click", () => {
      detailId = library.library;
      select("history-scope").value = detailId;
      renderDetail(current());
      renderHistory(current());
      byId("library-detail").scrollIntoView({ block: "nearest" });
    });
    row.insertCell().append(button);
    cell(row, library.service);
    cell(row, categoryNames[library.category]);
    cell(row, percent(library.metrics.customRatio));
    cell(row, integer.format(library.metrics.customLines));
    cell(row, integer.format(library.metrics.totalLines));
    body.append(row);
  }
  if (!sorted.length) {
    const row = document.createElement("tr");
    const empty = cell(row, "No libraries match these filters.");
    empty.colSpan = 6;
    empty.className = "empty-cell";
    body.append(row);
  }
}
function renderDetail(snapshot: Snapshot | undefined): void {
  const library = snapshot?.libraries.find((candidate) => candidate.library === detailId);
  byId("library-detail").hidden = !library;
  if (!library) return;
  text("detail-heading", library.library);
  text("detail-meta", `${library.service} / ${categoryNames[library.category]} | ${library.targetFrameworks.join(", ")}`);
  const metrics: [string, string][] = [
    ["Custom source", percent(library.metrics.customRatio)],
    ["Custom lines", integer.format(library.metrics.customLines)],
    ["Generated lines", integer.format(library.metrics.generatedLines)],
    ["Total lines", integer.format(library.metrics.totalLines)],
  ];
  byId("detail-stats").replaceChildren(...metrics.map(([label, value]) => {
    const item = document.createElement("span");
    item.textContent = label;
    const number = document.createElement("strong");
    number.textContent = value;
    item.append(number);
    return item;
  }));
  text("detail-source", `Project: ${library.projectPath}${library.files ? "" : ". No file-level evidence in this compact snapshot; collect with -IncludeFiles to audit provenance."}`);
  byId("file-evidence").hidden = !library.files;
  byId("file-values").replaceChildren();
  if (library.files) {
    const files = [...library.files].sort((a, b) => b.lines - a.lines || a.path.localeCompare(b.path));
    text("file-evidence-note", `Showing the largest ${Math.min(200, files.length)} of ${files.length} files.`);
    for (const file of files.slice(0, 200)) {
      const row = document.createElement("tr");
      cell(row, file.path);
      cell(row, integer.format(file.lines));
      cell(row, file.provenance);
      cell(row, file.evidence);
      byId("file-values").append(row);
    }
  }
}

byId("index-form").addEventListener("submit", (event) => {
  event.preventDefault();
  void perform(async () => {
    const url = input("index-url").value;
    setStatus("Loading the explicitly requested snapshot index...");
    const source = new URL(url);
    if (!["https:", "http:"].includes(source.protocol) || source.username || source.password) {
      throw new Error("An HTTP(S) index URL without credentials is required.");
    }
    const response = await fetch(source, { credentials: "omit", cache: "no-cache" });
    if (!response.ok) throw new Error(`Index request failed: HTTP ${response.status}.`);
    const value: unknown = await response.json();
    if (typeof value === "object" && value !== null && "latest" in value) {
      await openReport(url, value);
    } else {
      addSnapshots(await loadIndex(url), "Published index");
    }
  });
});
async function openReport(url: string, suppliedIndex?: unknown): Promise<void> {
  const nextReport = await loadReport(url, fetch, suppliedIndex);
  if (nextReport.latest.repository.name !== selectedRepository) {
    throw new Error(`Report repository does not match the selected ${selectedRepository}.`);
  }
  const nextHistory = await loadHistory(nextReport, 90);
  const nextSnapshots = mergeSnapshots(snapshots, [nextReport.latest]);
  mergeObservations([...nextHistory.observations, ...nextSnapshots]);
  snapshots = nextSnapshots;
  report = nextReport;
  publishedHistory = nextHistory.observations;
  monthCache = nextHistory.cache;
  loadedRange = "90";
  select("history-range").value = loadedRange;
  const all = [...select("history-range").options].find((item) => item.value === "all");
  if (all) all.disabled = true;
  selectedId = snapshots.at(-1)?.snapshotId || "";
  populateSnapshots(); populateServices(); populateHistoryScope(); render();
  setStatus(`Published index: latest snapshot and bounded monthly history loaded. ${report.index.history.length} months available; older months load on demand.`);
}
select("history-range").addEventListener("change", () => {
  if (!report) { loadedRange = select("history-range").value; render(); return; }
  const nextRange = select("history-range").value;
  const activeReport = report;
  void perform(async () => {
    try {
      setStatus("Loading the requested history range...");
      const anchor = current();
      if (!anchor) throw new Error("No selected observation for the history range.");
      const next = await loadHistory(activeReport, Number(nextRange), monthCache, fetch, anchor);
      mergeObservations([...next.observations, ...snapshots]);
      publishedHistory = next.observations;
      monthCache = next.cache;
      loadedRange = nextRange;
      populateServices(); populateHistoryScope(); render();
      setStatus(`Published index: ${nextRange}-day history loaded. Immutable monthly data is reused from this page's cache.`);
    } catch (error) {
      select("history-range").value = loadedRange;
      throw error;
    }
  });
});
select("snapshot-select").addEventListener("change", () => {
  const nextId = select("snapshot-select").value;
  const anchor = snapshots.find((snapshot) => snapshot.snapshotId === nextId);
  if (!report) {
    selectedId = nextId; populateServices(); render(); return;
  }
  if (!anchor) {
    select("snapshot-select").value = selectedId;
    setStatus("Selected observation was not found; existing data was retained.", true);
    return;
  }
  const activeReport = report;
  void perform(async () => {
    try {
      const next = await loadHistory(activeReport, Number(loadedRange), monthCache, fetch, anchor);
      mergeObservations([...next.observations, ...snapshots]);
      selectedId = nextId;
      publishedHistory = next.observations;
      monthCache = next.cache;
      populateServices(); populateHistoryScope(); render();
      setStatus("Published index: history loaded for the selected observation's UTC date.");
    } catch (error) {
      select("snapshot-select").value = selectedId;
      throw error;
    }
  });
});
for (const id of ["category-filter", "service-filter", "breakdown-group", "breakdown-measure", "history-scope"]) {
  select(id).addEventListener("change", render);
}
for (const id of ["fixed-cohort", "include-dirty"]) input(id).addEventListener("change", render);
for (const button of document.querySelectorAll<HTMLButtonElement>("#library-table button[data-sort]")) {
  button.addEventListener("click", () => {
    const key = sortKeys.find((candidate) => candidate === button.dataset.sort);
    if (!key) {
      setStatus("Unknown library sort column; existing data was retained.", true);
      return;
    }
    sortDescending = key === sortKey ? !sortDescending : false;
    sortKey = key;
    renderLibraries(filterLibraries(current()?.libraries || [], filters()));
  });
}
input("library-search").addEventListener("input", render);
select("repository-select").replaceChildren(...REPOSITORIES.map((repository) =>
  option(repository.name, repository.language)));
select("repository-select").value = selectedRepository;
select("repository-select").addEventListener("change", () => {
  const repository = REPOSITORIES.find((candidate) => candidate.name === select("repository-select").value);
  if (!repository) {
    select("repository-select").value = selectedRepository;
    setStatus("Unknown repository selection; existing observations were retained.", true);
    return;
  }
  selectedRepository = repository.name;
  setStatus(`Repository selected: ${repository.name}.`);
  render();
});
byId("close-detail").addEventListener("click", () => { detailId = ""; renderDetail(current()); });
matchMedia("(prefers-color-scheme: dark)").addEventListener("change", render);
setInterval(renderFreshness, 60000);

try {
  const seeds = globalThis.customCodeMetricsSeed;
  if (!Array.isArray(seeds)) throw new Error("Initial snapshots must be an array.");
  const url = globalThis.customCodeMetricsIndexUrl;
  if (typeof url !== "string") throw new Error("Initial report URL must be a string.");
  const preview = globalThis.customCodeMetricsPreview;
  if (preview !== undefined && typeof preview !== "boolean") throw new Error("Initial preview mode must be a boolean.");
  const accepted = seeds.map((seed: unknown) => acceptSnapshot(seed));
  staticPreview = preview === true;
  if (staticPreview) {
    if (!accepted.length || url) throw new Error("Static preview requires embedded observations and no automatic hosted feed.");
  }
  if (accepted.length) addSnapshots(accepted, "Build-time observations");
  else render();
  if (url) {
    input("index-url").value = url;
    void perform(async () => {
      setStatus("Loading the configured hosted report...");
      await openReport(url);
    });
  }
} catch (error) {
  setStatus(`Could not load initial data: ${error instanceof Error ? error.message : String(error)}`, true);
  console.error(error);
}
