export const CALCULATION_ENGINE_VERSION = 5;

export const METRIC_DEFINITIONS = [
  {
    id: "L1",
    version: 1,
    name: "Full time-to-SDK",
    description:
      "First linked spec PR creation to the final observed intended-language release.",
    unit: "hours",
    readiness: "provisional",
  },
  {
    id: "S1",
    version: 1,
    name: "Spec PR cycle time",
    description: "Spec PR creation to merge.",
    unit: "hours",
    readiness: "validated",
  },
  {
    id: "S2",
    version: 1,
    name: "Generation trigger latency",
    description:
      "Spec merge to the exact linked generation pipeline start.",
    unit: "hours",
    readiness: "provisional",
  },
  {
    id: "S3",
    version: 1,
    name: "Generation execution",
    description: "Exact linked generation pipeline start to SDK PR creation.",
    unit: "hours",
    readiness: "provisional",
  },
  {
    id: "S4",
    version: 2,
    name: "SDK delivery cycle",
    description:
      "Earliest attributed SDK PR creation to the final successful SDK PR merge.",
    unit: "hours",
    readiness: "validated",
  },
  {
    id: "S5",
    version: 1,
    name: "Release latency",
    description:
      "SDK PR merge to the trusted package-release boundary.",
    unit: "hours",
    readiness: "provisional",
  },
];

export function calculateSnapshot(snapshot) {
  assertSnapshotInput(snapshot);
  const analytics = snapshot.facts.plans.map(derivePlanAnalytics);
  const byId = new Map(analytics.map((plan) => [String(plan.id), plan]));
  const plans = snapshot.portfolio.plans.map((plan) => {
    const calculated = byId.get(String(plan.id));
    if (!calculated) throw new Error(`Missing analytical facts for plan ${plan.id}`);
    return {
      ...plan,
      quality: calculated.quality.status,
      warnings: calculated.quality.warnings.length,
    };
  });
  const completeMetrics = analytics.flatMap((plan) =>
    plan.metrics.filter((metric) => metric.outcome === "complete"),
  );
  const cycle = completeMetrics
    .filter((metric) => metric.metricId === "L1")
    .map((metric) => metric.value);
  return {
    definitions: METRIC_DEFINITIONS,
    scorecard: buildScorecard(
      snapshot.facts.plans,
      snapshot.generatedAt,
      snapshot.selection.metricStartAt,
    ),
    portfolio: {
      ...snapshot.portfolio,
      generatedAt: snapshot.generatedAt,
      selection: snapshot.selection,
      kpis: {
        total: plans.length,
        active: plans.filter((plan) =>
          ["new", "in-progress"].includes(plan.state),
        ).length,
        finished: plans.filter((plan) => plan.state === "finished").length,
        abandoned: plans.filter((plan) =>
          ["abandoned", "duplicate"].includes(plan.state),
        ).length,
        medianCycleHours: percentile(cycle, 0.5),
        p90CycleHours: percentile(cycle, 0.9),
        dataQualityPercent: Math.round(
          (completeMetrics.length /
            Math.max(
              1,
              analytics.reduce((sum, plan) => sum + plan.metrics.length, 0),
            )) *
            100,
        ),
      },
      plans,
    },
  };
}

export function hydratePlan(plan) {
  const calculated = derivePlanAnalytics(plan.boundaryFacts);
  return {
    ...plan,
    metrics: calculated.metrics,
    intervals: calculated.intervals,
    quality: calculated.quality,
  };
}

