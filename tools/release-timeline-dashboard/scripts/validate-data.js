#!/usr/bin/env node

const { createHash } = require("node:crypto");
const {
  existsSync,
  readdirSync,
  readFileSync,
  statSync,
} = require("node:fs");
const { join } = require("node:path");
const { parseArgs, readJson } = require("./lib/v2-common");

const args = parseArgs(process.argv.slice(2), {
  data: "data",
  snapshot: "",
  parityPortfolio: "",
  parityScorecard: "",
});
const errors = [];

main().catch((error) => {
  console.error(error.stack || error.message);
  process.exitCode = 1;
});

async function main() {
  const {
    CALCULATION_ENGINE_VERSION,
    buildScorecard,
    calculateSnapshot,
    derivePlanAnalytics,
    hydratePlan,
    percentile,
    shiftUtcMonthClamped,
  } = await import("../js/calculation-engine.mjs");
  const snapshot = readJson(
    args.snapshot || join(args.data, "snapshot.json"),
  );
  const root = join(args.data, "builds", snapshot.snapshotId);
  const planDirectory = join(root, "plans");
  const plans = existsSync(planDirectory)
    ? readdirSync(planDirectory)
        .filter((name) => name.endsWith(".json"))
        .map((name) => readJson(join(planDirectory, name)))
    : [];

  validateSnapshot(snapshot, plans, root);
  for (const plan of plans) validatePlan(plan, snapshot.selection, hydratePlan);

  let first;
  let second;
  try {
    first = calculateSnapshot(snapshot);
    second = calculateSnapshot(snapshot);
  } catch (error) {
    errors.push(`Calculation failed: ${error.message}`);
  }
  if (first && JSON.stringify(first) !== JSON.stringify(second))
    errors.push("Calculation engine output is not deterministic");
  if (first) validateCalculated(first, snapshot);

  validateFixtures({
    buildScorecard,
    derivePlanAnalytics,
    percentile,
    shiftUtcMonthClamped,
  });

  if (args.parityScorecard && first) {
    const legacy = readJson(args.parityScorecard);
    const current = structuredClone(first.scorecard);
    delete current.calculationEngineVersion;
    if (JSON.stringify(current) !== JSON.stringify(legacy))
      errors.push("Shared-engine scorecard does not match migration baseline");
  }
  if (args.parityPortfolio && first) {
    const legacy = readJson(args.parityPortfolio);
    const planSummary = (plans) =>
      plans.map(({ id, quality, warnings }) => ({ id, quality, warnings }));
    if (
      JSON.stringify(first.portfolio.kpis) !== JSON.stringify(legacy.kpis) ||
      JSON.stringify(planSummary(first.portfolio.plans)) !==
        JSON.stringify(planSummary(legacy.plans))
    )
      errors.push("Dynamic portfolio calculations do not match migration baseline");
  }

  scanPublishedFile(args.snapshot || join(args.data, "snapshot.json"));
  scanPublishedFiles(root);
  if (errors.length) {
    console.error(errors.map((error) => `- ${error}`).join("\n"));
    process.exitCode = 1;
  } else {
    console.log(
      `Validated snapshot ${snapshot.snapshotId}: ${plans.length} plans, ${snapshot.counts.events} events, deterministic engine v${CALCULATION_ENGINE_VERSION}`,
    );
  }
}

