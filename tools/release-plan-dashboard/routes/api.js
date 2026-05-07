import express from "express";
const router = express.Router();

import { cache, evictOldest, CACHE_TTL_MS, PR_DETAIL_CACHE_TTL_MS } from "../lib/cache.js";
import { parseGitHubPrUrl, batchFetchPrStatuses, batchFetchPrDetails, batchFetchSpecProjectPaths, batchFetchSpecPrLabels } from "../lib/github-api.js";
import {
  DEVOPS_ORG, DEVOPS_PROJECT, API_VERSION, LANGUAGES, LANGUAGE_DISPLAY, LANGUAGE_PACKAGE_WI,
  API_SPEC_FIELDS,
  devopsRequest, runWiql, fetchWorkItemsBatch,
  extractChildIds, getField, mapReleasePlan,
  fetchPackageWorkItems, fetchAzureSdkPackageList, isKnownPackage, isGAVersion,
} from "../lib/devops-api.js";

const RELEASE_PLAN_ID_FORMAT = /^[a-zA-Z0-9_-]+$/;
const GITHUB_PR_URL_PATTERN = /github\.com\/.*\/pull\/\d+/;

// ── Core data fetching and enrichment ─────────────────────────

/** Builds a map of child work item ID → API Spec work item from a list of parent work items. */
async function buildApiSpecMap(workItems) {
  const allChildIds = [];
  for (const workItem of workItems) allChildIds.push(...extractChildIds(workItem));
  const apiSpecMap = {};
  if (allChildIds.length) {
    const uniqueChildIds = [...new Set(allChildIds)];
    const childItems = await fetchWorkItemsBatch(uniqueChildIds, API_SPEC_FIELDS);
    for (const child of childItems) {
      if (getField(child, "System.WorkItemType") === "API Spec") apiSpecMap[child.id] = child;
    }
  }
  return apiSpecMap;
}

/** Checks if a GitHub token is available for API calls. */
function hasGitHubToken() {
  return !!(process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN);
}

/** Fetches all active release plans from Azure DevOps, enriches with GitHub data. */
async function fetchAllReleasePlans() {
  const wiqlQuery = `SELECT [System.Id] FROM WorkItems
    WHERE [System.TeamProject] = 'Release'
      AND [System.WorkItemType] = 'Release Plan'
      AND [System.Tags] NOT CONTAINS 'Release Planner App Test'
      AND (
        [System.State] IN ('In Progress','Not Started','New')
        OR ([System.State] = 'Finished' AND [System.ChangedDate] >= @Today - 60)
      )
    ORDER BY [System.ChangedDate] DESC`;

  console.log("Running WIQL query...");
  const ids = await runWiql(wiqlQuery);
  console.log(`Found ${ids.length} release plan work items.`);
  if (!ids.length) return { plans: [], fetchedAt: new Date().toISOString() };

  const workItems = await fetchWorkItemsBatch(ids);
  const apiSpecMap = await buildApiSpecMap(workItems);

  let plans = workItems.map(wi => mapReleasePlan(wi, apiSpecMap)).filter(p => {
    const defType = (p.apiSpec && p.apiSpec.definitionType) || "";
    if (defType.toLowerCase() === "openapi") return false;
    return true;
  });

  await enrichPlans(plans);

  const fetchedAt = new Date().toISOString();
  console.log(`Fetched ${plans.length} release plans.`);
  return { plans, fetchedAt };
}

/** Enriches plans with spec PR statuses and TypeSpec project paths from GitHub. */
async function enrichSpecPrData(plans) {
  if (!hasGitHubToken()) {
    plans.forEach(plan => { plan.apiReadiness = "unknown"; plan.specProjectPath = plan.typeSpecPath || ""; });
    return;
  }
  const specPrUrls = [];
  const specPrUrlsForPath = [];
  for (const plan of plans) {
    const specUrl = (plan.apiSpec && plan.apiSpec.specPrUrl) || "";
    if (specUrl) {
      specPrUrls.push(specUrl);
      if (!plan.typeSpecPath) specPrUrlsForPath.push(specUrl);
    }
  }
  const [statusMap, specPathMap, specLabelMap] = await Promise.all([
    batchFetchPrStatuses(specPrUrls),
    batchFetchSpecProjectPaths(specPrUrlsForPath),
    batchFetchSpecPrLabels(specPrUrls),
  ]);
  for (const plan of plans) {
    const specUrl = (plan.apiSpec && plan.apiSpec.specPrUrl) || "";
    if (specUrl && statusMap.has(specUrl)) {
      const status = statusMap.get(specUrl);
      plan.apiReadiness = status === "merged" ? "completed" : status === "open" ? "pending" : status || "unknown";
    } else { plan.apiReadiness = "unknown"; }
    if (specUrl && specPathMap.has(specUrl)) {
      const derivedPath = specPathMap.get(specUrl);
      if (derivedPath) plan.specProjectPath = derivedPath;
    }
    if (!plan.specProjectPath) plan.specProjectPath = plan.typeSpecPath || "";
    if (specUrl && specLabelMap.has(specUrl)) {
      plan.specPrLabels = specLabelMap.get(specUrl);
    }
  }
}

