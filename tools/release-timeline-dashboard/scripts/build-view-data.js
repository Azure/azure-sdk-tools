#!/usr/bin/env node

const { createHash } = require("node:crypto");
const { execFileSync } = require("node:child_process");
const {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  renameSync,
  statSync,
  unlinkSync,
} = require("node:fs");
const { join } = require("node:path");
const {
  parseArgs,
  readJson,
  writeJson,
} = require("./lib/v2-common");
const { POLICY_VERSION } = require("./lib/instrumentation-compliance");

const args = parseArgs(process.argv.slice(2), {
  plans: "cache/v2/release-plans.json",
  github: "cache/v2/github-prs.json",
  releases: "cache/v2/github-releases.json",
  pipelines: "cache/v2/pipeline-runs.json",
  output: "data",
  buildId: new Date().toISOString().replace(/[-:]/g, "").slice(0, 13),
});

main();

function main() {
  const source = readJson(args.plans);
  const github = readJson(args.github);
  const githubReleases = readJson(args.releases);
  const pipelineSource = readJson(args.pipelines);
  const prMap = new Map(github.prs.map((pr) => [pr.id, pr]));
  const releaseMap = new Map(
    githubReleases.matches.map((match) => [match.id, match]),
  );
  const pipelineRuns = pipelineSource.runs;
  const buildRoot = join(args.output, "builds", args.buildId);
  if (existsSync(buildRoot)) {
    throw new Error(
      `Build ${args.buildId} already exists. Use a new immutable build ID.`,
    );
  }
  mkdirSync(buildRoot, { recursive: true });

  const window = selectionWindow(source);
  const attributedSourcePlans = source.plans.map((plan) =>
    filterPlanPrAttribution(plan, prMap),
  );
  const candidatePlans = attributedSourcePlans.map((plan) =>
    buildPlan(plan, prMap, pipelineRuns, releaseMap, window.end.toISOString()),
  );
  const plans = candidatePlans.filter(
    (plan) => new Date(plan.range.end) <= window.end,
  );
  const includedPlanIds = new Set(plans.map((plan) => plan.id));
  const outOfWindowPlans = candidatePlans
    .filter((plan) => !includedPlanIds.has(plan.id))
    .map((plan) => ({
      id: plan.id,
      title: plan.title,
      sourceUrl: plan.sourceUrl,
      stage: "flow-window",
      reason: `last touch ${plan.range.end} exceeds ${window.end.toISOString()}`,
    }));
  const includedSourcePlans = attributedSourcePlans.filter((plan) =>
    includedPlanIds.has(plan.id),
  );
  const publishedGithub = filterGithubSource(github, includedSourcePlans);
  const publishedPipelines = filterPipelineSource(
    pipelineSource,
    includedPlanIds,
  );
  const buildSource = {
    ...source,
    plans: includedSourcePlans,
    selection: {
      ...source.selection,
      startAt: new Date(
        Math.min(window.start, ...plans.map((plan) => new Date(plan.range.start))),
      ).toISOString(),
      metricStartAt: window.start.toISOString(),
      endAt: window.end.toISOString(),
      criteria: `core-correlated management-plane plans changed since ${window.start.toISOString()}; metrics include completions on or after that boundary`,
      flowCandidateCount: source.plans.length,
      outOfWindowCount: outOfWindowPlans.length,
      publishedCount: plans.length,
    },
    skippedPlans: [...(source.skippedPlans || []), ...outOfWindowPlans],
  };
  const portfolio = buildPortfolio(
    plans,
    buildSource,
    publishedGithub,
    publishedPipelines,
  );
  for (const plan of plans) {
    writeJson(join(buildRoot, "plans", `${plan.id}.json`), plan);
  }

  const hashes = hashFiles(buildRoot, buildRoot);
  const snapshot = {
    schemaVersion: 2,
    dataSchemaVersion: 2,
    snapshotId: args.buildId,
    generatedAt: source.generatedAt,
    redactionPolicyVersion: 1,
    cadence: "daily",
    counts: {
      plans: plans.length,
      pullRequests: publishedGithub.prCount,
      pipelineRuns: publishedPipelines.runCount,
      events: plans.reduce((sum, plan) => sum + plan.events.length, 0),
      services: new Set(plans.map((plan) => plan.service)).size,
      skippedPlans: buildSource.skippedPlans?.length || 0,
      skippedPullRequests: publishedGithub.skippedPrCount,
      skippedPipelineRuns: publishedPipelines.skippedRunCount,
    },
    selection: buildSource.selection,
    sourceCoverage: portfolio.dataQuality,
    cohortAccounting: buildCohortAccounting(
      plans,
      buildSource,
      candidatePlans,
    ),
    portfolio,
    facts: {
      plans: plans.map((plan) => plan.boundaryFacts),
    },
    paths: {
      plan: `builds/${args.buildId}/plans/{id}.json`,
    },
    hashes,
  };
  const candidatePath = join(
    args.output,
    `.snapshot-${args.buildId}-${process.pid}.json`,
  );
  writeJson(candidatePath, snapshot);
  try {
    execFileSync(
      process.execPath,
      [
        join(__dirname, "validate-data.js"),
        "--data",
        args.output,
        "--snapshot",
        candidatePath,
      ],
      { stdio: "inherit" },
    );
    renameSync(candidatePath, join(args.output, "snapshot.json"));
  } catch (error) {
    if (existsSync(candidatePath)) unlinkSync(candidatePath);
    throw error;
  }
  console.log(
    `Built snapshot ${args.buildId} with ${plans.length} plans and ${snapshot.counts.events} events`,
  );
}