export function derivePlanAnalytics(fact) {
  assertPlanFact(fact);
  const metrics = [];
  const intervals = [];

  for (const pr of fact.specPrs) {
    metrics.push(
      durationMetric(
        "S1",
        { planId: fact.id, prId: pr.id, role: "spec-pr" },
        pr.createdAt,
        pr.mergedAt,
        "authoritative",
      ),
    );
    intervals.push(prInterval(pr, pr.trackId, "spec-review"));
  }

  const specCreated = earliest(fact.specPrs.map((pr) => pr.createdAt));
  const specMerged = latest(fact.specPrs.map((pr) => pr.mergedAt));
  const releases = [];
  for (const artifact of fact.artifacts) {
    const release = releaseBoundary(artifact);
    const releaseAt = release?.occurredAt || null;
    const attempts =
      artifact.prAttempts?.length > 0
        ? artifact.prAttempts
        : artifact.prId
          ? [
              {
                id: artifact.prId,
                createdAt: artifact.prCreatedAt,
                mergedAt: artifact.prMergedAt,
                closedAt: artifact.prClosedAt,
              },
            ]
          : [];
    const s4Start = earliest(attempts.map((attempt) => attempt.createdAt));
    const s4End = artifact.prMergedAt;
    metrics.push(
      durationMetric(
        "S4",
        {
          planId: fact.id,
          language: artifact.language,
          package: artifact.package,
          prIds: attempts.map((attempt) => attempt.id),
        },
        s4Start,
        s4End,
        "authoritative",
        2,
      ),
      durationMetric(
        "S2",
        { planId: fact.id, language: artifact.language },
        specMerged,
        artifact.generationStartAt,
        "authoritative",
      ),
      durationMetric(
        "S3",
        { planId: fact.id, language: artifact.language },
        artifact.generationStartAt,
        artifact.prCreatedAt,
        "authoritative",
      ),
      durationMetric(
        "S5",
        { planId: fact.id, language: artifact.language },
        artifact.prMergedAt,
        releaseAt,
        release?.confidence || "observed",
      ),
    );
    if (releaseAt) releases.push(release);
    if (artifact.prId)
      intervals.push(
        prInterval(
          {
            id: artifact.prId,
            createdAt: artifact.prCreatedAt,
            mergedAt: artifact.prMergedAt,
            closedAt: artifact.prClosedAt,
          },
          artifact.trackId,
          "sdk-review",
        ),
      );
    if (
      artifact.generationStartAt &&
      artifact.prCreatedAt &&
      nonNegativeDuration(artifact.generationStartAt, artifact.prCreatedAt)
    )
      intervals.push({
        id: `interval:${fact.id}:${artifact.language}:generation`,
        phase: "generation",
        trackId: artifact.trackId,
        label: "Generation execution",
        startAt: artifact.generationStartAt,
        endAt: artifact.prCreatedAt,
        durationHours: hours(artifact.generationStartAt, artifact.prCreatedAt),
        status: "complete",
        confidence: "authoritative",
      });
    if (
      artifact.prMergedAt &&
      releaseAt &&
      nonNegativeDuration(artifact.prMergedAt, releaseAt)
    )
      intervals.push({
        id: `interval:${fact.id}:${artifact.language}:release`,
        phase: "release",
        trackId: artifact.trackId,
        label: "Release latency",
        startAt: artifact.prMergedAt,
        endAt: releaseAt,
        durationHours: hours(artifact.prMergedAt, releaseAt),
        status: "complete",
        confidence: "observed",
      });
  }

  const allIntendedReleased =
    fact.artifacts.length > 0 && releases.length === fact.artifacts.length;
  const l1 = durationMetric(
    "L1",
    { planId: fact.id },
    specCreated,
    allIntendedReleased
      ? latest(releases.map((release) => release.occurredAt))
      : null,
    releases.some((release) => release.confidence === "inferred")
      ? "inferred"
      : "observed",
  );
  if (!fact.artifacts.length) {
    l1.outcome = "ineligible";
    l1.missingBoundaryReason = "no-intended-artifacts";
  }
  metrics.push(l1);

  const warnings = [...(fact.quality?.warnings || [])];
  const incomplete = metrics.filter((metric) => metric.outcome !== "complete");
  return {
    id: fact.id,
    metrics,
    intervals: intervals.filter(
      (interval) =>
        interval.durationHours === null || interval.durationHours >= 0,
    ),
    quality: {
      ...(fact.quality || {}),
      status: incomplete.length
        ? "partial"
        : warnings.length
          ? "qualified"
          : "complete",
      warnings,
      metricResults: metrics.length,
      observedMetricResults: metrics.filter(
        (metric) => metric.confidence === "observed",
      ).length,
      incompleteMetricResults: incomplete.length,
    },
  };
}