/** Enriches plans with package version, API review status, and namespace approval from DevOps. */
async function enrichPackageData(plans) {
  const pkgLangPairs = [];
  for (const plan of plans) {
    for (const [displayLang, langData] of Object.entries(plan.languages || {})) {
      if (langData.packageName) pkgLangPairs.push({ pkg: langData.packageName, lang: LANGUAGE_PACKAGE_WI[displayLang] || displayLang });
    }
  }
  const [pkgMap, azureSdkPage] = await Promise.all([fetchPackageWorkItems(pkgLangPairs), fetchAzureSdkPackageList()]);
  for (const plan of plans) {
    for (const [displayLang, langData] of Object.entries(plan.languages || {})) {
      if (!langData.packageName) continue;
      const isReleased = (langData.releaseStatus || "").toLowerCase() === "released";
      const key = `${langData.packageName}|${LANGUAGE_PACKAGE_WI[displayLang] || displayLang}`;
      const pkgData = pkgMap.get(key);
      if (pkgData) {
        langData.pkgVersion = pkgData.version;
        if (!isReleased) {
          langData.namespaceApproval = pkgData.namespaceApproval;
          if (isGAVersion(pkgData.version)) langData.apiReviewStatus = pkgData.apiReviewStatus;
        }
      }
      langData.isNewPackage = !isKnownPackage(langData.packageName, azureSdkPage);
    }
  }
}

/** Enriches plans with SDK PR statuses from GitHub and caches results. */
async function enrichSdkPrStatuses(plans) {
  if (!hasGitHubToken()) return;
  const sdkPrUrls = [];
  for (const plan of plans) {
    for (const [, langData] of Object.entries(plan.languages || {})) {
      if (langData.sdkPrUrl) sdkPrUrls.push(langData.sdkPrUrl);
    }
  }
  const uniqueSdkPrUrls = [...new Set(sdkPrUrls.filter(Boolean))];
  if (!uniqueSdkPrUrls.length) return;

  console.log(`Fetching SDK PR statuses for ${uniqueSdkPrUrls.length} unique PRs...`);
  const statusMap = await batchFetchPrStatuses(uniqueSdkPrUrls);
  const now = Date.now();
  for (const plan of plans) {
    for (const [, langData] of Object.entries(plan.languages || {})) {
      if (!langData.sdkPrUrl) continue;
      const status = statusMap.get(langData.sdkPrUrl);
      if (status) {
        langData.sdkPrGitHubStatus = status;
        cache.prStatuses.set(langData.sdkPrUrl, { data: status, updatedAt: now });
      }
    }
  }
  evictOldest(cache.prStatuses);
  console.log(`SDK PR statuses fetched and cached for ${uniqueSdkPrUrls.length} PRs.`);
}

/** Enriches release plans with GitHub PR data, package data, and activity timestamps. */
async function enrichPlans(plans) {
  await enrichSpecPrData(plans);

  try {
    await enrichPackageData(plans);
  } catch (err) { console.warn("Package enrichment error:", err.message); }

  await enrichSdkPrStatuses(plans);

  for (const plan of plans) {
    const latest = plan.changedDate ? new Date(plan.changedDate).getTime() : 0;
    plan.lastActivity = latest ? new Date(latest).toISOString() : "";
  }
}

// ── Cache management ──────────────────────────────────────────

async function refreshReleasePlansCache() {
  if (cache.releasePlans.refreshing) return;
  cache.releasePlans.refreshing = true;
  try {
    const result = await fetchAllReleasePlans();
    cache.releasePlans.data = result;
    cache.releasePlans.fetchedAt = result.fetchedAt;
    cache.releasePlans.updatedAt = Date.now();
    console.log(`Release plans cache refreshed at ${result.fetchedAt}`);
  } catch (err) {
    console.error("Cache refresh error:", err.message);
  } finally {
    cache.releasePlans.refreshing = false;
  }
}

async function getCachedReleasePlans() {
  if (!cache.releasePlans.data && cache.releasePlans.refreshing) {
    return { plans: [], fetchedAt: null, loading: true };
  }
  if (cache.releasePlans.data && cache.releasePlans.refreshing) {
    return cache.releasePlans.data;
  }
  const age = Date.now() - cache.releasePlans.updatedAt;
  if (!cache.releasePlans.data || age > CACHE_TTL_MS) {
    await refreshReleasePlansCache();
  }
  return cache.releasePlans.data || { plans: [], fetchedAt: new Date().toISOString() };
}