function validateSnapshot(snapshot, plans, root) {
  if (snapshot.schemaVersion !== 2 || snapshot.dataSchemaVersion !== 2)
    errors.push("Snapshot schema version is unsupported");
  for (const field of [
    "calculationEngineVersion",
    "scorecard",
    "definitions",
  ]) {
    if (field in snapshot)
      errors.push(`Snapshot publishes derived field ${field}`);
  }
  if (snapshot.portfolio?.kpis)
    errors.push("Snapshot publishes derived portfolio KPIs");
  if (!snapshot.snapshotId || !validDate(snapshot.generatedAt))
    errors.push("Snapshot identity or generatedAt is invalid");
  if (!snapshot.cohortAccounting)
    errors.push("Snapshot cohort accounting is missing");
  else {
    const accounting = snapshot.cohortAccounting;
    const inventory = accounting.inventory;
    const l1Coverage = accounting.metricCoverage?.L1;
    const finished = snapshot.portfolio?.plans?.filter(
      (plan) => plan.state === "finished",
    ).length;
    if (inventory?.published !== plans.length)
      errors.push("Published cohort count does not reconcile with detailed plans");
    if (
      accounting.exclusions?.length !==
      inventory?.managementPlane - inventory?.published
    )
      errors.push("Cohort exclusions do not reconcile with the inventory");
    if (
      !l1Coverage ||
      l1Coverage.complete +
          l1Coverage.incomplete +
          l1Coverage.ineligible +
          (l1Coverage.excluded || 0) !==
        finished
    )
      errors.push("L1 coverage does not reconcile with finished plans");
    if (l1Coverage?.incompletePlans?.length !== l1Coverage?.incomplete)
      errors.push("Incomplete L1 evidence links do not reconcile");
    if (
      Object.keys(accounting.boundarySources || {}).some(
        (source) =>
          !["release-plan", "github-release", "missing"].includes(source),
      )
    )
      errors.push("Cohort accounting contains an invalid release-boundary source");
  }
  if (plans.length !== snapshot.counts?.plans)
    errors.push(`Snapshot says ${snapshot.counts?.plans} plans but ${plans.length} exist`);
  if (snapshot.portfolio?.plans?.length !== plans.length)
    errors.push("Portfolio index does not reconcile with detailed plans");
  if (snapshot.facts?.plans?.length !== plans.length)
    errors.push("Boundary facts do not reconcile with detailed plans");
  if (!snapshot.paths?.plan?.includes("{id}"))
    errors.push("Immutable plan path template is missing");
  if (Object.keys(snapshot.paths || {}).some((path) => path !== "plan"))
    errors.push("Snapshot publishes unused immutable view paths");
  validateSelection(snapshot.selection);

  const ids = new Set(snapshot.facts?.plans?.map((plan) => String(plan.id)));
  if (ids.size !== plans.length)
    errors.push("Boundary fact plan identities are missing or duplicated");
  const portfolioIds = new Set(
    snapshot.portfolio?.plans?.map((plan) => String(plan.id)),
  );
  if (
    ids.size !== portfolioIds.size ||
    [...ids].some((id) => !portfolioIds.has(id))
  )
    errors.push("Portfolio index and boundary-fact identities do not reconcile");
  for (const [relative, expected] of Object.entries(snapshot.hashes || {})) {
    if (!/^plans\/[^/]+\.json$/.test(relative))
      errors.push(`Unexpected immutable view file: ${relative}`);
    const path = join(root, relative);
    if (!existsSync(path)) {
      errors.push(`Hashed file is missing: ${relative}`);
      continue;
    }
    const actual = createHash("sha256").update(readFileSync(path)).digest("hex");
    if (actual !== expected) errors.push(`Hash mismatch: ${relative}`);
  }
  const actualFiles = listFiles(root);
  if (actualFiles.length !== Object.keys(snapshot.hashes || {}).length)
    errors.push("Snapshot hash inventory does not cover every immutable file");
  const planById = new Map(plans.map((plan) => [String(plan.id), plan]));
  for (const fact of snapshot.facts?.plans || []) {
    const detail = planById.get(String(fact.id));
    if (detail && JSON.stringify(fact) !== JSON.stringify(detail.boundaryFacts))
      errors.push(`${fact.id}: bootstrap and detailed facts drifted`);
    if (fact.metrics || fact.intervals)
      errors.push(`${fact.id}: bootstrap publishes derived analytics`);
  }
}

function validateSelection(selection) {
  const start = new Date(selection?.startAt);
  const metricStart = new Date(selection?.metricStartAt || selection?.startAt);
  const end = new Date(selection?.endAt);
  if (
    Number.isNaN(start.getTime()) ||
    Number.isNaN(metricStart.getTime()) ||
    Number.isNaN(end.getTime()) ||
    start > metricStart ||
    metricStart >= end
  )
    errors.push("Portfolio selection window is invalid");
}