export function buildScorecard(planFacts, generatedAt, metricStartAt = null) {
  const anchor = validDate(generatedAt, "generatedAt");
  const metricStart = metricStartAt
    ? validDate(metricStartAt, "metricStartAt")
    : null;
  const eligibleFacts = planFacts.filter((plan) => plan.state === "finished");
  const rollingWeekStart = new Date(anchor.getTime() - 7 * 86_400_000);
  const rolling90DaysStart = new Date(anchor.getTime() - 90 * 86_400_000);
  const rollingMonthStart = shiftUtcMonthClamped(anchor, -1);
  return {
    schemaVersion: 3,
    calculationEngineVersion: CALCULATION_ENGINE_VERSION,
    cohort: {
      kind: "core-correlated-finished-management-plane",
      eligiblePlanCount: eligibleFacts.length,
      totalPlanCount: planFacts.length,
      generatedAt: anchor.toISOString(),
      excludedStateCounts: {
        new: planFacts.filter((plan) => plan.state === "new").length,
        inProgress: planFacts.filter((plan) => plan.state === "in-progress")
          .length,
        abandoned: planFacts.filter((plan) => plan.state === "abandoned").length,
        duplicate: planFacts.filter((plan) => plan.state === "duplicate").length,
      },
    },
    metrics: METRIC_DEFINITIONS.map((definition) => {
      const results = eligibleFacts.flatMap((fact) =>
        derivePlanAnalytics(fact).metrics
          .filter((metric) => metric.metricId === definition.id)
          .map((metric) => {
            if (
              metric.outcome === "complete" &&
              metricStart &&
              new Date(metric.evidence.endAt) < metricStart
            )
              return {
                ...metric,
                outcome: "excluded",
                value: null,
                missingBoundaryReason: "before-metric-period",
                cohortPlan: fact.cohortPlan,
              };
            return { ...metric, cohortPlan: fact.cohortPlan };
          }),
      );
      const completeResults = results.filter(
        (metric) => metric.outcome === "complete",
      );
      const values = completeResults.map((metric) => metric.value);
      const trends = {
        weekly: buildTrend(completeResults, "week", anchor, 13),
        monthly: buildTrend(
          completeResults,
          "month",
          anchor,
          monthlyBucketCount(completeResults, anchor),
        ),
      };
      return {
        metricId: definition.id,
        definitionVersion: definition.version,
        periodStatistics: {
          rollingWeek: aggregatePeriod(
            completeResults,
            "rolling-week",
            rollingWeekStart,
            anchor,
            true,
          ),
          weekly: completedPeriodStatistics(trends.weekly),
          rollingMonth: aggregatePeriod(
            completeResults,
            "rolling-month",
            rollingMonthStart,
            anchor,
            true,
          ),
          rolling90Days: {
            ...aggregatePeriod(
              completeResults,
              "rolling-90-days",
              rolling90DaysStart,
              anchor,
              true,
            ),
            p90Change: insufficientChange(),
          },
          monthly: completedPeriodStatistics(trends.monthly),
        },
        historicalStatistics: {
          p50: percentile(values, 0.5),
          p90: percentile(values, 0.9),
        },
        population: {
          eligible: results.filter((metric) => metric.outcome !== "ineligible")
            .length,
          included: values.length,
          incomplete: countOutcome(results, "incomplete"),
          censored: countOutcome(results, "censored"),
          excluded: countOutcome(results, "excluded"),
          ineligible: countOutcome(results, "ineligible"),
        },
        confidenceCounts: {
          authoritative: results.filter(
            (metric) =>
              metric.outcome === "complete" &&
              metric.confidence === "authoritative",
          ).length,
          observed: results.filter(
            (metric) =>
              metric.outcome === "complete" && metric.confidence === "observed",
          ).length,
          inferred: results.filter(
            (metric) =>
              metric.outcome === "complete" &&
              metric.confidence === "inferred",
          ).length,
        },
        trends,
      };
    }),
  };
}