// ── Route handlers ────────────────────────────────────────────

router.get("/api/release-plans", async (req, res) => {
  try {
    const filterPlanId = req.query.releasePlan || req.query.releaseplan || "";

    if (filterPlanId) {
      if (!RELEASE_PLAN_ID_FORMAT.test(filterPlanId)) {
        return res.status(400).json({ error: "Invalid release plan ID format." });
      }
      const wiqlQuery = `SELECT [System.Id] FROM WorkItems
        WHERE [System.TeamProject] = 'Release'
          AND [System.WorkItemType] = 'Release Plan'
          AND [Custom.ReleasePlanID] = '${filterPlanId}'`;
      const ids = await runWiql(wiqlQuery);
      if (!ids.length) return res.json({ plans: [], notFound: filterPlanId, fetchedAt: new Date().toISOString() });
      const workItems = await fetchWorkItemsBatch(ids);
      const apiSpecMap = await buildApiSpecMap(workItems);
      const plans = workItems.map(wi => mapReleasePlan(wi, apiSpecMap));
      await enrichPlans(plans);
      return res.json({ plans, fetchedAt: new Date().toISOString() });
    }

    const result = await getCachedReleasePlans();
    res.json(result);
  } catch (err) {
    console.error("Error fetching release plans:", err);
    res.status(500).json({ error: "Internal server error." });
  }
});

router.post("/api/refresh", async (req, res) => {
  try {
    await refreshReleasePlansCache();
    cache.prDetails.clear();
    cache.prStatuses.clear();
    res.json({ ok: true, fetchedAt: cache.releasePlans.fetchedAt });
  } catch (err) {
    console.error("Refresh error:", err.message);
    res.status(500).json({ error: "Internal server error." });
  }
});

router.post("/api/refresh-plan/:id", async (req, res) => {
  try {
    const wiId = parseInt(req.params.id, 10);
    if (!wiId) return res.status(400).json({ error: "Invalid work item ID" });

    const wiUrl = `${DEVOPS_ORG}/_apis/wit/workitems?ids=${wiId}&$expand=All&api-version=${API_VERSION}`;
    const wiResult = await devopsRequest(wiUrl, "GET");
    const workItems = wiResult.value || [];
    if (!workItems.length) return res.status(404).json({ error: "Work item not found" });
    const wi = workItems[0];

    const childIds = extractChildIds(wi);
    const apiSpecMap = {};
    if (childIds.length) {
      const childItems = await fetchWorkItemsBatch(childIds, API_SPEC_FIELDS);
      for (const child of childItems) {
        if ((child.fields["System.WorkItemType"] || "") === "API Spec") apiSpecMap[child.id] = child;
      }
    }

    const plan = mapReleasePlan(wi, apiSpecMap);
    await enrichPlans([plan]);

    // Invalidate PR caches for this plan's SDK PRs
    for (const [, langData] of Object.entries(plan.languages || {})) {
      if (langData.sdkPrUrl) {
        cache.prDetails.delete(langData.sdkPrUrl);
        cache.prStatuses.delete(langData.sdkPrUrl);
      }
    }

    // Update the plan in the global cache
    if (cache.releasePlans.data && cache.releasePlans.data.plans) {
      const idx = cache.releasePlans.data.plans.findIndex(p => p.id === wiId);
      if (idx >= 0) {
        cache.releasePlans.data.plans[idx] = plan;
      } else {
        cache.releasePlans.data.plans.push(plan);
      }
    }

    res.json({ plan });
  } catch (err) {
    console.error("Refresh plan error:", err);
    res.status(500).json({ error: "Internal server error." });
  }
});