function validatePlan(plan, selection, hydratePlan) {
  if (plan.schemaVersion !== 2) errors.push(`${plan.id}: outdated plan schema`);
  if (plan.metrics || plan.intervals)
    errors.push(`${plan.id}: detailed plan publishes derived analytics`);
  if (String(plan.id) !== String(plan.boundaryFacts?.id))
    errors.push(`${plan.id}: boundary fact identity drifted`);
  const start = new Date(selection.startAt);
  const end = new Date(selection.endAt);
  if (new Date(plan.range.start) < start || new Date(plan.range.end) > end)
    errors.push(`${plan.id}: plan range falls outside cohort`);
  if (
    plan.correlation?.policyVersion !== 1 ||
    plan.correlation?.preflight !== "passed"
  )
    errors.push(`${plan.id}: core correlation is missing`);
  const eventIds = new Set();
  for (const event of plan.events || []) {
    if (eventIds.has(event.id)) errors.push(`${plan.id}: duplicate event ${event.id}`);
    eventIds.add(event.id);
    if (!validDate(event.occurredAt))
      errors.push(`${plan.id}: invalid event timestamp ${event.id}`);
    if (!["authoritative", "observed", "inferred"].includes(event.confidence))
      errors.push(`${plan.id}: invalid confidence on ${event.id}`);
  }
  const planIds = new Set([String(plan.id), String(plan.releasePlanId)]);
  for (const link of plan.links || []) {
    if (link.role === "spec-pr" || link.role === "spec-pr-history") continue;
    if (
      link.releasePlanIds?.length &&
      !link.releasePlanIds.some((id) => planIds.has(String(id)))
    )
      errors.push(`${plan.id}: PR ${link.artifactId} names another release plan`);
  }
  for (const boundary of [
    ...(plan.boundaryFacts?.specPrs || []).flatMap((pr) => [
      pr.createdAt,
      pr.mergedAt,
      pr.closedAt,
    ]),
    ...(plan.boundaryFacts?.artifacts || []).flatMap((artifact) => [
      artifact.prCreatedAt,
      artifact.prMergedAt,
      artifact.prClosedAt,
      artifact.generationStartAt,
      ...(artifact.releaseEvents || []).map((event) => event.occurredAt),
    ]),
  ].filter(Boolean)) {
    if (!validDate(boundary))
      errors.push(`${plan.id}: invalid analytical boundary ${boundary}`);
  }
  for (const artifact of plan.boundaryFacts?.artifacts || []) {
    if (!Array.isArray(artifact.releaseEvents))
      errors.push(`${plan.id}: release-event facts are missing`);
    if (!Array.isArray(artifact.prAttempts))
      errors.push(`${plan.id}: SDK PR attempt history is missing`);
    for (const attempt of artifact.prAttempts || []) {
      for (const boundary of [
        attempt.createdAt,
        attempt.mergedAt,
        attempt.closedAt,
      ].filter(Boolean)) {
        if (!validDate(boundary))
          errors.push(`${plan.id}: invalid SDK PR attempt boundary ${boundary}`);
      }
    }
    if (
      artifact.releaseBoundarySource &&
      !["release-plan", "github-release", "missing"].includes(
        artifact.releaseBoundarySource,
      )
    )
      errors.push(`${plan.id}: invalid release-boundary source`);
    if (
      artifact.releaseBoundarySource === "github-release" &&
      (!validDate(artifact.releaseBoundaryAt) ||
        !/^https:\/\/github\.com\/[^/]+\/[^/]+\/releases\/tag\//.test(
          artifact.releaseBoundaryUrl || "",
        ))
    )
      errors.push(`${plan.id}: invalid GitHub Release boundary`);
  }
  const hydrated = hydratePlan(plan);
  for (const metric of hydrated.metrics) {
    if (metric.outcome !== "complete" && metric.value !== null)
      errors.push(`${plan.id}: non-complete metric ${metric.metricId} has value`);
    if (metric.outcome === "complete" && metric.value < 0)
      errors.push(`${plan.id}: negative metric ${metric.metricId}`);
  }
  for (const interval of hydrated.intervals) {
    if (
      interval.startAt &&
      interval.endAt &&
      new Date(interval.endAt) < new Date(interval.startAt)
    )
      errors.push(`${plan.id}: negative interval ${interval.id}`);
  }
}

