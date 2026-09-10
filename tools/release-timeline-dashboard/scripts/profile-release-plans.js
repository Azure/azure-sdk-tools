#!/usr/bin/env node

const { execFileSync } = require("node:child_process");

const ORG = "https://dev.azure.com/azure-sdk";
const PROJECT = "Release";
const API_VERSION = "7.1";
const DEVOPS_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798";
const LANGUAGES = ["Dotnet", "JavaScript", "Python", "Java", "Go"];
const STATES = ["New", "In Progress", "Finished", "Abandoned", "Duplicate"];
const CHILD_RELATION = "System.LinkTypes.Hierarchy-Forward";
const WORK_ITEM_ID = /\/workItems\/(\d+)$/;
const GITHUB_PR = /https?:\/\/github\.com\/[^/\s]+\/[^/\s]+\/pull\/\d+/i;

const args = parseArgs(process.argv.slice(2));
let token = getToken();

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});

async function main() {
  const generatedAt = new Date();
  const queryEnd = new Date(
    Date.UTC(
      generatedAt.getUTCFullYear(),
      generatedAt.getUTCMonth(),
      generatedAt.getUTCDate() + 1,
    ),
  );
  const start = new Date(queryEnd);
  start.setUTCDate(start.getUTCDate() - args.days);

  const ids = await queryIdsByWindow(start, queryEnd);
  const plans = await fetchWorkItems(ids);
  const childIds = [
    ...new Set(plans.flatMap((plan) => extractChildIds(plan.relations || []))),
  ];
  const children = await fetchWorkItems(childIds);
  const apiSpecs = new Map(
    children
      .filter((item) => item.fields?.["System.WorkItemType"] === "API Spec")
      .map((item) => [item.id, item]),
  );

  const revisionPlans = selectRevisionSample(plans, args.revisionSample);
  const revisionProfiles = await concurrentMap(
    revisionPlans,
    args.concurrency,
    async (plan) => {
      const revisions = await request(
        `${ORG}/${PROJECT}/_apis/wit/workitems/${plan.id}/revisions?$expand=all&api-version=${API_VERSION}`,
      );
      return profileRevisions(plan, revisions.value || []);
    },
  );
  const revisionApiSpecs = [
    ...new Map(
      revisionPlans
        .flatMap((plan) => getApiSpecs(plan, apiSpecs))
        .map((item) => [item.id, item]),
    ).values(),
  ];
  const apiSpecRevisionProfiles = await concurrentMap(
    revisionApiSpecs,
    args.concurrency,
    async (item) => {
      const revisions = await request(
        `${ORG}/${PROJECT}/_apis/wit/workitems/${item.id}/revisions?$expand=all&api-version=${API_VERSION}`,
      );
      return profileRevisions(item, revisions.value || []);
    },
  );

  const report = {
    generatedAt: generatedAt.toISOString(),
    query: {
      days: args.days,
      startDate: start.toISOString(),
      endDate: generatedAt.toISOString(),
      queryEndExclusive: queryEnd.toISOString(),
      releasePlanCount: plans.length,
      apiSpecChildCount: apiSpecs.size,
      revisionSampleSize: revisionProfiles.length,
    },
    inventory: summarizeInventory(plans),
    fieldCoverage: summarizeCoverage(plans, apiSpecs),
    languageCoverage: summarizeLanguages(plans),
    correlation: summarizeCorrelation(plans, apiSpecs),
    dataQuality: summarizeDataQuality(plans, apiSpecs),
    revisions: {
      releasePlans: summarizeRevisionProfiles(revisionProfiles),
      apiSpecs: summarizeRevisionProfiles(apiSpecRevisionProfiles),
    },
  };

  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
}

