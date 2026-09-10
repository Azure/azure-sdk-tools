#!/usr/bin/env node

const {
  LANGUAGES,
  childIds,
  concurrentMap,
  fetchRevisions,
  fetchWorkItems,
  isLanguageApplicable,
  lower,
  parseArgs,
  parsePrUrl,
  plainText,
  queryReleasePlanIds,
  releasePlanQueryWindow,
  writeJson,
} = require("./lib/v2-common");
const {
  POLICY_VERSION,
  assessPreflight,
} = require("./lib/instrumentation-compliance");

const args = parseArgs(process.argv.slice(2), {
  days: 180,
  startAt: "",
  limit: 10,
  concurrency: 6,
  mode: "complete",
  output: "cache/v2/release-plans.json",
});

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});

async function main() {
  if (!["complete", "all-management"].includes(args.mode))
    throw new Error("--mode must be complete or all-management");
  const queryWindow = releasePlanQueryWindow(args.days);
  if (args.startAt) {
    const start = new Date(args.startAt);
    if (Number.isNaN(start.getTime()) || start >= new Date(queryWindow.endAt))
      throw new Error("--start-at must be a valid date before today");
    queryWindow.startAt = start.toISOString();
  }
  const ids = await queryReleasePlanIds(args.days, "changed", queryWindow);
  const inventory = await fetchWorkItems(ids);
  const preliminaryCandidates = inventory
    .filter((item) =>
      args.mode === "all-management"
        ? item.fields?.["Custom.MgmtScope"] === "Yes"
        : isCompleteManagementPlan(item),
    )
    .sort(
      (left, right) =>
        new Date(right.fields["System.ChangedDate"]) -
        new Date(left.fields["System.ChangedDate"]),
    );
  const allChildIds = [...new Set(preliminaryCandidates.flatMap(childIds))];
  const children = new Map(
    (await fetchWorkItems(allChildIds))
      .filter((item) => item.fields?.["System.WorkItemType"] === "API Spec")
      .map((item) => [item.id, item]),
  );
  const assessedCandidates = preliminaryCandidates.map((item) => {
    const specs = childIds(item)
      .map((id) => children.get(id))
      .filter(Boolean);
    return { item, specs, preflight: assessPreflight(item, specs) };
  });
  const preflightSkippedPlans = assessedCandidates
    .filter((candidate) => !candidate.preflight.compliant)
    .map(({ item, preflight }) => ({
      id: String(item.id),
      title: plainText(item.fields?.["System.Title"], 180),
      sourceUrl: `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${item.id}`,
      stage: "preflight",
      reason: preflight.reasons.join(", ").slice(0, 300),
    }));
  const eligibleCandidates = assessedCandidates.filter(
    (candidate) => candidate.preflight.compliant,
  );
  const candidates =
    args.limit > 0 ? eligibleCandidates.slice(0, args.limit) : eligibleCandidates;
  if (args.limit > 0 && candidates.length < args.limit) {
    throw new Error(
      `Only ${candidates.length} core-correlated management-plane plans found in ${args.days} days`,
    );
  }

  const collected = await concurrentMap(
    candidates,
    args.concurrency,
    async ({ item, specs, preflight }) => {
      try {
        const [revisions, specRevisions] = await Promise.all([
          fetchRevisions(item.id),
          concurrentMap(specs, 3, async (spec) => ({
            id: spec.id,
            revisions: await fetchRevisions(spec.id),
          })),
        ]);
        return {
          plan: sanitizePlan(
            item,
            specs,
            revisions,
            specRevisions,
            preflight,
          ),
          skipped: null,
        };
      } catch (error) {
        return {
          plan: null,
          skipped: {
            id: String(item.id),
            title: plainText(item.fields?.["System.Title"], 180),
            sourceUrl: `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${item.id}`,
            stage: "revision-collection",
            reason: error.message.slice(0, 300),
          },
        };
      }
    },
  );
  const enrichedPlans = collected.map((result) => result.plan).filter(Boolean);
  const periodEligiblePlans = enrichedPlans.filter((plan) =>
    isPeriodEligible(plan, queryWindow.startAt),
  );
  const periodSkippedPlans = enrichedPlans
    .filter((plan) => !isPeriodEligible(plan, queryWindow.startAt))
    .map((plan) => ({
      id: plan.id,
      title: plan.title,
      sourceUrl: plan.adoUrl,
      stage: "reporting-period",
      reason: `terminal flow has no completion transition on or after ${queryWindow.startAt}`,
    }));
  const plans =
    args.limit > 0
      ? periodEligiblePlans.slice(0, Number(args.limit))
      : periodEligiblePlans;
  const skippedPlans = [
    ...preflightSkippedPlans,
    ...periodSkippedPlans,
    ...collected.map((result) => result.skipped).filter(Boolean),
  ];

  const generatedAt = new Date().toISOString();
  const output = {
    schemaVersion: 1,
    generatedAt,
    selection: {
      days: args.days,
      startAt: queryWindow.startAt,
      endAt: generatedAt,
      mode: args.mode,
      requested: args.limit || "all",
      criteria:
        args.mode === "all-management"
          ? `core-correlated management-plane Release Plans changed since ${queryWindow.startAt}; flows may start earlier`
          : "recent core-correlated Finished management-plane plans with complete released artifacts",
      inventoryCount: inventory.length,
      managementCount: preliminaryCandidates.length,
      candidateCount: eligibleCandidates.length,
      preflightSkippedCount: preflightSkippedPlans.length,
      periodSkippedCount: periodSkippedPlans.length,
      lookbackOverflowCount: 0,
      collectedCount: plans.length,
      skippedCount: skippedPlans.length,
    },
    skippedPlans,
    plans,
  };
  writeJson(args.output, output);
  console.log(
    `Collected ${plans.length} release plans (${skippedPlans.length} skipped) into ${args.output}`,
  );
}