function filterPlanPrAttribution(plan, prMap) {
  const planIds = new Set([String(plan.id), String(plan.releasePlanId)]);
  const planCreatedAt = new Date(plan.createdAt);
  const hasMatchingIdentity = (linked) => {
    const ids = prMap.get(linked.id)?.releasePlanIds || [];
    return ids.length === 0 || ids.some((id) => planIds.has(String(id)));
  };
  const isAttributedHistory = (linked, index, history) => {
    if (!hasMatchingIdentity(linked)) return false;
    const pr = prMap.get(linked.id);
    if ((pr?.releasePlanIds || []).length > 0) return true;
    if (!pr?.mergedAt || new Date(pr.mergedAt) >= planCreatedAt) return true;
    return !history.slice(index + 1).some((replacement) => {
      const replacementPr = prMap.get(replacement.id);
      return (
        replacementPr?.createdAt &&
        new Date(replacementPr.createdAt) >= planCreatedAt
      );
    });
  };
  const retainedSpecPrs = plan.specPrs;
  const retainedLanguages = plan.languages.map((language) => {
    const history = language.sdkPrHistory || [];
    const sdkPrHistory = history.filter(isAttributedHistory);
    return {
      ...language,
      sdkPr:
        language.sdkPr && hasMatchingIdentity(language.sdkPr)
          ? language.sdkPr
          : null,
      sdkPrHistory,
    };
  });
  const retainedPrUrls = new Set([
    ...retainedSpecPrs.map((pr) => pr.url),
    ...retainedLanguages.flatMap((language) =>
      [
        language.sdkPr?.url,
        ...language.sdkPrHistory.map((pr) => pr.url),
      ].filter(Boolean),
    ),
  ]);
  return {
    ...plan,
    specPrs: retainedSpecPrs,
    languages: retainedLanguages,
    revisionEvents: plan.revisionEvents.filter(
      (event) =>
        event.type !== "sdk.pr_linked" || retainedPrUrls.has(event.value),
    ),
    specRevisionEvents: plan.specRevisionEvents,
  };
}

function selectionWindow(source) {
  const end = new Date(source.selection?.endAt || source.generatedAt);
  const start = source.selection?.startAt
    ? new Date(source.selection.startAt)
    : new Date(end.getTime() - Number(source.selection?.days || 0) * 86_400_000);
  if (
    Number.isNaN(start.getTime()) ||
    Number.isNaN(end.getTime()) ||
    start >= end
  )
    throw new Error("The source selection window is invalid");
  return { start, end };
}

function filterGithubSource(github, plans) {
  const ids = new Set(
    plans.flatMap((plan) => [
      ...plan.specPrs.map((pr) => pr.id),
      ...plan.languages.flatMap((language) =>
        (language.sdkPrHistory || []).map((pr) => pr.id),
      ),
    ]),
  );
  const prs = github.prs.filter((pr) => ids.has(pr.id));
  const skippedPrs = (github.skippedPrs || []).filter((pr) => ids.has(pr.id));
  return {
    ...github,
    prCount: prs.length,
    skippedPrCount: skippedPrs.length,
    skippedPrs,
    prs,
  };
}

function filterPipelineSource(source, includedPlanIds) {
  const runs = source.runs
    .filter((run) =>
      run.references.some((reference) =>
        includedPlanIds.has(reference.planId),
      ),
    )
    .map((run) => ({
      ...run,
      references: run.references.filter((reference) =>
        includedPlanIds.has(reference.planId),
      ),
    }));
  const runIds = new Set(runs.map((run) => run.id));
  const skippedRuns = (source.skippedRuns || []).filter((run) =>
    runIds.has(run.id),
  );
  return {
    ...source,
    runCount: runs.length,
    skippedRunCount: skippedRuns.length,
    skippedRuns,
    runs,
  };
}