function parseArgs(argv) {
  const result = { days: 365, revisionSample: 30, concurrency: 8 };
  for (let i = 0; i < argv.length; i++) {
    const value = argv[i + 1];
    if (argv[i] === "--days") result.days = positiveInteger(value, "--days");
    else if (argv[i] === "--revision-sample")
      result.revisionSample = positiveInteger(value, "--revision-sample");
    else if (argv[i] === "--concurrency")
      result.concurrency = positiveInteger(value, "--concurrency");
    else if (argv[i] === "--help") {
      console.log(
        "Usage: node scripts/profile-release-plans.js [--days 365] [--revision-sample 30] [--concurrency 8]",
      );
      process.exit(0);
    } else {
      throw new Error(`Unknown argument: ${argv[i]}`);
    }
    i++;
  }
  return result;
}

function positiveInteger(value, name) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed <= 0)
    throw new Error(`${name} must be a positive integer`);
  return parsed;
}

function getToken() {
  try {
    return execFileSync(
      "az",
      [
        "account",
        "get-access-token",
        "--resource",
        DEVOPS_SCOPE,
        "--query",
        "accessToken",
        "-o",
        "tsv",
      ],
      { encoding: "utf8", stdio: ["ignore", "pipe", "inherit"] },
    ).trim();
  } catch {
    throw new Error(
      "Unable to acquire an Azure DevOps token. Run `az login` with an account that can read the Release project.",
    );
  }
}

async function request(url, options = {}, retried = false) {
  const response = await fetch(url, {
    ...options,
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: "application/json",
      "Content-Type": "application/json",
      ...options.headers,
    },
    signal: AbortSignal.timeout(30_000),
  });
  if (response.status === 401 && !retried) {
    token = getToken();
    return request(url, options, true);
  }
  if (!response.ok) {
    const body = await response.text();
    throw new Error(
      `Azure DevOps ${response.status} for ${url}: ${body.slice(0, 300)}`,
    );
  }
  return response.json();
}

async function queryIdsByWindow(start, end) {
  const ids = new Set();
  let cursor = new Date(start);
  while (cursor < end) {
    const next = new Date(cursor);
    next.setUTCDate(next.getUTCDate() + 30);
    if (next > end) next.setTime(end.getTime());
    const query = `SELECT [System.Id] FROM WorkItems
      WHERE [System.TeamProject] = '${PROJECT}'
        AND [System.WorkItemType] = 'Release Plan'
        AND [System.CreatedDate] >= '${formatWiqlDate(cursor)}'
        AND [System.CreatedDate] < '${formatWiqlDate(next)}'
      ORDER BY [System.CreatedDate] DESC`;
    const result = await request(
      `${ORG}/${PROJECT}/_apis/wit/wiql?api-version=${API_VERSION}`,
      { method: "POST", body: JSON.stringify({ query }) },
    );
    const windowIds = (result.workItems || []).map((item) => item.id);
    if (windowIds.length >= 1000) {
      throw new Error(
        `The ${formatWiqlDate(cursor)} to ${formatWiqlDate(next)} window reached the 1000-item WIQL cap; reduce the window size.`,
      );
    }
    windowIds.forEach((id) => ids.add(id));
    cursor = next;
  }
  return [...ids];
}

function formatWiqlDate(date) {
  return date.toISOString().slice(0, 10);
}

async function fetchWorkItems(ids) {
  const items = [];
  for (let i = 0; i < ids.length; i += 200) {
    const batch = ids.slice(i, i + 200);
    const result = await request(
      `${ORG}/_apis/wit/workitems?ids=${batch.join(",")}&$expand=all&api-version=${API_VERSION}`,
    );
    items.push(...(result.value || []));
  }
  return items;
}

function extractChildIds(relations) {
  return relations.flatMap((relation) => {
    if (relation.rel !== CHILD_RELATION) return [];
    const match = relation.url?.match(WORK_ITEM_ID);
    return match ? [Number.parseInt(match[1], 10)] : [];
  });
}

function getApiSpecs(plan, apiSpecs) {
  return extractChildIds(plan.relations || [])
    .map((id) => apiSpecs.get(id))
    .filter(Boolean);
}