function countOutcome(results, outcome) {
  return results.filter((metric) => metric.outcome === outcome).length;
}

function releaseBoundary(artifact) {
  if (artifact.releaseBoundaryAt)
    return {
      occurredAt: artifact.releaseBoundaryAt,
      confidence:
        artifact.releaseBoundaryConfidence === "inferred"
          ? "inferred"
          : "observed",
    };
  if (!artifact.releasedVersion) return null;
  const occurredAt = earliest(
    (artifact.releaseEvents || [])
      .filter((event) => /released/i.test(event.value))
      .map((event) => event.occurredAt),
  );
  return occurredAt ? { occurredAt, confidence: "observed" } : null;
}

function durationMetric(
  metricId,
  scope,
  start,
  end,
  confidence,
  definitionVersion = 1,
) {
  const complete = Boolean(start && end && nonNegativeDuration(start, end));
  return {
    metricId,
    definitionVersion,
    scope,
    outcome: complete ? "complete" : "incomplete",
    value: complete ? hours(start, end) : null,
    unit: "hours",
    confidence,
    evidence: { startAt: start || null, endAt: end || null },
    missingBoundaryReason: complete
      ? null
      : !start
        ? "missing-start-event"
        : "missing-end-event",
  };
}

function prInterval(pr, trackId, phase) {
  const end = pr.mergedAt || pr.closedAt;
  return {
    id: `interval:${pr.id}:${trackId}`,
    phase,
    trackId,
    label: phase === "spec-review" ? "Spec PR cycle" : "SDK PR cycle",
    startAt: pr.createdAt,
    endAt: end,
    durationHours: pr.createdAt && end ? hours(pr.createdAt, end) : null,
    status: end ? "complete" : "censored",
    confidence: "authoritative",
  };
}

function completedPeriodStatistics(trend) {
  const bucket = trend.series.at(-2);
  if (!bucket) throw new Error(`${trend.cadence} trend lacks a completed period`);
  return {
    key: bucket.key,
    start: bucket.start,
    end: bucket.end,
    count: bucket.count,
    p50: bucket.p50,
    p90: bucket.p90,
    plans: bucket.plans,
    rolling: false,
    change: trend.change,
    p90Change: trend.p90Change,
  };
}

function aggregatePeriod(results, key, start, end, rolling) {
  const periodResults = resultsForPeriod(results, start, end);
  const values = periodResults.map((result) => result.value);
  const period = {
    key,
    start: start.toISOString(),
    end: end.toISOString(),
    count: values.length,
    p50: percentile(values, 0.5),
    p90: percentile(values, 0.9),
    plans: cohortPlans(periodResults),
    rolling,
  };
  const baselineStart = shiftUtcMonthClamped(start, -3);
  const baselineValues = resultsForPeriod(results, baselineStart, start).map(
    (result) => result.value,
  );
  return {
    ...period,
    change: changeAgainstBaseline(period, baselineValues, {
      baselinePeriodCount: 3,
      baselineKeys: [],
    }),
    p90Change: changeAgainstBaseline(
      period,
      baselineValues,
      {
        baselinePeriodCount: 3,
        baselineKeys: [],
      },
      "p90",
      0.9,
    ),
  };
}