function isPeriodEligible(plan, startAt) {
  if (new Date(plan.createdAt) >= new Date(startAt)) return true;
  return plan.revisionEvents.some(
    (event) =>
      new Date(event.occurredAt) >= new Date(startAt) &&
      event.type === "release.status_changed" &&
      /released/i.test(event.value),
  );
}

function isCompleteManagementPlan(item) {
  const fields = item.fields || {};
  if (fields["System.State"] !== "Finished") return false;
  if (fields["Custom.MgmtScope"] !== "Yes") return false;
  if (lower(fields["Custom.ReleasePlanType"]).includes("private")) return false;
  const languages = LANGUAGES.filter((language) =>
    isLanguageApplicable(fields, language),
  );
  if (!languages.length) return false;
  if (
    !languages.every(
      (language) =>
        lower(fields[`Custom.ReleaseStatusFor${language}`]).includes(
          "released",
        ) && parsePrUrl(fields[`Custom.SDKPullRequestFor${language}`]),
    )
  )
    return false;
  return childIds(item).length > 0;
}

function sanitizePlan(
  item,
  specs,
  revisions,
  specRevisionGroups,
  preflight,
) {
  const fields = item.fields || {};
  const languages = preflight.languages.map((snapshot) => {
    const sdkPr = snapshot.sdkPr;
    const history = new Map();
    for (const revision of revisions) {
      const linked = parsePrUrl(
        revision.fields?.[`Custom.SDKPullRequestFor${snapshot.key}`],
      );
      if (linked) history.set(linked.id, linked);
    }
    if (sdkPr) history.set(sdkPr.id, sdkPr);
    return {
      ...snapshot,
      sdkPr,
      sdkPrHistory: [...history.values()],
    };
  });
  const specPrs = new Map(preflight.specPrs.map((pr) => [pr.id, pr]));
  for (const group of specRevisionGroups) {
    for (const revision of group.revisions) {
      const pr = parsePrUrl(
        revision.fields?.["Custom.ActiveSpecPullRequestUrl"],
      );
      if (pr) specPrs.set(pr.id, pr);
    }
  }

  return {
    id: String(item.id),
    releasePlanId: preflight.releasePlanId,
    title: plainText(fields["System.Title"], 180),
    service: plainText(
      fields["Custom.ServiceName"] ||
        fields["Custom.ProductServiceTreeName"] ||
        fields["System.Title"],
      120,
    ),
    product: plainText(
      fields["Custom.ProductServiceTreeName"] || fields["System.AreaPath"],
      160,
    ),
    path: plainText(fields["Custom.ApiSpecProjectPath"], 220),
    state: fields["System.State"],
    plane: "management",
    releaseType: plainText(fields["Custom.ReleasePlanType"], 80),
    intendedMonth: plainText(fields["Custom.SDKReleasemonth"], 40) || null,
    createdAt: fields["System.CreatedDate"],
    changedAt: fields["System.ChangedDate"],
    adoUrl: `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${item.id}`,
    specPrs: [...specPrs.values()],
    languages,
    revisionEvents: normalizeRevisions(item.id, revisions, languages),
    specRevisionEvents: specRevisionGroups.flatMap((group) =>
      normalizeSpecRevisions(group.id, group.revisions),
    ),
    correlation: {
      policyVersion: POLICY_VERSION,
      preflight: "passed",
    },
  };
}