function buildCohortAccounting(plans, source, candidatePlans) {
  const finished = plans.filter((plan) => plan.state === "finished");
  const metricStart = new Date(
    source.selection.metricStartAt || source.selection.startAt,
  );
  const finalReleaseAt = (plan) => {
    const boundaries = plan.boundaryFacts.artifacts
      .map((artifact) => artifact.releaseBoundaryAt)
      .filter(Boolean)
      .sort();
    return boundaries.length === plan.boundaryFacts.artifacts.length
      ? boundaries.at(-1)
      : null;
  };
  const prePeriodL1 = finished.filter((plan) => {
    const completedAt = finalReleaseAt(plan);
    return completedAt && new Date(completedAt) < metricStart;
  });
  const prePeriodIds = new Set(prePeriodL1.map((plan) => plan.id));
  const completeL1 = finished.filter(
    (plan) =>
      !prePeriodIds.has(plan.id) &&
      plan.boundaryFacts.specPrs.some((pr) => pr.createdAt) &&
      plan.boundaryFacts.artifacts.length > 0 &&
      plan.boundaryFacts.artifacts.every((artifact) => artifact.releaseBoundaryAt),
  );
  const ineligibleL1 = finished.filter(
    (plan) => plan.boundaryFacts.artifacts.length === 0,
  );
  const completeIds = new Set(completeL1.map((plan) => plan.id));
  const ineligibleIds = new Set(ineligibleL1.map((plan) => plan.id));
  const incompleteL1 = finished.filter(
    (plan) =>
      !completeIds.has(plan.id) &&
      !ineligibleIds.has(plan.id) &&
      !prePeriodIds.has(plan.id),
  );
  const boundarySources = {};
  for (const plan of plans) {
    for (const artifact of plan.boundaryFacts.artifacts) {
      const sourceName = artifact.releaseBoundarySource || "missing";
      boundarySources[sourceName] = (boundarySources[sourceName] || 0) + 1;
    }
  }
  const planLink = (plan, reason = null) => ({
    id: plan.id,
    releasePlanId: plan.releasePlanId || null,
    title: plan.title,
    service: plan.service || plan.title,
    sourceUrl:
      plan.sourceUrl ||
      `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${plan.id}`,
    reason,
  });
  return {
    inventory: {
      releasePlans: source.selection.inventoryCount,
      managementPlane: source.selection.managementCount,
      coreCorrelated: source.selection.candidateCount,
      flowCandidates: candidatePlans.length,
      published: plans.length,
      excluded: (source.skippedPlans || []).length,
      overflow:
        (source.selection.lookbackOverflowCount || 0) +
        (source.selection.outOfWindowCount || 0),
      preflightExcluded: source.selection.preflightSkippedCount || 0,
    },
    states: {
      finished: finished.length,
      active: plans.filter((plan) => ["new", "in-progress"].includes(plan.state))
        .length,
      abandoned: plans.filter((plan) =>
        ["abandoned", "duplicate"].includes(plan.state),
      ).length,
    },
    metricCoverage: {
      L1: {
        eligible: finished.length - ineligibleL1.length,
        complete: completeL1.length,
        incomplete: incompleteL1.length,
        ineligible: ineligibleL1.length,
        excluded: prePeriodL1.length,
        incompletePlans: incompleteL1.map((plan) =>
          planLink(plan, "missing full release boundary"),
        ),
        ineligiblePlans: ineligibleL1.map((plan) =>
          planLink(plan, "no intended SDK artifacts"),
        ),
        excludedPlans: prePeriodL1.map((plan) =>
          planLink(plan, "completed before metric reporting period"),
        ),
      },
    },
    boundarySources,
    exclusions: (source.skippedPlans || []).map((plan) => ({
      id: plan.id,
      title: plan.title,
      stage: plan.stage,
      reason: plan.reason,
      sourceUrl:
        plan.sourceUrl ||
        `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${plan.id}`,
    })),
  };
}

