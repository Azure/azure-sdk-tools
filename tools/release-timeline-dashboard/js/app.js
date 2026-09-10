import Alpine from "./vendor/alpine.esm.js";
import { DataStore } from "./data-store.js";
import {
  calculateSnapshot,
  derivePlanAnalytics,
  hydratePlan,
} from "./calculation-engine.mjs";
import { createTimelineScale, median } from "./timeline-scale.js";

const store = new DataStore();
const REPORTED_METRIC_IDS = new Set(["L1", "S1", "S4", "S5"]);

Alpine.data("timelineApp", () => ({
  loading: true,
  error: "",
  view: "portfolio",
  snapshot: null,
  portfolio: null,
  scorecard: null,
  definitions: [],
  plan: null,
  selectedEvent: null,
  selectedCohort: null,
  cohortEvidenceOpen: false,
  search: "",
  stateFilter: "all",
  delayedMetricFilters: [],
  planMetricResultsById: {},
  eventFilter: "all",
  trendsExpanded: false,

  async init() {
    window.addEventListener("popstate", () => this.loadRoute());
    window.addEventListener("keydown", (event) => {
      if (event.key === "Escape") this.closeDrawers();
    });
    try {
      this.snapshot = await store.initialize();
      const calculated = calculateSnapshot(this.snapshot);
      this.portfolio = calculated.portfolio;
      this.scorecard = calculated.scorecard;
      this.definitions = calculated.definitions;
      this.planMetricResultsById = Object.fromEntries(
        this.snapshot.facts.plans.map((fact) => [
          String(fact.id),
          derivePlanAnalytics(fact).metrics,
        ]),
      );
      await this.loadRoute();
    } catch (error) {
      this.error = error.message;
    } finally {
      this.loading = false;
    }
  },

  async loadRoute() {
    this.error = "";
    const params = new URLSearchParams(location.search);
    this.view = params.get("view") || "portfolio";
    this.selectedEvent = null;
    this.selectedCohort = null;
    if (this.view === "plan" && params.get("plan")) {
      this.loading = true;
      try {
        this.plan = hydratePlan(await store.plan(params.get("plan")));
        const eventId = params.get("event");
        if (eventId)
          this.selectedEvent =
            this.plan.events.find((event) => event.id === eventId) || null;
      } catch (error) {
        this.error = error.message;
      } finally {
        this.loading = false;
      }
    } else {
      this.plan = null;
    }
    window.scrollTo({ top: 0, behavior: "instant" });
  },

  async navigate(values) {
    history.pushState({}, "", this.routeUrl(values));
    await this.loadRoute();
  },

  routeUrl(values) {
    const params = new URLSearchParams(values);
    return `${location.pathname}?${params}`;
  },

  navigateLink(event, values) {
    if (
      event.button !== 0 ||
      event.ctrlKey ||
      event.metaKey ||
      event.shiftKey ||
      event.altKey
    )
      return;
    event.preventDefault();
    this.navigate(values);
  },

  get filteredPlans() {
    const query = this.search.trim().toLowerCase();
    return (this.portfolio?.plans || []).filter((plan) => {
      const matchesQuery =
        !query ||
        [
          plan.service,
          plan.title,
          plan.releasePlanId,
          plan.id,
          plan.releaseType,
        ]
          .join(" ")
          .toLowerCase()
          .includes(query);
      const matchesState =
        this.stateFilter === "all" ||
        plan.state === this.stateFilter ||
        (this.stateFilter === "active" &&
          ["new", "in-progress"].includes(plan.state)) ||
        (this.stateFilter === "abandoned" &&
          ["abandoned", "duplicate"].includes(plan.state));
      const matchesDelay =
        this.delayedMetricFilters.length === 0 ||
        (["new", "in-progress"].includes(plan.state) &&
          this.delayedMetricFilters.some((metricId) =>
            this.isP90Delayed(plan, metricId),
          ));
      return matchesQuery && matchesState && matchesDelay;
    });
  },

  selectStateFilter(state) {
    this.stateFilter = state;
    if (state !== "active") this.delayedMetricFilters = [];
  },

  toggleDelayedMetric(metricId) {
    this.stateFilter = "active";
    this.delayedMetricFilters = this.delayedMetricFilters.includes(metricId)
      ? this.delayedMetricFilters.filter((id) => id !== metricId)
      : [...this.delayedMetricFilters, metricId];
  },

  p90Threshold(metricId) {
    return (
      this.scorecard?.metrics.find((metric) => metric.metricId === metricId)
        ?.historicalStatistics?.p90 ?? null
    );
  },

  isP90Delayed(plan, metricId) {
    const threshold = this.p90Threshold(metricId);
    if (threshold === null) return false;
    return (this.planMetricResultsById[String(plan.id)] || []).some(
      (metric) =>
        metric.metricId === metricId &&
        metric.outcome === "complete" &&
        metric.value >= threshold,
    );
  },

  delayedFilterTitle(metricId) {
    const threshold = this.p90Threshold(metricId);
    const name = this.definition(metricId).name || metricId;
    return threshold === null
      ? `${name} has no finished-release P90 threshold`
      : `${name} completed observation at or above the finished-release P90 of ${this.formatDuration(threshold)}`;
  },

  readinessTooltip(readiness) {
    const descriptions = {
      validated:
        "Validated metrics use authoritative boundaries and are suitable for headline comparison.",
      provisional:
        "Provisional metrics depend on incomplete or observed release evidence and should be interpreted with caution.",
    };
    return descriptions[readiness] || "Metric readiness has not been classified.";
  },

  get scorecardMetrics() {
    return (this.scorecard?.metrics || [])
      .filter((metric) => REPORTED_METRIC_IDS.has(metric.metricId))
      .map((metric) => ({
        ...metric,
        ...this.definition(metric.metricId),
      }));
  },

  get portfolioCohortLabel() {
    const plans = this.portfolio?.plans || [];
    const createdAt = plans.map((plan) => plan.createdAt).filter(Boolean);
    const startAt =
      this.portfolio?.selection?.startAt ||
      (createdAt.length
        ? new Date(
            Math.min(...createdAt.map((value) => new Date(value).getTime())),
          ).toISOString()
        : null);
    const endAt =
      this.portfolio?.selection?.endAt ||
      this.portfolio?.generatedAt ||
      this.snapshot?.generatedAt;
    if (!startAt || !endAt) return "Management-plane cohort";
    const start = new Date(startAt);
    const end = new Date(endAt);
    const formatter = new Intl.DateTimeFormat("en", {
      month: "short",
      day: "numeric",
      year: "numeric",
      timeZone: "UTC",
    });
    return `${formatter.format(start)}–${formatter.format(end)} · management plane`;
  },

  periodRangeLabel(period) {
    if (!period?.start || !period?.end) return "";
    const displayEnd = new Date(period.end);
    if (!period.rolling)
      displayEnd.setUTCDate(displayEnd.getUTCDate() - 1);
    const formatter = new Intl.DateTimeFormat("en", {
      month: "short",
      day: "numeric",
      year: "numeric",
      timeZone: "UTC",
    });
    return `${formatter.format(new Date(period.start))}–${formatter.format(displayEnd)}`;
  },

  compactPeriodRangeLabel(period) {
    if (!period?.start || !period?.end) return "";
    const start = new Date(period.start);
    const end = new Date(period.end);
    if (!period.rolling) end.setUTCDate(end.getUTCDate() - 1);
    const month = new Intl.DateTimeFormat("en", {
      month: "short",
      timeZone: "UTC",
    });
    if (
      start.getUTCFullYear() === end.getUTCFullYear() &&
      start.getUTCMonth() === end.getUTCMonth()
    )
      return `${month.format(start)} ${start.getUTCDate()}–${end.getUTCDate()}`;
    const date = new Intl.DateTimeFormat("en", {
      month: "short",
      day: "numeric",
      timeZone: "UTC",
    });
    return `${date.format(start)}–${date.format(end)}`;
  },

  scorecardPeriods(metric) {
    if (metric.metricId === "L1")
      return [
        {
          id: "full-month",
          label: "Full month",
          statistics: metric.periodStatistics.monthly,
          showP50: true,
          showP90: false,
        },
        {
          id: "rolling-month",
          label: "Rolling 1mo",
          statistics: metric.periodStatistics.rollingMonth,
          showP50: true,
          showP90: false,
        },
        {
          id: "rolling-90-days",
          label: "Rolling 3mo",
          statistics: metric.periodStatistics.rolling90Days,
          showP50: false,
          showP90: true,
          showP90Change: false,
        },
      ];
    return [
      {
        id: "full-month",
        label: "Full month",
        statistics: metric.periodStatistics.monthly,
        showP50: true,
        showP90: true,
      },
      {
        id: "rolling-month",
        label: "Rolling 1mo",
        statistics: metric.periodStatistics.rollingMonth,
        showP50: true,
        showP90: true,
      },
      {
        id: "full-week",
        label: "Full week",
        statistics: metric.periodStatistics.weekly,
        showP50: true,
        showP90: true,
      },
      {
        id: "rolling-week",
        label: "Rolling 7d",
        statistics: metric.periodStatistics.rollingWeek,
        showP50: true,
        showP90: true,
      },
    ];
  },

  observationLabel(count) {
    return `${count} completed stage observation${count === 1 ? "" : "s"}`;
  },

  completeTrendSeries(trend) {
    return trend.series.filter((bucket) => !bucket.partial);
  },

  miniColumnStyle(value, series) {
    if (value === null) return "height:0";
    const maximum = Math.max(
      ...series.map((bucket) => bucket.p50).filter((item) => item !== null),
      1,
    );
    const height = Math.sqrt(Math.max(0, value) / maximum) * 100;
    return `height:${Math.max(4, height).toFixed(1)}%`;
  },

  trendMaximum(metric, trend) {
    return Math.max(
      this.historicalStatistic(metric, "p90") || 0,
      ...trend.series.flatMap((bucket) =>
        [bucket.p50, bucket.p90].filter((value) => value !== null),
      ),
      1,
    );
  },

  columnHeight(value, metric, trend) {
    if (value === null) return 0;
    return (
      Math.sqrt(Math.max(0, value) / this.trendMaximum(metric, trend)) * 100
    );
  },

  columnStyle(value, metric, trend) {
    const height = this.columnHeight(value, metric, trend);
    return `height:${Math.max(value > 0 ? 2 : 0, height).toFixed(1)}%`;
  },

  benchmarkStyle(metric, trend) {
    const top =
      100 -
      this.columnHeight(
        this.historicalStatistic(metric, "p50"),
        metric,
        trend,
      );
    return `top:${((top / 100) * 192).toFixed(1)}px`;
  },

  historicalStatistic(metric, field) {
    return metric.historicalStatistics?.[field] ?? null;
  },

  chartTicks(metric, trend) {
    const maximum = this.trendMaximum(metric, trend);
    return [0, 25, 50, 75, 100].map((position) => ({
      position,
      pixel: (position / 100) * 192,
      value: maximum * ((100 - position) / 100) ** 2,
    }));
  },

  columnTooltip(bucket, field) {
    if (bucket[field] === null)
      return `${bucket.label}: no completed stage data`;
    return `${bucket.label}${bucket.partial ? " (partial)" : ""}: ${field.toUpperCase()} ${this.formatDuration(bucket[field])}, n=${bucket.count} completed stage observation${bucket.count === 1 ? "" : "s"}\nClick to see release plans`;
  },

  tooltipEdgeClass(index, length) {
    if (index < length / 3) return "tooltip-left";
    if (index >= (length * 2) / 3) return "tooltip-right";
    return "";
  },

  bucketAxisLabel(bucket, index, length, cadence) {
    if (cadence === "month")
      return `${bucket.label}${bucket.partial ? "*" : ""}`;
    if (index === 0 || index === length - 1 || index % 2 === 0)
      return `${bucket.label}${bucket.partial ? "*" : ""}`;
    return "";
  },

  formatTrendChange(change) {
    if (change?.outcome !== "complete" || change.percent === null) return "—";
    const sign = change.percent > 0 ? "+" : "";
    return `${sign}${change.percent.toFixed(1)}%`;
  },

  rollingTrendChange(trend, live = false) {
    if (
      [
        "current-period-p50-vs-prior-three-month-p50",
        "current-period-vs-prior-three-month-average",
      ].includes(trend.comparison)
    )
      return live ? trend.liveChange : trend.change;
    const currentIndex = trend.series.length + (live ? -1 : -2);
    const current = trend.series[currentIndex];
    if (!current)
      return {
        baselinePeriodCount: 0,
        baselineSampleCount: 0,
        currentSampleCount: 0,
        outcome: "insufficient-data",
        percent: null,
        direction: "unknown",
      };
    const baselineStart = new Date(current.start);
    baselineStart.setUTCMonth(baselineStart.getUTCMonth() - 3);
    const baseline = trend.series
      .slice(0, currentIndex)
      .filter(
        (bucket) =>
          new Date(bucket.start) >= baselineStart && bucket.p50 !== null,
      );
    const baselineValue = baseline.length
      ? baseline.reduce((sum, bucket) => sum + bucket.p50, 0) / baseline.length
      : null;
    const comparison = {
      baselinePeriodCount: baseline.length,
      baselineSampleCount: baseline.reduce(
        (sum, bucket) => sum + bucket.count,
        0,
      ),
      currentSampleCount: current.count,
    };
    if (baselineValue === null || current.p50 === null)
      return {
        ...comparison,
        outcome: "insufficient-data",
        percent: null,
        direction: "unknown",
      };
    const absolute = current.p50 - baselineValue;
    return {
      ...comparison,
      outcome: "complete",
      percent:
        baselineValue === 0
          ? null
          : Math.round((absolute / baselineValue) * 1000) / 10,
      direction:
        absolute < 0 ? "improving" : absolute > 0 ? "slowing" : "flat",
    };
  },

  trendComparisonTitle(change) {
    if (!change.baselineSampleCount)
      return "No completed stage observations are available in the prior 3 months";
    return `Compared with the pooled P50 of ${change.baselineSampleCount} completed stage observations in the prior 3 months`;
  },

  trendAriaLabel(metric, cadence, live = false) {
    const trend = metric.trends[cadence];
    const period = cadence === "weekly" ? "week over week" : "month over month";
    const change = this.rollingTrendChange(trend, live);
    return `${metric.name} ${live ? "live " : "completed-period "}${period}: ${this.formatTrendChange(change)} in median duration against the pooled prior-three-month P50`;
  },

  selectTrendCohort(metric, cadence, bucket) {
    this.selectedCohort = {
      metricId: metric.metricId,
      metricName: metric.name,
      cadence,
      bucket,
      displayPeriod: bucket.partial
        ? {
            ...bucket,
            end: this.scorecard.cohort.generatedAt,
            rolling: true,
          }
        : bucket,
    };
  },

  closeCohort() {
    this.selectedCohort = null;
  },

  closeDrawers() {
    this.cohortEvidenceOpen = false;
    if (this.selectedCohort) this.closeCohort();
    if (this.selectedEvent) this.closeEvent();
  },

  cohortCadenceLabel(cadence) {
    return cadence === "weekly" ? "Weekly cohort" : "Monthly cohort";
  },

  cohortPlanSummary(plan) {
    const observations = `${plan.observationCount} observation${plan.observationCount === 1 ? "" : "s"}`;
    return `${observations} · P50 ${this.formatDuration(plan.p50)} · P90 ${this.formatDuration(plan.p90)}`;
  },

  get workflowStages() {
    if (!this.plan) return [];
    const spec = this.completedMetricValues("S1");
    const sdk = this.completedMetricValues("S4");
    const release = this.completedMetricValues("S5");
    return [
      { name: "Plan intake", detail: this.formatDate(this.plan.createdAt) },
      { name: "Spec review", detail: this.formatDuration(median(spec)) },
      { name: "SDK review", detail: this.formatDuration(median(sdk)) },
      {
        name:
          this.plan.state === "finished"
            ? "Released"
            : this.formatState(this.plan.state),
        detail:
          release.length > 0
            ? this.formatDuration(median(release))
            : "No complete boundary",
      },
    ];
  },

  get scale() {
    return this.plan
      ? createTimelineScale(this.plan.range.start, this.plan.range.end)
      : createTimelineScale(new Date(), new Date());
  },

  get timelineTicks() {
    return this.scale.ticks(6).map((tick) => ({
      position: tick.position,
      label: new Intl.DateTimeFormat("en", {
        month: "short",
        day: "numeric",
      }).format(tick.value),
    }));
  },

  get groupedPlanMetrics() {
    if (!this.plan) return [];
    return this.definitions
      .filter((definition) => REPORTED_METRIC_IDS.has(definition.id))
      .map((definition) => {
        const results = this.plan.metrics.filter(
          (metric) => metric.metricId === definition.id,
        );
        const complete = results.filter(
          (metric) => metric.outcome === "complete",
        );
        return {
          metricId: definition.id,
          name: definition.name,
          readiness: definition.readiness,
          median: median(complete.map((metric) => metric.value)),
          complete: complete.length,
          total: results.length,
          confidence: complete.some(
            (metric) => metric.confidence === "observed",
          )
            ? "observed"
            : "authoritative",
        };
      });
  },

  definition(id) {
    return this.definitions.find((definition) => definition.id === id) || {};
  },

  completedMetricValues(id) {
    return (this.plan?.metrics || [])
      .filter(
        (metric) => metric.metricId === id && metric.outcome === "complete",
      )
      .map((metric) => metric.value);
  },

  planMetric(id) {
    const values = this.completedMetricValues(id);
    return values.length ? this.formatDuration(median(values)) : "Unavailable";
  },

  intervalsFor(trackId) {
    return (this.plan?.intervals || []).filter(
      (interval) =>
        interval.trackId === trackId && interval.phase !== "generation",
    );
  },

  eventsFor(trackId) {
    const events = (this.plan?.events || []).filter(
      (event) => event.trackId === trackId,
    );
    if (this.eventFilter === "all") return events;
    if (this.eventFilter === "human")
      return events.filter((event) => event.actor?.kind === "human");
    return events.filter((event) =>
      [
        "plan.created",
        "plan.state_changed",
        "spec.approval_changed",
        "pr.created",
        "pr.merged",
        "pr.closed",
        "release.status_changed",
        "package.version_observed",
      ].includes(event.type),
    );
  },

  get filteredEventFeed() {
    if (!this.plan) return [];
    return this.plan.events
      .filter((event) => {
        if (this.eventFilter === "all") return true;
        if (this.eventFilter === "human")
          return event.actor?.kind === "human";
        return false;
      })
      .slice()
      .reverse();
  },

  eventPosition(event) {
    return this.scale.position(event.occurredAt);
  },

  eventClass(event) {
    return [
      event.type.split(".")[0],
      event.confidence,
      this.selectedEvent?.id === event.id ? "selected" : "",
    ].join(" ");
  },

  eventStyle(event) {
    const horizontalStack = Math.floor((event.stack || 0) / 3);
    const verticalStack = (event.stack || 0) % 3;
    return `left:calc(${this.eventPosition(event)}% + ${horizontalStack * 11}px);bottom:${19 + verticalStack * 10}px`;
  },

  intervalStyle(interval) {
    const left = this.scale.position(interval.startAt);
    const right = this.scale.position(interval.endAt || this.plan.range.end);
    return `left:${left}%;width:${Math.max(0.6, right - left)}%`;
  },

  intervalWidth(interval) {
    return (
      this.scale.position(interval.endAt || this.plan.range.end) -
      this.scale.position(interval.startAt)
    );
  },

  intervalTitle(interval) {
    return interval.status === "complete"
      ? `${interval.label}: ${this.formatDuration(interval.durationHours)}`
      : `${interval.label}: In progress`;
  },

  selectEvent(event) {
    this.selectedEvent = event;
    const params = new URLSearchParams(location.search);
    params.set("event", event.id);
    history.replaceState({}, "", `${location.pathname}?${params}`);
  },

  selectEventLink(clickEvent, event) {
    if (
      clickEvent.button !== 0 ||
      clickEvent.ctrlKey ||
      clickEvent.metaKey ||
      clickEvent.shiftKey ||
      clickEvent.altKey
    )
      return;
    clickEvent.preventDefault();
    this.selectEvent(event);
  },

  closeEvent() {
    this.selectedEvent = null;
    const params = new URLSearchParams(location.search);
    params.delete("event");
    history.replaceState({}, "", `${location.pathname}?${params}`);
  },

  artifactUrl(id) {
    const match = id?.match(/^github:([^#]+)#(\d+)$/);
    return match ? `https://github.com/${match[1]}/pull/${match[2]}` : "#";
  },

  languageAbbreviation(language) {
    return (
      {
        ".NET": "N",
        JavaScript: "JS",
        Python: "PY",
        Java: "JV",
        Go: "GO",
      }[language] || language.slice(0, 2).toUpperCase()
    );
  },

  formatState(value) {
    return String(value || "unknown")
      .split("-")
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(" ");
  },

  formatDuration(hours) {
    if (hours === null || hours === undefined) return "Unavailable";
    if (hours === 0) return "0";
    if (hours < 1) return `${Math.max(1, Math.round(hours * 60))}m`;
    if (hours < 48) return `${Math.round(hours)}h`;
    const days = hours / 24;
    return `${days >= 10 ? Math.round(days) : days.toFixed(1)}d`;
  },

  formatCompactDuration(hours) {
    return hours === null || hours === undefined
      ? "NA"
      : this.formatDuration(hours);
  },

  formatDate(value) {
    if (!value) return "";
    return new Intl.DateTimeFormat("en", {
      month: "short",
      day: "numeric",
      year: "numeric",
    }).format(new Date(value));
  },

  formatDateTime(value) {
    if (!value) return "";
    return new Intl.DateTimeFormat("en", {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(value));
  },
}));

window.Alpine = Alpine;
Alpine.start();