function normalizeRevisions(planId, revisions, languages) {
  const events = [];
  let previous = {};
  for (const revision of revisions) {
    const fields = revision.fields || {};
    const occurredAt = fields["System.ChangedDate"];
    addChange(
      events,
      previous,
      fields,
      "System.State",
      "plan.state_changed",
      "plan",
      occurredAt,
      planId,
      revision.rev,
    );
    addChange(
      events,
      previous,
      fields,
      "Custom.APISpecApprovalStatus",
      "spec.approval_changed",
      "spec-review",
      occurredAt,
      planId,
      revision.rev,
    );
    for (const language of languages) {
      const track = `sdk:${language.id}:${language.package || language.id}`;
      for (const [suffix, type, phase] of [
        ["GenerationStatusFor", "generation.status_changed", "generation"],
        ["SDKPullRequestFor", "sdk.pr_linked", "generation"],
        ["ReleaseStatusFor", "release.status_changed", "release"],
        ["ReleasedVersionFor", "package.version_observed", "release"],
      ]) {
        addChange(
          events,
          previous,
          fields,
          `Custom.${suffix}${language.key}`,
          type,
          phase,
          occurredAt,
          planId,
          revision.rev,
          track,
        );
      }
    }
    previous = fields;
  }
  return events;
}

function normalizeSpecRevisions(specId, revisions) {
  const events = [];
  let previous = {};
  for (const revision of revisions) {
    addChange(
      events,
      previous,
      revision.fields || {},
      "Custom.ActiveSpecPullRequestUrl",
      "spec.pr_linked",
      "spec-review",
      revision.fields?.["System.ChangedDate"],
      specId,
      revision.rev,
      "spec",
    );
    previous = revision.fields || {};
  }
  return events;
}

function addChange(
  events,
  previous,
  current,
  field,
  type,
  phase,
  occurredAt,
  itemId,
  revision,
  trackId = "plan",
) {
  if (!occurredAt || previous[field] === current[field] || !current[field])
    return;
  const value =
    field.includes("PullRequest") || field.includes("PullRequestUrl")
      ? parsePrUrl(current[field])?.url
      : plainText(current[field], 160);
  if (!value) return;
  events.push({
    id: `ado:${itemId}:${revision}:${field}`,
    type,
    phase,
    occurredAt,
    observedAt: occurredAt,
    trackId,
    value,
    confidence: "observed",
    source: {
      system: "azure-devops",
      entity: `work-item:${itemId}`,
      url: `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${itemId}`,
    },
  });
}