function summarizeInventory(plans) {
  return {
    byState: countBy(plans, (plan) => plan.fields?.["System.State"] || "(empty)"),
    byCreatedUsing: countBy(
      plans,
      (plan) => plan.fields?.["Custom.CreatedUsing"] || "(empty)",
    ),
    byReleasePlanType: countBy(
      plans,
      (plan) => plan.fields?.["Custom.ReleasePlanType"] || "(empty)",
    ),
    byPlane: countBy(plans, classifyPlane),
    byCreationMonth: countBy(plans, (plan) =>
      (plan.fields?.["System.CreatedDate"] || "").slice(0, 7),
    ),
  };
}

function classifyPlane(plan) {
  const fields = plan.fields || {};
  const management = fields["Custom.MgmtScope"] === "Yes";
  const data = fields["Custom.DataScope"] === "Yes";
  if (management && data) return "both";
  if (management) return "management";
  if (data) return "data";
  return "unspecified";
}

function summarizeCoverage(plans, apiSpecs) {
  const fields = [
    "Custom.ReleasePlanID",
    "Custom.ApiSpecProjectPath",
    "Custom.SDKLanguages",
    "Custom.APISpecApprovalStatus",
    "Custom.ReleasePlanType",
    "Custom.ProductServiceTreeID",
    "Custom.SDKReleasemonth",
    "Custom.SDKtypetobereleased",
  ];
  const result = Object.fromEntries(
    fields.map((field) => [field, coverage(plans, field)]),
  );
  result.apiSpecChild = ratio(
    plans.filter((plan) => getApiSpecs(plan, apiSpecs).length > 0).length,
    plans.length,
  );
  result.apiSpecActivePr = ratio(
    plans.filter((plan) =>
      getApiSpecs(plan, apiSpecs).some((spec) =>
        GITHUB_PR.test(spec.fields?.["Custom.ActiveSpecPullRequestUrl"] || ""),
      ),
    ).length,
    plans.length,
  );
  result.apiSpecReviewHistory = ratio(
    plans.filter((plan) =>
      getApiSpecs(plan, apiSpecs).some((spec) =>
        GITHUB_PR.test(spec.fields?.["Custom.RESTAPIReviews"] || ""),
      ),
    ).length,
    plans.length,
  );
  return result;
}

function summarizeLanguages(plans) {
  return Object.fromEntries(
    LANGUAGES.map((language) => {
      const applicable = plans.filter(
        (plan) => isLanguageApplicable(plan, language),
      );
      const fields = [
        "PackageName",
        "SDKGenerationPipeline",
        "SDKPullRequest",
        "GenerationStatus",
        "SDKPullRequestStatus",
        "ReleaseStatus",
        "ReleasedVersion",
        "ReleasePipeline",
      ];
      const prefixes = {
        PackageName: `Custom.${language}PackageName`,
        SDKGenerationPipeline: `Custom.SDKGenerationPipelineFor${language}`,
        SDKPullRequest: `Custom.SDKPullRequestFor${language}`,
        GenerationStatus: `Custom.GenerationStatusFor${language}`,
        SDKPullRequestStatus: `Custom.SDKPullRequestStatusFor${language}`,
        ReleaseStatus: `Custom.ReleaseStatusFor${language}`,
        ReleasedVersion: `Custom.ReleasedVersionFor${language}`,
        ReleasePipeline: `Custom.ReleasePipelineFor${language}`,
      };
      const finished = applicable.filter(
        (plan) => plan.fields?.["System.State"] === "Finished",
      );
      const fieldCoverage = (items) =>
        Object.fromEntries(
          fields.map((field) => [field, coverage(items, prefixes[field])]),
        );
      return [
        displayLanguage(language),
        {
          applicablePlans: applicable.length,
          fields: fieldCoverage(applicable),
          finishedPlans: finished.length,
          finishedFields: fieldCoverage(finished),
          exclusionStatuses: countBy(
            plans,
            (plan) =>
              plan.fields?.[`Custom.ReleaseExclusionStatusFor${language}`] ||
              "(empty)",
          ),
          generationStatuses: countBy(
            applicable,
            (plan) =>
              plan.fields?.[`Custom.GenerationStatusFor${language}`] ||
              "(empty)",
          ),
          releaseStatuses: countBy(
            applicable,
            (plan) =>
              plan.fields?.[`Custom.ReleaseStatusFor${language}`] || "(empty)",
          ),
        },
      ];
    }),
  );
}