function buildTrend(results, cadence, generatedAt, bucketCount) {
  const currentStart =
    cadence === "week"
      ? startOfUtcWeek(generatedAt)
      : startOfUtcMonth(generatedAt);
  const starts = Array.from({ length: bucketCount }, (_, index) =>
    shiftBucket(currentStart, cadence, index - bucketCount + 1),
  );
  const series = starts.map((start, index) => {
    const end = shiftBucket(start, cadence, 1);
    const bucketResults = resultsForPeriod(results, start, end);
    const values = bucketResults.map((result) => result.value);
    return {
      key: bucketKey(start, cadence),
      label: bucketLabel(start, cadence),
      start: start.toISOString(),
      end: end.toISOString(),
      partial: index === starts.length - 1,
      count: values.length,
      p50: percentile(values, 0.5),
      p90: percentile(values, 0.9),
      plans: cohortPlans(bucketResults),
    };
  });
  return {
    cadence,
    series,
    comparison: "current-period-p50-vs-prior-three-month-p50",
    change: rollingPeriodChange(series, results, -2),
    p90Change: rollingPeriodChange(series, results, -2, "p90", 0.9),
    liveChange: rollingPeriodChange(series, results, -1),
  };
}

function rollingPeriodChange(
  series,
  results,
  currentOffset,
  field = "p50",
  fraction = 0.5,
) {
  const currentIndex =
    currentOffset < 0 ? series.length + currentOffset : currentOffset;
  const current = series[currentIndex];
  if (!current) return insufficientChange();
  const baselineStart = shiftUtcMonthClamped(new Date(current.start), -3);
  const baselineBuckets = series
    .slice(0, currentIndex)
    .filter(
      (bucket) =>
        new Date(bucket.start) >= baselineStart && bucket[field] !== null,
    );
  const baselineValues = resultsForPeriod(
    results,
    baselineStart,
    new Date(current.start),
  ).map((result) => result.value);
  return changeAgainstBaseline(current, baselineValues, {
    baselinePeriodCount: baselineBuckets.length,
    baselineKeys: baselineBuckets.map((bucket) => bucket.key),
  }, field, fraction);
}

function changeAgainstBaseline(
  current,
  baselineValues,
  context,
  field = "p50",
  fraction = 0.5,
) {
  const baselineValue = percentile(baselineValues, fraction);
  const currentValue = current[field];
  const comparison = {
    baselineValue,
    baselineSampleCount: baselineValues.length,
    ...context,
    currentKey: current.key,
    currentSampleCount: current.count,
  };
  if (baselineValue === null || currentValue === null)
    return { ...insufficientChange(), ...comparison };
  const absolute = Math.round((currentValue - baselineValue) * 10) / 10;
  return {
    ...comparison,
    outcome: "complete",
    absolute,
    percent:
      baselineValue === 0
        ? null
        : Math.round((absolute / baselineValue) * 1000) / 10,
    direction: absolute < 0 ? "improving" : absolute > 0 ? "slowing" : "flat",
  };
}

function insufficientChange() {
  return {
    baselineValue: null,
    baselinePeriodCount: 0,
    baselineSampleCount: 0,
    baselineKeys: [],
    currentKey: null,
    currentSampleCount: 0,
    outcome: "insufficient-data",
    absolute: null,
    percent: null,
    direction: "unknown",
  };
}

function cohortPlans(results) {
  const grouped = new Map();
  for (const result of results) {
    const plan = result.cohortPlan;
    if (!grouped.has(plan.id)) grouped.set(plan.id, { ...plan, values: [] });
    grouped.get(plan.id).values.push(result.value);
  }
  return [...grouped.values()]
    .map(({ values, ...plan }) => ({
      ...plan,
      observationCount: values.length,
      p50: percentile(values, 0.5),
      p90: percentile(values, 0.9),
    }))
    .sort(
      (left, right) =>
        right.p50 - left.p50 ||
        left.service.localeCompare(right.service) ||
        String(left.id).localeCompare(String(right.id)),
    );
}