function buildPlan(source, prMap, pipelineRuns, releaseMap, generatedAt) {
  const runs = pipelineRuns.filter((run) =>
    run.references.some((reference) => reference.planId === source.id),
  );
  const specPrIds = new Set(source.specPrs.map((pr) => pr.id));
  const tracks = [
    {
      id: "plan",
      kind: "plan",
      label: "Release Plan",
      detail: source.releaseType,
    },
    ...source.specPrs.map((linked, index) => {
      const pr = prMap.get(linked.id);
      return {
        id: `spec:${linked.id}`,
        kind: "spec",
        label: source.specPrs.length > 1 ? `Spec PR ${index + 1}` : "Spec PR",
        detail: pr ? `${pr.repo} #${pr.number}` : linked.id,
        artifactId: linked.id,
        state: pr?.state || "missing",
      };
    }),
    ...source.languages.map((language) => {
      const primary = selectSdkPr(language, prMap, source.state);
      return {
        id: trackId(language),
        kind: "sdk",
        label: language.id,
        detail: language.package || "Package not recorded",
        artifactId: primary?.id || null,
        state: primary?.state || "missing",
      };
    }),
  ];

  const events = [
    {
      id: `ado:${source.id}:created`,
      type: "plan.created",
      phase: "plan",
      occurredAt: source.createdAt,
      observedAt: source.createdAt,
      trackId: "plan",
      title: "Release Plan created",
      confidence: "authoritative",
      source: {
        system: "azure-devops",
        entity: `work-item:${source.id}`,
        url: source.adoUrl,
      },
    },
    ...source.revisionEvents.map((event) => ({
      ...event,
      title: eventTitle(event),
    })),
    ...source.specRevisionEvents.map((event) => ({
      ...event,
      trackId: findSpecTrack(event.value, source.specPrs),
      title: eventTitle(event),
    })),
  ];

  for (const linked of source.specPrs) {
    const pr = prMap.get(linked.id);
    if (pr)
      events.push(...githubEvents(pr, `spec:${linked.id}`, "spec-review"));
  }
  for (const language of source.languages) {
    for (const linked of language.sdkPrHistory || []) {
      const pr = prMap.get(linked.id);
      if (pr) events.push(...githubEvents(pr, trackId(language), "sdk-review"));
    }
  }
  for (const run of runs) events.push(...pipelineEvents(run, source));
  for (const language of source.languages) {
    const release = releaseMap.get(`${source.id}:${language.id}`);
    if (release?.status === "matched")
      events.push(githubReleaseEvent(release, trackId(language)));
  }

  const uniqueEvents = dedupeEvents(events)
    .filter((event) => validDate(event.occurredAt))
    .sort((left, right) => new Date(left.occurredAt) - new Date(right.occurredAt));
  const boundaryFacts = buildBoundaryFacts(
    source,
    prMap,
    uniqueEvents,
    runs,
    releaseMap,
  );
  const quality = qualitySummary(source, prMap, runs);
  const flowStart = new Date(
    Math.min(
      new Date(source.createdAt),
      ...boundaryFacts.specPrs
        .map((pr) => pr.createdAt)
        .filter(Boolean)
        .map((value) => new Date(value)),
      ...boundaryFacts.artifacts
        .flatMap((artifact) => [
          artifact.generationStartAt,
          ...(artifact.prAttempts || []).map((attempt) => attempt.createdAt),
        ])
        .filter(Boolean)
        .map((value) => new Date(value)),
    ),
  );
  const publishedEvents = assignEventStacks(
    uniqueEvents.filter((event) => new Date(event.occurredAt) >= flowStart),
  );
  const eventTimes = publishedEvents.map((event) => new Date(event.occurredAt));
  const state = normalizeState(source.state);
  const terminal = ["finished", "abandoned", "duplicate"].includes(state);
  const terminalStateEvent = [...source.revisionEvents]
    .reverse()
    .find(
      (event) =>
        event.type === "plan.state_changed" &&
        normalizeState(event.value) === state,
    );
  const effectiveEnd = terminal
    ? terminalStateEvent?.occurredAt || source.changedAt
    : generatedAt;

  return {
    schemaVersion: 2,
    id: source.id,
    correlation: {
      policyVersion: source.correlation?.policyVersion || POLICY_VERSION,
      preflight: source.correlation?.preflight || "passed",
    },
    releasePlanId: source.releasePlanId,
    title: source.title,
    service: serviceName(source),
    product: source.product,
    path: source.path || null,
    state,
    plane: source.plane,
    releaseType: source.releaseType,
    intendedMonth: source.intendedMonth,
    createdAt: source.createdAt,
    completedAt: terminal ? effectiveEnd : null,
    sourceUrl: source.adoUrl,
    range: {
      start: new Date(Math.min(...eventTimes)).toISOString(),
      end: new Date(
        Math.max(...eventTimes, new Date(effectiveEnd)),
      ).toISOString(),
    },
    intendedArtifacts: source.languages.map((language) => ({
      ...(() => {
        const primary = selectSdkPr(language, prMap, source.state);
        return { prId: primary?.id || null };
      })(),
      language: language.id,
      package: language.package || null,
      version: language.releasedVersion,
      outcome: releaseOutcome(language, state),
      generationPipelineUrl: language.generationPipelineUrl,
      releasePipelineUrl: language.releasePipelineUrl,
    })),
    links: [
      ...source.specPrs.map((pr, index) => ({
        artifactId: pr.id,
        role: index === source.specPrs.length - 1 ? "spec-pr" : "spec-pr-history",
        url: pr.url,
        releasePlanIds: prMap.get(pr.id)?.releasePlanIds || [],
      })),
      ...source.languages.flatMap((language) => {
        const primary = selectSdkPr(language, prMap, source.state);
        return (language.sdkPrHistory || []).map((pr) => ({
          artifactId: pr.id,
          role: pr.id === primary?.id ? "sdk-pr" : "sdk-pr-history",
          language: language.id,
          url: pr.url,
          releasePlanIds: prMap.get(pr.id)?.releasePlanIds || [],
        }));
      }),
    ].filter((link) => link.artifactId),
    tracks,
    events: publishedEvents,
    quality,
    boundaryFacts: {
      ...boundaryFacts,
      cohortPlan: {
        id: source.id,
        releasePlanId: source.releasePlanId,
        title: source.title,
        service: serviceName(source),
        state,
      },
      quality,
    },
    summary: {
      durationHours: hours(source.createdAt, effectiveEnd),
      pullRequestCount: new Set([
        ...specPrIds,
        ...source.languages.flatMap((language) =>
          (language.sdkPrHistory || []).map((pr) => pr.id),
        ),
      ]).size,
      humanReviewCount: publishedEvents.filter(
        (event) =>
          event.type === "review.submitted" && event.actor?.kind === "human",
      ).length,
      humanCommentCount: publishedEvents.filter(
        (event) =>
          event.type === "comment.created" && event.actor?.kind === "human",
      ).length,
    },
  };
}