router.get("/api/previous-sdk-prs/:id", async (req, res) => {
  try {
    const wiId = parseInt(req.params.id, 10);
    if (!wiId) return res.status(400).json({ error: "Invalid work item ID" });
    const previousPrs = {};
    for (const lang of LANGUAGES) previousPrs[LANGUAGE_DISPLAY[lang]] = [];
    let continuationToken = null;
    do {
      const tokenParam = continuationToken ? `&continuationToken=${encodeURIComponent(continuationToken)}` : "";
      const url = `${DEVOPS_ORG}/${DEVOPS_PROJECT}/_apis/wit/workitems/${wiId}/updates?api-version=${API_VERSION}${tokenParam}`;
      const { body: result, headers } = await devopsRequest(url, "GET", null, { returnHeaders: true });
      const updates = result.value || [];
      for (const upd of updates) {
        if (!upd.fields) continue;
        for (const lang of LANGUAGES) {
          const fieldName = `Custom.SDKPullRequestFor${lang}`;
          const change = upd.fields[fieldName];
          if (!change) continue;
          const oldVal = (change.oldValue || "").trim().replace(/\/+$/, "");
          if (oldVal && GITHUB_PR_URL_PATTERN.test(oldVal)) {
            const displayLang = LANGUAGE_DISPLAY[lang];
            if (!previousPrs[displayLang].includes(oldVal)) previousPrs[displayLang].push(oldVal);
          }
        }
      }
      continuationToken = headers["x-ms-continuationtoken"] || null;
    } while (continuationToken);
    // Remove values that match the current PR
    if (cache.releasePlans.data) {
      const plan = cache.releasePlans.data.plans.find(p => p.id === wiId);
      if (plan && plan.languages) {
        for (const [lang, langData] of Object.entries(plan.languages)) {
          if (langData.sdkPrUrl && previousPrs[lang]) {
            previousPrs[lang] = previousPrs[lang].filter(u => u !== langData.sdkPrUrl);
          }
        }
      }
    }
    res.json({ previousPrs });
  } catch (err) {
    console.error("Previous SDK PRs error:", err);
    res.status(500).json({ error: "Internal server error." });
  }
});

router.post("/api/pr-statuses", async (req, res) => {
  try {
    const urls = (req.body && req.body.urls) || [];
    if (!Array.isArray(urls)) return res.status(400).json({ error: "urls must be an array." });
    const hasGhToken = !!(process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN);
    if (!urls.length || !hasGhToken) return res.json({ statuses: {} });
    const unique = [...new Set(urls.filter(u => typeof u === "string" && parseGitHubPrUrl(u)))];
    if (!unique.length) return res.json({ statuses: {} });
    const result = {};
    const now = Date.now();
    const toFetch = [];
    for (const url of unique) {
      const entry = cache.prStatuses.get(url);
      if (entry && (now - entry.updatedAt) < CACHE_TTL_MS) {
        result[url] = entry.data;
      } else {
        toFetch.push(url);
      }
    }
    if (toFetch.length) {
      const statusMap = await batchFetchPrStatuses(toFetch);
      for (const url of toFetch) {
        const st = statusMap.get(url) || null;
        if (st) cache.prStatuses.set(url, { data: st, updatedAt: now });
        result[url] = st;
      }
      evictOldest(cache.prStatuses);
    }
    res.json({ statuses: result });
  } catch (err) {
    console.error("PR statuses error:", err);
    res.status(500).json({ error: "Internal server error." });
  }
});

router.post("/api/pr-details", async (req, res) => {
  try {
    const urls = (req.body && req.body.urls) || [];
    if (!Array.isArray(urls)) return res.status(400).json({ error: "urls must be an array." });
    const hasGhToken = !!(process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN);
    if (!urls.length || !hasGhToken) return res.json({ details: {} });
    const unique = [...new Set(urls.filter(u => typeof u === "string" && parseGitHubPrUrl(u)))];
    if (!unique.length) return res.json({ details: {} });
    const result = {};
    const now = Date.now();
    const toFetch = [];
    for (const url of unique) {
      const entry = cache.prDetails.get(url);
      if (entry && (now - entry.updatedAt) < PR_DETAIL_CACHE_TTL_MS) {
        result[url] = entry.data;
      } else {
        toFetch.push(url);
      }
    }
    if (toFetch.length) {
      const [statusMap, detailMap] = await Promise.all([batchFetchPrStatuses(toFetch), batchFetchPrDetails(toFetch)]);
      for (const url of toFetch) {
        const details = detailMap.get(url) || null;
        const status = statusMap.get(url) || null;
        const entry = {
          gitHubStatus: status,
          prDetails: details ? { mergeable: details.mergeable, mergeableState: details.mergeableState, isApproved: details.isApproved, approvedBy: details.approvedBy, failedChecks: details.failedChecks, apiViewUrl: details.apiViewUrl || "", title: details.title || "", requestedReviewers: details.requestedReviewers || [], latestComment: details.latestComment || null, updatedAt: details.updatedAt || "" } : null,
        };
        cache.prDetails.set(url, { data: entry, updatedAt: now });
        if (status) cache.prStatuses.set(url, { data: status, updatedAt: now });
        result[url] = entry;
      }
      evictOldest(cache.prDetails);
      evictOldest(cache.prStatuses);
    }
    res.json({ details: result });
  } catch (err) {
    console.error("PR details error:", err);
    res.status(500).json({ error: "Internal server error." });
  }
});

// Expose refreshReleasePlansCache for startup use
router.refreshReleasePlansCache = refreshReleasePlansCache;

export default router;