function summarizeCorrelation(plans, apiSpecs) {
  const specPrOwners = new Map();
  const sdkPrOwners = new Map();
  for (const plan of plans) {
    for (const spec of getApiSpecs(plan, apiSpecs)) {
      const urls = [
        spec.fields?.["Custom.ActiveSpecPullRequestUrl"],
        ...extractPrUrls(spec.fields?.["Custom.RESTAPIReviews"] || ""),
      ];
      urls.filter(Boolean).forEach((url) => addOwner(specPrOwners, url, plan.id));
    }
    for (const language of LANGUAGES) {
      const url = plan.fields?.[`Custom.SDKPullRequestFor${language}`];
      if (url) addOwner(sdkPrOwners, url, plan.id);
    }
  }
  return {
    uniqueSpecPrUrls: specPrOwners.size,
    specPrUrlsLinkedToMultiplePlans: duplicateSummary(specPrOwners),
    uniqueSdkPrUrls: sdkPrOwners.size,
    sdkPrUrlsLinkedToMultiplePlans: duplicateSummary(sdkPrOwners),
    plansWithMultipleApiSpecChildren: plans.filter(
      (plan) => getApiSpecs(plan, apiSpecs).length > 1,
    ).length,
  };
}

function summarizeDataQuality(plans, apiSpecs) {
  const counters = {
    applicableLanguageInstances: 0,
    releasedLanguageInstances: 0,
    sdkPrLanguageInstances: 0,
    completedGenerationInstances: 0,
    approvedSpecPlans: 0,
    releasedWithoutVersion: 0,
    releasedWithNonTerminalPrStatus: 0,
    sdkPrWithoutGenerationPipeline: 0,
    generationCompletedWithoutSdkPr: 0,
    approvedSpecWithoutSpecPr: 0,
    missingTypeSpecPath: 0,
  };
  for (const plan of plans) {
    const fields = plan.fields || {};
    if (!fields["Custom.ApiSpecProjectPath"]) counters.missingTypeSpecPath++;
    const specApproved = lower(fields["Custom.APISpecApprovalStatus"]).includes(
      "approved",
    );
    if (specApproved) counters.approvedSpecPlans++;
    const hasSpecPr = getApiSpecs(plan, apiSpecs).some((spec) =>
      GITHUB_PR.test(spec.fields?.["Custom.ActiveSpecPullRequestUrl"] || ""),
    );
    if (specApproved && !hasSpecPr) counters.approvedSpecWithoutSpecPr++;

    for (const language of LANGUAGES) {
      if (!isLanguageApplicable(plan, language)) continue;
      counters.applicableLanguageInstances++;
      const released = lower(
        fields[`Custom.ReleaseStatusFor${language}`],
      ).includes("released");
      const prStatus = lower(
        fields[`Custom.SDKPullRequestStatusFor${language}`],
      );
      const sdkPr = fields[`Custom.SDKPullRequestFor${language}`];
      const generationPipeline =
        fields[`Custom.SDKGenerationPipelineFor${language}`];
      const generationCompleted = lower(
        fields[`Custom.GenerationStatusFor${language}`],
      ).includes("completed");
      if (released) counters.releasedLanguageInstances++;
      if (sdkPr) counters.sdkPrLanguageInstances++;
      if (generationCompleted) counters.completedGenerationInstances++;
      if (released && !fields[`Custom.ReleasedVersionFor${language}`])
        counters.releasedWithoutVersion++;
      if (
        released &&
        prStatus &&
        !prStatus.includes("merged") &&
        !prStatus.includes("completed")
      )
        counters.releasedWithNonTerminalPrStatus++;
      if (sdkPr && !generationPipeline)
        counters.sdkPrWithoutGenerationPipeline++;
      if (generationCompleted && !sdkPr)
        counters.generationCompletedWithoutSdkPr++;
    }
  }
  return counters;
}