function validateCalculated(calculated, snapshot) {
  const scorecard = calculated.scorecard;
  const eligible = snapshot.facts.plans.filter(
    (plan) => plan.state === "finished",
  ).length;
  if (
    scorecard.cohort.generatedAt !== new Date(snapshot.generatedAt).toISOString() ||
    scorecard.cohort.eligiblePlanCount !== eligible
  )
    errors.push("Scorecard cohort is not anchored to the snapshot");
  const l1Population = scorecard.metrics.find(
    (metric) => metric.metricId === "L1",
  )?.population;
  const l1Coverage = snapshot.cohortAccounting?.metricCoverage?.L1;
  if (
    !l1Population ||
    !l1Coverage ||
    l1Coverage.complete !== l1Population.included ||
    l1Coverage.incomplete !== l1Population.incomplete ||
    l1Coverage.ineligible !== l1Population.ineligible ||
    (l1Coverage.excluded || 0) !== l1Population.excluded
  )
    errors.push("Published L1 coverage does not reconcile with the engine");
  for (const metric of scorecard.metrics) {
    const population = metric.population;
    if (
      population.included +
        population.incomplete +
        population.censored +
        population.excluded +
        population.ineligible <
      population.eligible
    )
      errors.push(`${metric.metricId}: population does not reconcile`);
    for (const period of Object.values(metric.periodStatistics)) {
      if (
        !period.change ||
        !("percent" in period.change) ||
        !period.p90Change ||
        !("percent" in period.p90Change)
      )
        errors.push(`${metric.metricId}: period comparison is missing`);
    }
    for (const cadence of ["weekly", "monthly"]) {
      const series = metric.trends[cadence].series;
      for (let index = 0; index < series.length; index += 1) {
        const bucket = series[index];
        if (new Date(bucket.start) >= new Date(bucket.end))
          errors.push(`${metric.metricId}: invalid half-open ${cadence} bucket`);
        if (index && series[index - 1].end !== bucket.start)
          errors.push(`${metric.metricId}: non-contiguous ${cadence} buckets`);
        if (!bucket.count && (bucket.p50 !== null || bucket.p90 !== null))
          errors.push(`${metric.metricId}: empty bucket has statistics`);
      }
      if (
        metric.metricId === "L1" &&
        metric.periodStatistics.rolling90Days?.p90Change?.outcome !==
          "insufficient-data"
      )
        errors.push("L1 rolling 90-day P90 must not publish a comparison");
    }
  }
}