function githubEvents(pr, track, phase) {
  if (pr.state === "unavailable") return [];
  const source = {
    system: "github",
    entity: `${pr.owner}/${pr.repo}#${pr.number}`,
    url: pr.url,
  };
  const values = [
    githubEvent(pr, track, phase, "pr.created", "PR opened", pr.createdAt, source),
  ];
  if (pr.mergedAt)
    values.push(
      githubEvent(pr, track, phase, "pr.merged", "PR merged", pr.mergedAt, source),
    );
  else if (pr.closedAt)
    values.push(
      githubEvent(pr, track, phase, "pr.closed", "PR closed", pr.closedAt, source),
    );
  for (const review of pr.reviews) {
    if (!review.submittedAt) continue;
    values.push({
      ...githubEvent(
        pr,
        track,
        phase,
        "review.submitted",
        review.state === "CHANGES_REQUESTED"
          ? "Changes requested"
          : review.state === "APPROVED"
            ? "Review approved"
            : "Review submitted",
        review.submittedAt,
        { ...source, url: review.url || pr.url },
        `review:${review.id}`,
      ),
      actor: review.actor,
      excerpt: redact(review.excerpt),
      reviewState: review.state.toLowerCase(),
    });
  }
  for (const comment of [...pr.issueComments, ...pr.reviewComments]) {
    values.push({
      ...githubEvent(
        pr,
        track,
        phase,
        "comment.created",
        "Comment",
        comment.createdAt,
        { ...source, url: comment.url || pr.url },
        `comment:${comment.id}`,
      ),
      actor: comment.actor,
      excerpt: redact(comment.excerpt),
    });
  }
  for (const commit of pr.commits) {
    if (!commit.createdAt) continue;
    values.push({
      ...githubEvent(
        pr,
        track,
        phase,
        "commit.pushed",
        "Commit pushed",
        commit.createdAt,
        { ...source, url: commit.url || pr.url },
        `commit:${commit.id.slice(0, 12)}`,
      ),
      actor: commit.actor,
    });
  }
  return values;
}