function selectRevisionSample(plans, size) {
  const groups = new Map(
    STATES.map((state) => [
      state,
      plans
        .filter((plan) => plan.fields?.["System.State"] === state)
        .sort(
          (a, b) =>
            new Date(b.fields["System.CreatedDate"]) -
            new Date(a.fields["System.CreatedDate"]),
        ),
    ]),
  );
  const selected = [];
  while (selected.length < size) {
    let added = false;
    for (const state of STATES) {
      const item = groups.get(state)?.shift();
      if (item) {
        selected.push(item);
        added = true;
        if (selected.length === size) break;
      }
    }
    if (!added) break;
  }
  return selected;
}

function profileRevisions(plan, revisions) {
  const fieldChanges = {};
  const milestones = {};
  let previous = {};
  for (const revision of revisions) {
    const fields = revision.fields || {};
    const changedAt = fields["System.ChangedDate"];
    const names = new Set([...Object.keys(previous), ...Object.keys(fields)]);
    for (const name of names) {
      if (!isTimelineField(name)) continue;
      if (JSON.stringify(previous[name]) === JSON.stringify(fields[name]))
        continue;
      fieldChanges[name] = (fieldChanges[name] || 0) + 1;
      if (isPopulated(fields[name]) && !milestones[name])
        milestones[name] = changedAt;
    }
    previous = fields;
  }
  const first = revisions[0]?.fields?.["System.ChangedDate"];
  const last = revisions.at(-1)?.fields?.["System.ChangedDate"];
  const actors = new Set(
    revisions
      .map((revision) => revision.fields?.["System.ChangedBy"]?.displayName)
      .filter(Boolean),
  );
  return {
    id: plan.id,
    state: plan.fields?.["System.State"] || "",
    revisionCount: revisions.length,
    lifecycleHours:
      first && last ? (new Date(last) - new Date(first)) / 3_600_000 : 0,
    humanActorCount: [...actors].filter((actor) => !isBot(actor)).length,
    botActorCount: [...actors].filter(isBot).length,
    fieldChanges,
    milestoneFieldCount: Object.keys(milestones).length,
  };
}

function summarizeRevisionProfiles(profiles) {
  const mergedChanges = {};
  for (const profile of profiles) {
    for (const [field, count] of Object.entries(profile.fieldChanges))
      mergedChanges[field] = (mergedChanges[field] || 0) + count;
  }
  return {
    revisionCount: stats(profiles.map((profile) => profile.revisionCount)),
    lifecycleHours: stats(profiles.map((profile) => profile.lifecycleHours)),
    plansChangedByHumans: profiles.filter(
      (profile) => profile.humanActorCount > 0,
    ).length,
    plansChangedByBots: profiles.filter((profile) => profile.botActorCount > 0)
      .length,
    timelineFieldChanges: Object.fromEntries(
      Object.entries(mergedChanges).sort((a, b) => b[1] - a[1]),
    ),
    samples: profiles.map(
      ({
        id,
        state,
        revisionCount,
        lifecycleHours,
        humanActorCount,
        botActorCount,
        milestoneFieldCount,
      }) => ({
        id,
        state,
        revisionCount,
        lifecycleHours: round(lifecycleHours),
        humanActorCount,
        botActorCount,
        milestoneFieldCount,
      }),
    ),
  };
}