function validateFixtures(engine) {
  if (engine.percentile([0, 10], 0.9) !== 9)
    errors.push("Percentile interpolation fixture failed");
  if (
    engine.shiftUtcMonthClamped("2024-03-31T12:00:00.000Z", -1).toISOString() !==
    "2024-02-29T12:00:00.000Z"
  )
    errors.push("Clamped month subtraction fixture failed");
  const fact = {
    id: "fixture",
    state: "finished",
    cohortPlan: {
      id: "fixture",
      releasePlanId: "fixture",
      title: "Fixture",
      service: "Fixture",
      state: "finished",
    },
    quality: { warnings: [] },
    specPrs: [
      {
        id: "spec",
        trackId: "spec:spec",
        createdAt: "2024-01-01T00:00:00.000Z",
        mergedAt: "2024-01-02T00:00:00.000Z",
        closedAt: null,
      },
    ],
    artifacts: [],
  };
  const scorecard = engine.buildScorecard(
    [fact],
    "2024-03-31T12:00:00.000Z",
  );
  const l1 = scorecard.metrics.find((metric) => metric.metricId === "L1");
  if (l1.population.ineligible !== 1 || l1.population.included !== 0)
    errors.push("Ineligible-outcome fixture failed");
  if (l1.trends.weekly.series.length !== 13)
    errors.push("Empty-bucket trend fixture failed");
  const activeFact = {
    ...fact,
    id: "active-fixture",
    state: "in-progress",
    cohortPlan: {
      ...fact.cohortPlan,
      id: "active-fixture",
      releasePlanId: "active-fixture",
      state: "in-progress",
    },
    specPrs: [
      {
        ...fact.specPrs[0],
        id: "active-spec",
        trackId: "spec:active-spec",
        mergedAt: "2024-02-01T00:00:00.000Z",
      },
    ],
  };
  const finishedOnlyScorecard = engine.buildScorecard(
    [fact, activeFact],
    "2024-03-31T12:00:00.000Z",
  );
  const finishedOnlyS1 = finishedOnlyScorecard.metrics.find(
    (metric) => metric.metricId === "S1",
  );
  if (
    finishedOnlyScorecard.cohort.eligiblePlanCount !== 1 ||
    finishedOnlyScorecard.cohort.totalPlanCount !== 2 ||
    finishedOnlyS1.population.included !== 1
  )
    errors.push("Finished-only scorecard cohort fixture failed");
  const releaseFact = {
    ...fact,
    artifacts: [
      {
        language: "Java",
        package: "fixture",
        trackId: "sdk:Java:fixture",
        prId: "sdk",
        prCreatedAt: "2024-01-02T00:00:00.000Z",
        prMergedAt: "2024-01-03T00:00:00.000Z",
        prClosedAt: null,
        generationStartAt: "2024-01-01T12:00:00.000Z",
        releasedVersion: null,
        releaseEvents: [
          {
            occurredAt: "2024-01-05T00:00:00.000Z",
            value: "Released",
          },
        ],
      },
    ],
  };
  const unreleased = engine.derivePlanAnalytics(releaseFact);
  if (
    unreleased.metrics.find((metric) => metric.metricId === "S5").outcome !==
    "incomplete"
  )
    errors.push("Release-version evidence fixture failed");
  releaseFact.artifacts[0].releasedVersion = "1.0.0";
  const released = engine.derivePlanAnalytics(releaseFact);
  if (
    released.metrics.find((metric) => metric.metricId === "S5").value !== 48
  )
    errors.push("Dynamic release-boundary fixture failed");
  releaseFact.artifacts[0].prAttempts = [
    {
      id: "sdk-first",
      createdAt: "2024-01-01T18:00:00.000Z",
      mergedAt: null,
      closedAt: "2024-01-02T12:00:00.000Z",
    },
    {
      id: "sdk-final",
      createdAt: "2024-01-02T12:00:00.000Z",
      mergedAt: "2024-01-03T00:00:00.000Z",
      closedAt: "2024-01-03T00:00:00.000Z",
    },
  ];
  const sdkDelivery = engine
    .derivePlanAnalytics(releaseFact)
    .metrics.find((metric) => metric.metricId === "S4");
  if (sdkDelivery.value !== 30 || sdkDelivery.definitionVersion !== 2)
    errors.push("Cross-attempt S4 delivery-cycle fixture failed");
  releaseFact.artifacts[0].prMergedAt = null;
  const activeReplacement = engine
    .derivePlanAnalytics(releaseFact)
    .metrics.find((metric) => metric.metricId === "S4");
  if (activeReplacement.outcome !== "incomplete")
    errors.push("Open replacement S4 delivery-cycle fixture failed");
}

function listFiles(directory, root = directory) {
  if (!existsSync(directory)) return [];
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    return statSync(path).isDirectory()
      ? listFiles(path, root)
      : [path.slice(root.length + 1)];
  });
}

function validDate(value) {
  return Boolean(value) && !Number.isNaN(new Date(value).getTime());
}

function scanPublishedFiles(directory) {
  for (const name of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, name.name);
    if (name.isDirectory()) scanPublishedFiles(path);
    else if (name.name.endsWith(".json")) scanPublishedFile(path);
  }
}

function scanPublishedFile(path) {
  const text = readFileSync(path, "utf8");
  if (/\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b/i.test(text))
    errors.push(`${path}: contains an email address`);
  if (/gh[pousr]_[A-Za-z0-9]{20,}|Bearer\s+[A-Za-z0-9._-]{20,}/.test(text))
    errors.push(`${path}: contains a credential-like value`);
}