function monthlyBucketCount(results, generatedAt) {
  const currentStart = startOfUtcMonth(generatedAt);
  const earliestAllowed = shiftBucket(currentStart, "month", -11);
  const dates = results
    .map((result) => result.evidence?.endAt)
    .filter(Boolean)
    .map(startOfUtcMonth)
    .filter((date) => date >= earliestAllowed && date <= currentStart);
  if (!dates.length) return 2;
  const earliest = new Date(Math.min(...dates));
  const difference =
    (currentStart.getUTCFullYear() - earliest.getUTCFullYear()) * 12 +
    currentStart.getUTCMonth() -
    earliest.getUTCMonth();
  return Math.min(12, Math.max(2, difference + 1));
}

function resultsForPeriod(results, start, end) {
  return results.filter((result) => {
    const occurredAt = result.evidence?.endAt;
    return occurredAt && new Date(occurredAt) >= start && new Date(occurredAt) < end;
  });
}

export function percentile(values, fraction) {
  if (!values.length) return null;
  const sorted = [...values].sort((left, right) => left - right);
  const index = (sorted.length - 1) * fraction;
  const lower = Math.floor(index);
  const upper = Math.ceil(index);
  return Math.round(
    (sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower)) * 10,
  ) / 10;
}

export function shiftUtcMonthClamped(value, amount) {
  const date = new Date(value);
  const day = date.getUTCDate();
  date.setUTCDate(1);
  date.setUTCMonth(date.getUTCMonth() + amount);
  const lastDay = new Date(
    Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0),
  ).getUTCDate();
  date.setUTCDate(Math.min(day, lastDay));
  return date;
}

function startOfUtcWeek(value) {
  const date = new Date(value);
  date.setUTCHours(0, 0, 0, 0);
  const day = date.getUTCDay() || 7;
  date.setUTCDate(date.getUTCDate() - day + 1);
  return date;
}

function startOfUtcMonth(value) {
  const date = new Date(value);
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1));
}

function shiftBucket(value, cadence, amount) {
  const date = new Date(value);
  if (cadence === "week") date.setUTCDate(date.getUTCDate() + amount * 7);
  else date.setUTCMonth(date.getUTCMonth() + amount);
  return date;
}

function bucketKey(date, cadence) {
  return cadence === "week"
    ? date.toISOString().slice(0, 10)
    : date.toISOString().slice(0, 7);
}

function bucketLabel(date, cadence) {
  return new Intl.DateTimeFormat("en", {
    month: "short",
    ...(cadence === "week" ? { day: "numeric" } : {}),
    timeZone: "UTC",
  }).format(date);
}

function earliest(values) {
  const dates = values.filter(Boolean).map((value) => new Date(value));
  return dates.length ? new Date(Math.min(...dates)).toISOString() : null;
}

function latest(values) {
  const dates = values.filter(Boolean).map((value) => new Date(value));
  return dates.length ? new Date(Math.max(...dates)).toISOString() : null;
}

function hours(start, end) {
  return Math.round(((new Date(end) - new Date(start)) / 3_600_000) * 10) / 10;
}

function nonNegativeDuration(start, end) {
  return new Date(end).getTime() >= new Date(start).getTime();
}

function validDate(value, name) {
  const date = new Date(value);
  if (!value || Number.isNaN(date.getTime())) throw new Error(`Invalid ${name}`);
  return date;
}

function assertSnapshotInput(snapshot) {
  if (
    !snapshot ||
    snapshot.schemaVersion !== 2 ||
    snapshot.dataSchemaVersion !== 2
  )
    throw new Error("Unsupported snapshot schema");
  validDate(snapshot.generatedAt, "snapshot generatedAt");
  if (!Array.isArray(snapshot.portfolio?.plans))
    throw new Error("Snapshot portfolio index is missing");
  if (!Array.isArray(snapshot.facts?.plans))
    throw new Error("Snapshot analytical facts are missing");
}

function assertPlanFact(fact) {
  if (!fact?.id) throw new Error("Plan fact is missing an id");
  if (!Array.isArray(fact.specPrs) || !Array.isArray(fact.artifacts))
    throw new Error(`Plan ${fact.id} has invalid boundary facts`);
}