function pipelineEvents(run, plan) {
  return run.references
    .filter((reference) => reference.planId === plan.id)
    .flatMap((reference) => {
      const language = plan.languages.find(
        (item) => item.id === reference.language,
      );
      if (!language) return [];
      const track = trackId(language);
      const phase =
        reference.role === "generation" ? "generation" : "release";
      const prefix =
        reference.role === "generation" ? "generation" : "release";
      const source = {
        system: "azure-pipelines",
        entity: `${run.project} build ${run.buildId}`,
        url: run.url,
      };
      const values = [];
      if (run.queueAt)
        values.push(
          pipelineEvent(
            run,
            track,
            phase,
            `${prefix}.queued`,
            "Pipeline queued",
            run.queueAt,
            source,
          ),
        );
      if (run.startAt)
        values.push(
          pipelineEvent(
            run,
            track,
            phase,
            `${prefix}.started`,
            "Pipeline started",
            run.startAt,
            source,
          ),
        );
      if (run.finishAt) {
        const completed = ["succeeded", "partiallySucceeded"].includes(
          run.result,
        );
        values.push(
          pipelineEvent(
            run,
            track,
            phase,
            completed
              ? `${prefix}.completed`
              : `${prefix}.failed`,
            completed
              ? run.result === "partiallySucceeded"
                ? "Pipeline completed with issues"
                : "Pipeline completed"
              : "Pipeline failed",
            run.finishAt,
            source,
          ),
        );
      }
      for (const failure of run.failures) {
        if (!failure.finishAt && !failure.startAt) continue;
        values.push({
          ...pipelineEvent(
            run,
            track,
            phase,
            `${prefix}.stage_failed`,
            `${failure.type} failed: ${failure.name}`,
            failure.finishAt || failure.startAt,
            source,
            failure.id,
          ),
          value: failure.result,
        });
      }
      return values;
    });
}

function pipelineEvent(
  run,
  trackIdValue,
  phase,
  type,
  title,
  occurredAt,
  source,
  suffix = type,
) {
  return {
    id: `${run.id}:${suffix}:${trackIdValue}`,
    type,
    phase,
    occurredAt,
    observedAt: run.finishAt || occurredAt,
    trackId: trackIdValue,
    title,
    actor: { kind: "service", publicId: null },
    source,
    confidence: "authoritative",
  };
}

function githubReleaseEvent(release, trackIdValue) {
  return {
    id: `github-release:${release.planId}:${release.language}:${release.tagName}`,
    type: "package.github_release_published",
    phase: "release",
    occurredAt: release.publishedAt,
    observedAt: release.publishedAt,
    trackId: trackIdValue,
    title: "GitHub Release published",
    value: release.tagName,
    actor: { kind: "service", publicId: null },
    source: {
      system: "github",
      entity: `${release.repository} release ${release.tagName}`,
      url: release.url,
    },
    confidence: release.confidence === "inferred" ? "inferred" : "observed",
  };
}

function githubEvent(
  pr,
  trackIdValue,
  phase,
  type,
  title,
  occurredAt,
  source,
  suffix = type,
) {
  return {
    id: `${pr.id}:${suffix}:${trackIdValue}`,
    type,
    phase,
    occurredAt,
    observedAt: occurredAt,
    trackId: trackIdValue,
    title,
    actor: type === "pr.created" ? pr.author : undefined,
    source,
    confidence: "authoritative",
  };
}

function buildBoundaryFacts(source, prMap, events, runs, releaseMap) {
  return {
    id: source.id,
    state: normalizeState(source.state),
    specPrs: source.specPrs.map((linked) => {
      const pr = prMap.get(linked.id);
      return {
        id: linked.id,
        trackId: `spec:${linked.id}`,
        createdAt: pr?.createdAt || null,
        mergedAt: pr?.mergedAt || null,
        closedAt: pr?.closedAt || null,
        available: Boolean(pr && pr.state !== "unavailable"),
      };
    }),
    artifacts: source.languages.map((language) => {
      const pr = selectSdkPr(language, prMap, source.state);
      const prAttempts = (language.sdkPrHistory || [])
        .map((linked) => prMap.get(linked.id))
        .filter(Boolean)
        .map((attempt) => ({
          id: attempt.id,
          createdAt: attempt.createdAt || null,
          mergedAt: attempt.mergedAt || null,
          closedAt: attempt.closedAt || null,
          state: attempt.state,
        }));
      const generationRun = findRun(
        runs,
        source.id,
        language.id,
        "generation",
      );
      const releaseEvents = events
        .filter(
          (event) =>
            event.trackId === trackId(language) &&
            event.type === "release.status_changed",
        )
        .map((event) => ({
          occurredAt: event.occurredAt,
          value: event.value,
        }));
      const observedReleaseAt =
        releaseEvents
          .filter((event) => /released/i.test(event.value))
          .map((event) => event.occurredAt)
          .sort()[0] || null;
      const githubRelease = releaseMap.get(`${source.id}:${language.id}`);
      const fallbackRelease =
        githubRelease?.status === "matched" ? githubRelease : null;
      const releaseBoundaryAt =
        fallbackRelease?.publishedAt ||
        (language.releasedVersion ? observedReleaseAt : null);
      const releaseBoundarySource = fallbackRelease
        ? "github-release"
        : releaseBoundaryAt
          ? "release-plan"
          : "missing";
      const releaseBoundaryConfidence =
        fallbackRelease?.confidence === "inferred"
          ? "inferred"
          : releaseBoundaryAt
            ? "observed"
            : "missing";
      return {
        language: language.id,
        package: language.package || null,
        trackId: trackId(language),
        prId: pr?.id || null,
        prCreatedAt: pr?.createdAt || null,
        prMergedAt: pr?.mergedAt || null,
        prClosedAt: pr?.closedAt || null,
        prAttempts,
        generationRunId: generationRun?.id || null,
        generationStartAt: generationRun?.startAt || null,
        releaseEvents,
        releasedVersion: language.releasedVersion || null,
        releaseBoundaryAt,
        releaseBoundarySource,
        releaseBoundaryConfidence,
        releaseBoundaryUrl: fallbackRelease?.url || null,
        releaseTag: fallbackRelease?.tagName || null,
        releaseMatchMethod: fallbackRelease?.method || null,
        provenance: {
          pr: pr?.state === "unavailable" ? "unavailable" : pr ? "exact" : "missing",
          generation: generationRun ? "exact" : "missing",
          release: releaseBoundarySource,
        },
      };
    }),
  };
}