function isTimelineField(name) {
  return (
    name === "System.State" ||
    name === "System.Reason" ||
    name === "Custom.APISpecApprovalStatus" ||
    name === "Custom.ApiSpecProjectPath" ||
    name === "Custom.ActiveSpecPullRequestUrl" ||
    name === "Custom.RESTAPIReviews" ||
    name === "Custom.APISpecversion" ||
    name === "Custom.APISpecDefinitionType" ||
    /Custom\.(SDKGenerationPipelineFor|SDKPullRequestFor|GenerationStatusFor|SDKPullRequestStatusFor|ReleaseStatusFor|ReleasedVersionFor|ReleasePipelineFor)/.test(
      name,
    )
  );
}

function coverage(items, field) {
  return ratio(
    items.filter((item) => isPopulated(item.fields?.[field])).length,
    items.length,
  );
}

function ratio(count, total) {
  return { count, total, percent: total ? round((count / total) * 100) : 0 };
}

function countBy(items, keyFn) {
  const counts = new Map();
  for (const item of items) {
    const key = keyFn(item) || "(empty)";
    counts.set(key, (counts.get(key) || 0) + 1);
  }
  return Object.fromEntries(
    [...counts.entries()].sort((a, b) => b[1] - a[1]),
  );
}

function stats(values) {
  if (!values.length)
    return { min: 0, median: 0, p90: 0, max: 0, mean: 0 };
  const sorted = [...values].sort((a, b) => a - b);
  return {
    min: round(sorted[0]),
    median: round(percentile(sorted, 0.5)),
    p90: round(percentile(sorted, 0.9)),
    max: round(sorted.at(-1)),
    mean: round(sorted.reduce((sum, value) => sum + value, 0) / sorted.length),
  };
}

function percentile(sorted, fraction) {
  return sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * fraction))];
}

function addOwner(map, rawUrl, planId) {
  const url = rawUrl.trim().replace(/\/+$/, "").toLowerCase();
  if (!map.has(url)) map.set(url, new Set());
  map.get(url).add(planId);
}

function duplicateSummary(map) {
  const duplicates = [...map.values()].filter((owners) => owners.size > 1);
  return {
    count: duplicates.length,
    maximumPlanCount: duplicates.length
      ? Math.max(...duplicates.map((owners) => owners.size))
      : 0,
  };
}

function extractPrUrls(value) {
  return value.match(new RegExp(GITHUB_PR.source, "gi")) || [];
}

function isExcluded(value) {
  const normalized = lower(value);
  return (
    normalized === "approved" ||
    normalized === "missingemitterconfig"
  );
}

function isLanguageApplicable(plan, language) {
  const fields = plan.fields || {};
  if (lower(fields["Custom.ReleasePlanType"]).includes("private")) return false;
  if (isExcluded(fields[`Custom.ReleaseExclusionStatusFor${language}`]))
    return false;

  const configured = String(fields["Custom.SDKLanguages"] || "")
    .split(",")
    .map((value) => value.trim().toLowerCase())
    .filter(Boolean);
  if (configured.length)
    return configured.includes(displayLanguage(language).toLowerCase());

  return [
    `Custom.${language}PackageName`,
    `Custom.SDKGenerationPipelineFor${language}`,
    `Custom.SDKPullRequestFor${language}`,
    `Custom.GenerationStatusFor${language}`,
    `Custom.SDKPullRequestStatusFor${language}`,
    `Custom.ReleaseStatusFor${language}`,
  ].some((field) => isPopulated(fields[field]));
}

function isPopulated(value) {
  return value !== undefined && value !== null && value !== "";
}

function isBot(actor) {
  return /bot|assistant|build service|project collection/i.test(actor);
}

function displayLanguage(language) {
  return language === "Dotnet" ? ".NET" : language;
}

function lower(value) {
  return String(value || "").toLowerCase();
}

function round(value) {
  return Math.round(value * 100) / 100;
}

async function concurrentMap(items, concurrency, fn) {
  const results = new Array(items.length);
  let cursor = 0;
  async function worker() {
    while (cursor < items.length) {
      const index = cursor++;
      results[index] = await fn(items[index]);
    }
  }
  await Promise.all(
    Array.from({ length: Math.min(concurrency, items.length) }, worker),
  );
  return results;
}