function qualitySummary(source, prMap, runs) {
  const warnings = [];
  if (!source.path) warnings.push("API specification path is missing");
  if (!source.specPrs.length) warnings.push("No exact spec PR is recorded");
  if (source.specPrs.length > 1)
    warnings.push("Multiple spec PRs are retained in link history");
  const nonMerged = [
    ...source.specPrs,
    ...source.languages.flatMap((language) => language.sdkPrHistory || []),
  ].filter((linked) => prMap.get(linked.id)?.state !== "merged");
  if (nonMerged.length)
    warnings.push(`${nonMerged.length} historical linked PR(s) closed without merge`);
  const replacedSdkPrs = source.languages.reduce(
    (sum, language) => sum + Math.max(0, (language.sdkPrHistory || []).length - 1),
    0,
  );
  if (replacedSdkPrs)
    warnings.push(`${replacedSdkPrs} superseded SDK PR link(s) retained`);
  const unavailablePrs = [
    ...source.specPrs,
    ...source.languages.flatMap((language) => language.sdkPrHistory || []),
  ].filter((linked) => prMap.get(linked.id)?.state === "unavailable");
  if (unavailablePrs.length)
    warnings.push(`${unavailablePrs.length} PR enrichment(s) skipped`);
  const unavailableRuns = runs.filter((run) => run.status === "unavailable");
  if (unavailableRuns.length)
    warnings.push(`${unavailableRuns.length} pipeline run enrichment(s) skipped`);
  return {
    warnings,
    unavailablePullRequests: unavailablePrs.length,
    unavailablePipelineRuns: unavailableRuns.length,
  };
}

function buildPortfolio(plans, source, github, pipelineSource) {
  const warnings = plans.reduce(
    (sum, plan) => sum + plan.quality.warnings.length,
    0,
  );
  return {
    schemaVersion: 2,
    dataQuality: {
      releasePlans: plans.length,
      linkedPullRequests: github.prCount,
      enrichedPullRequests: github.prs.filter(
        (pr) => pr.state !== "unavailable",
      ).length,
      exactPipelineRuns: pipelineSource.runCount,
      exactPrCoveragePercent: github.prCount
        ? Math.round(
            (github.prs.filter((pr) => pr.state !== "unavailable").length /
              github.prCount) *
              100,
          )
        : 100,
      plansWithWarnings: plans.filter((plan) => plan.quality.warnings.length).length,
      warningCount: warnings,
      releaseTimestamps:
        "Observed from Release Plan revision history; exact package timestamps are not yet available.",
      pipelineRuns:
        `${pipelineSource.runCount} exact linked runs are enriched; plans without exact links remain explicitly incomplete.`,
      collectionSkips: `${source.skippedPlans?.length || 0} plans, ${github.skippedPrCount || 0} PRs, and ${pipelineSource.skippedRunCount || 0} pipeline runs were marked and skipped during collection.`,
    },
    plans: plans.map((plan) => ({
      id: plan.id,
      releasePlanId: plan.releasePlanId,
      title: plan.title,
      service: plan.service,
      state: plan.state,
      plane: plan.plane,
      releaseType: plan.releaseType,
      intendedMonth: plan.intendedMonth,
      createdAt: plan.createdAt,
      completedAt: plan.completedAt,
      durationHours: plan.summary.durationHours,
      languageCount: plan.intendedArtifacts.length,
      releasedCount: plan.intendedArtifacts.filter(
        (artifact) => artifact.outcome === "released",
      ).length,
      prCount: plan.summary.pullRequestCount,
      reviewCount: plan.summary.humanReviewCount,
      warnings: plan.quality.warnings.length,
    })),
  };
}

function eventTitle(event) {
  const labels = {
    "plan.state_changed": "Plan state changed",
    "spec.approval_changed": "Spec approval changed",
    "spec.pr_linked": "Spec PR linked",
    "generation.status_changed": "Generation status changed",
    "sdk.pr_linked": "SDK PR linked",
    "release.status_changed": "Release status changed",
    "package.version_observed": "Package version observed",
  };
  return labels[event.type] || event.type;
}

function findSpecTrack(url, specPrs) {
  const match = specPrs.find(
    (pr) => pr.url.toLowerCase() === String(url).toLowerCase(),
  );
  return match ? `spec:${match.id}` : "plan";
}

function trackId(language) {
  return `sdk:${language.id}:${language.package || language.id}`;
}

function selectSdkPr(language, prMap, planState) {
  const values = (language.sdkPrHistory || [])
    .map((linked) => prMap.get(linked.id))
    .filter(Boolean);
  const current = prMap.get(language.sdkPr?.id);
  if (normalizeState(planState) !== "finished")
    return current || values.at(-1) || null;
  const merged = values
    .filter((pr) => pr.mergedAt)
    .sort((left, right) => new Date(right.mergedAt) - new Date(left.mergedAt));
  return merged[0] || current || values.at(-1) || null;
}

function findRun(runs, planId, language, role) {
  return runs.find((run) =>
    run.references.some(
      (reference) =>
        reference.planId === planId &&
        reference.language === language &&
        reference.role === role,
    ),
  );
}

function serviceName(source) {
  const titleName = source.title
    .replace(/^Release plan\s*-\s*\d+\s*-\s*(GA|Preview)\s*-\s*/i, "")
    .replace(/^(Public Preview|GA) release plan for /i, "")
    .trim();
  if (titleName !== source.title) return titleName;
  const pathParts = String(source.path || "").split("/").filter(Boolean);
  const pathName = pathParts.at(-1);
  if (pathName) return pathName.replace(/([a-z])([A-Z])/g, "$1 $2");
  return source.title;
}

function redact(value) {
  return String(value || "")
    .replace(
      /\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b/gi,
      "[email redacted]",
    )
    .slice(0, 240);
}

function dedupeEvents(events) {
  const unique = new Map();
  for (const event of events) {
    if (!unique.has(event.id)) unique.set(event.id, event);
  }
  return [...unique.values()];
}

function assignEventStacks(events) {
  const times = events.map((event) => new Date(event.occurredAt).getTime());
  const threshold = (Math.max(...times) - Math.min(...times)) * 0.012;
  const trackSlots = new Map();
  return events.map((event) => {
    const time = new Date(event.occurredAt).getTime();
    const slots = trackSlots.get(event.trackId) || [];
    let stack = slots.findIndex((lastTime) => time - lastTime > threshold);
    if (stack === -1) {
      stack =
        slots.length < 6
          ? slots.length
          : slots.indexOf(Math.min(...slots));
    }
    slots[stack] = time;
    trackSlots.set(event.trackId, slots);
    return { ...event, stack };
  });
}

function validDate(value) {
  return value && !Number.isNaN(new Date(value).getTime());
}

function hours(start, end) {
  return Math.round(((new Date(end) - new Date(start)) / 3_600_000) * 10) / 10;
}

function earliest(values) {
  return values.length
    ? new Date(Math.min(...values.map((value) => new Date(value)))).toISOString()
    : null;
}

function latest(values) {
  return values.length
    ? new Date(Math.max(...values.map((value) => new Date(value)))).toISOString()
    : null;
}

function normalizeState(value) {
  return String(value || "unknown")
    .trim()
    .toLowerCase()
    .replace(/\s+/g, "-");
}

function releaseOutcome(language, planState) {
  const status = String(language.releaseStatus || "").toLowerCase();
  if (status.includes("released")) return "released";
  if (status.includes("failed")) return "failed";
  if (planState === "abandoned" || planState === "duplicate")
    return "not-released";
  return "pending";
}

function hashFiles(directory, root) {
  const hashes = {};
  for (const entry of readdirSync(directory)) {
    const path = join(directory, entry);
    if (statSync(path).isDirectory()) {
      Object.assign(hashes, hashFiles(path, root));
    } else {
      const relative = path.slice(root.length + 1);
      hashes[relative] = createHash("sha256")
        .update(readFileSync(path))
        .digest("hex");
    }
  }
  return hashes;
}
