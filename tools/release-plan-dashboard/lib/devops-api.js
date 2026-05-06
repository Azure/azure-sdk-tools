import { DefaultAzureCredential } from "@azure/identity";

// ══════════════════════════════════════════════════════════════
// ── Azure DevOps helpers ──────────────────────────────────────
// ══════════════════════════════════════════════════════════════

const DEVOPS_ORG = "https://dev.azure.com/azure-sdk";
const DEVOPS_PROJECT = "Release";
const API_VERSION = "7.1";
const BATCH_SIZE = 200;
const WIQL_BATCH_SIZE = 50;
const DEVOPS_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"; // Azure DevOps resource ID
const HIERARCHY_FORWARD_LINK = "System.LinkTypes.Hierarchy-Forward";
const WORK_ITEM_ID_REGEX = /\/workItems\/(\d+)$/;
const GITHUB_PR_URL_PATTERN = /github\.com\/.*\/pull\/\d+/;
const HREF_REGEX = /href="([^"]+)"/g;
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const credential = new DefaultAzureCredential();

const LANGUAGES = ["Dotnet", "JavaScript", "Python", "Java", "Go"];
const LANGUAGE_DISPLAY = {
  Dotnet: ".NET", JavaScript: "JavaScript", Python: "Python", Java: "Java", Go: "Go",
};
const LANGUAGE_PACKAGE_WI = {
  ".NET": ".NET", JavaScript: "JavaScript", Python: "Python", Java: "Java", Go: "Go",
};

const RELEASE_PLAN_FIELDS = [
  "System.Id", "System.Title", "System.State", "System.CreatedDate", "System.ChangedDate", "System.CreatedBy",
  "Custom.SDKReleasemonth", "Custom.SDKtypetobereleased", "Custom.ReleasePlanID", "Custom.ReleasePlanLink",
  "Custom.ReleasePlanSubmittedby", "Custom.PrimaryPM", "Custom.ApiSpecProjectPath",
  "Custom.MgmtScope", "Custom.DataScope", "Custom.SDKLanguages", "Custom.APISpecApprovalStatus",
  "Custom.ProductName", "Custom.ProductLifecycle", "Custom.ServiceName", "Custom.ReleasePlanType",
  "Custom.CreatedUsing", "Custom.ProductServiceTreeID", "Custom.ProductServiceTreeLink",
];
for (const lang of LANGUAGES) {
  RELEASE_PLAN_FIELDS.push(
    `Custom.SDKGenerationPipelineFor${lang}`, `Custom.SDKPullRequestFor${lang}`,
    `Custom.${lang}PackageName`, `Custom.GenerationStatusFor${lang}`,
    `Custom.ReleaseStatusFor${lang}`, `Custom.SDKPullRequestStatusFor${lang}`,
    `Custom.ReleaseExclusionStatusFor${lang}`, `Custom.ReleasedVersionFor${lang}`
  );
}

const API_SPEC_FIELDS = [
  "System.Id", "System.Title", "System.WorkItemType",
  "Custom.ActiveSpecPullRequestUrl", "Custom.RESTAPIReviews", "Custom.APISpecversion", "Custom.APISpecDefinitionType",
];

const PACKAGE_FIELDS = [
  "System.Id", "System.ChangedDate", "Custom.Package", "Custom.Language",
  "Custom.PackageVersion", "Custom.APIReviewStatus", "Custom.PackageNameApprovalStatus",
];

/** Fetches an Azure DevOps auth header using Managed Identity (DefaultAzureCredential). */
async function getAuthHeader() {
  const tokenResponse = await credential.getToken(DEVOPS_SCOPE);
  return "Bearer " + tokenResponse.token;
}

/**
 * Makes an authenticated request to Azure DevOps.
 * @param {string} urlPath - Full API URL
 * @param {string} [method="GET"] - HTTP method
 * @param {object} [body] - Request body (will be JSON-serialized)
 * @param {{ returnHeaders?: boolean }} [options] - If returnHeaders is true, returns { body, headers }
 */
async function devopsRequest(urlPath, method, body, options) {
  const authHeader = await getAuthHeader();
  const fetchOptions = {
    method: method || "GET",
    headers: { Authorization: authHeader, "Content-Type": "application/json", Accept: "application/json" },
    signal: AbortSignal.timeout(30000),
  };
  if (body) fetchOptions.body = JSON.stringify(body);
  const response = await fetch(urlPath, fetchOptions);
  const text = await response.text();
  if (!response.ok) {
    throw new Error(`DevOps ${response.status}: ${text.substring(0, 500)}`);
  }
  const parsed = (() => { try { return JSON.parse(text); } catch { return text; } })();
  if (options && options.returnHeaders) {
    return { body: parsed, headers: Object.fromEntries(response.headers.entries()) };
  }
  return parsed;
}

/** Executes a WIQL query and returns matching work item IDs. */
async function runWiql(query) {
  const url = `${DEVOPS_ORG}/${DEVOPS_PROJECT}/_apis/wit/wiql?api-version=${API_VERSION}`;
  const result = await devopsRequest(url, "POST", { query });
  return result.workItems ? result.workItems.map(wi => wi.id) : [];
}

/** Fetches work items by IDs in batches of BATCH_SIZE, with optional field selection. */
async function fetchWorkItemsBatch(ids, fields) {
  if (!ids.length) return [];
  const allItems = [];
  for (let i = 0; i < ids.length; i += BATCH_SIZE) {
    const batch = ids.slice(i, i + BATCH_SIZE);
    const fieldsParam = fields ? `&fields=${fields.join(",")}` : "";
    const expand = fields ? "" : "&$expand=All";
    const url = `${DEVOPS_ORG}/_apis/wit/workitems?ids=${batch.join(",")}${expand}${fieldsParam}&api-version=${API_VERSION}`;
    const result = await devopsRequest(url, "GET");
    if (result.value) allItems.push(...result.value);
  }
  return allItems;
}

/** Extracts child work item IDs from hierarchy-forward relations. */
function extractChildIds(workItem) {
  const ids = [];
  if (workItem.relations) {
    for (const relation of workItem.relations) {
      if (relation.rel === HIERARCHY_FORWARD_LINK && relation.url) {
        const match = relation.url.match(WORK_ITEM_ID_REGEX);
        if (match) ids.push(parseInt(match[1], 10));
      }
    }
  }
  return ids;
}

function getField(workItem, name) { return workItem.fields ? workItem.fields[name] : undefined; }

/** Strips email addresses and normalizes display names from DevOps identity fields. */
function stripEmail(val) {
  if (!val) return "";
  let cleaned = val.replace(/<[^>]*@[^>]*>/g, "").trim();
  if (EMAIL_REGEX.test(cleaned)) {
    return cleaned.split("@")[0].replace(/[._]/g, " ");
  }
  return cleaned;
}

/** Extracts GitHub PR URLs from the RESTAPIReviews HTML field. */
function extractSpecPrUrls(reviewsHtml) {
  const urls = [];
  let match;
  // Reset lastIndex since HREF_REGEX has the global flag
  HREF_REGEX.lastIndex = 0;
  while ((match = HREF_REGEX.exec(reviewsHtml)) !== null) {
    const url = match[1].trim().replace(/\/+$/, "");
    if (GITHUB_PR_URL_PATTERN.test(url) && !urls.includes(url)) urls.push(url);
  }
  return urls;
}

/**
 * Maps a DevOps work item into a normalized release plan object.
 * Extracts language-specific fields, API spec data, and identity information.
 */
function mapReleasePlan(workItem, apiSpecMap) {
  const fields = workItem.fields || {};
  const id = workItem.id || fields["System.Id"];
  const languages = {};
  for (const lang of LANGUAGES) {
    languages[LANGUAGE_DISPLAY[lang]] = {
      packageName: fields[`Custom.${lang}PackageName`] || "",
      sdkPrUrl: (fields[`Custom.SDKPullRequestFor${lang}`] || "").trim().replace(/\/+$/, ""),
      prStatus: fields[`Custom.SDKPullRequestStatusFor${lang}`] || "",
      releaseStatus: fields[`Custom.ReleaseStatusFor${lang}`] || "",
      exclusionStatus: fields[`Custom.ReleaseExclusionStatusFor${lang}`] || "",
      generationStatus: fields[`Custom.GenerationStatusFor${lang}`] || "",
      releasedVersion: fields[`Custom.ReleasedVersionFor${lang}`] || "",
    };
  }
  const childIds = extractChildIds(workItem);
  let apiSpec = null;
  for (const childId of childIds) {
    const specWi = apiSpecMap[childId];
    if (specWi) {
      const specFields = specWi.fields || {};
      let specPrUrl = (specFields["Custom.ActiveSpecPullRequestUrl"] || "").trim().replace(/\/+$/, "");
      const reviewsHtml = specFields["Custom.RESTAPIReviews"] || "";
      const allSpecPrUrls = extractSpecPrUrls(reviewsHtml);
      if (!specPrUrl && allSpecPrUrls.length) specPrUrl = allSpecPrUrls[0];
      const previousSpecPrUrls = allSpecPrUrls.filter(u => u !== specPrUrl);
      apiSpec = { id: childId, specPrUrl, previousSpecPrUrls, apiVersion: specFields["Custom.APISpecversion"] || "", definitionType: specFields["Custom.APISpecDefinitionType"] || "" };
      break;
    }
  }
  const createdBy = fields["System.CreatedBy"];
  const createdByName = typeof createdBy === "object" ? createdBy.displayName || "" : "";
  const rawSubmittedBy = fields["Custom.ReleasePlanSubmittedby"];
  const submittedByName = typeof rawSubmittedBy === "object" && rawSubmittedBy
    ? rawSubmittedBy.displayName || rawSubmittedBy.uniqueName || ""
    : (rawSubmittedBy || "");
  return {
    id, title: fields["System.Title"] || "", state: fields["System.State"] || "",
    createdDate: fields["System.CreatedDate"] || "", changedDate: fields["System.ChangedDate"] || "",
    createdBy: stripEmail(createdByName),
    releaseMonth: fields["Custom.SDKReleasemonth"] || "", releaseType: fields["Custom.SDKtypetobereleased"] || "",
    releasePlanId: fields["Custom.ReleasePlanID"] || "", releasePlanLink: fields["Custom.ReleasePlanLink"] || "",
    submittedBy: (submittedByName || createdByName),
    ownerPM: stripEmail(fields["Custom.PrimaryPM"] || ""),
    typeSpecPath: fields["Custom.ApiSpecProjectPath"] || "",
    mgmtScope: fields["Custom.MgmtScope"] || "", dataScope: fields["Custom.DataScope"] || "",
    sdkLanguages: fields["Custom.SDKLanguages"] || "",
    specApprovalStatus: fields["Custom.APISpecApprovalStatus"] || "",
    productName: fields["Custom.ProductName"] || "", productLifecycle: fields["Custom.ProductLifecycle"] || "",
    releasePlanType: fields["Custom.ReleasePlanType"] || "",
    serviceName: fields["Custom.ServiceName"] || "",
    createdUsing: fields["Custom.CreatedUsing"] || "",
    productId: fields["Custom.ProductServiceTreeID"] || "",
    productServiceTreeLink: fields["Custom.ProductServiceTreeLink"] || "",
    languages, apiSpec,
  };
}

// ── Package work item helpers ─────────────────────────────────

async function fetchPackageWorkItems(pkgLangPairs) {
  if (!pkgLangPairs.length) return new Map();
  const uniquePkgs = [...new Set(pkgLangPairs.map(p => p.pkg))].filter(Boolean);
  if (!uniquePkgs.length) return new Map();
  const resultMap = new Map();
  for (let i = 0; i < uniquePkgs.length; i += WIQL_BATCH_SIZE) {
    const batch = uniquePkgs.slice(i, i + WIQL_BATCH_SIZE);
    const conds = batch.map(p => `[Custom.Package] = '${p.replace(/'/g, "''")}'`).join(" OR ");
    const query = `SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'Release' AND [System.WorkItemType] = 'Package' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned') AND (${conds}) ORDER BY [System.ChangedDate] DESC`;
    try {
      const ids = await runWiql(query);
      if (!ids.length) continue;
      const items = await fetchWorkItemsBatch(ids, PACKAGE_FIELDS);
      for (const item of items) {
        const itemFields = item.fields || {};
        const key = `${itemFields["Custom.Package"] || ""}|${itemFields["Custom.Language"] || ""}`;
        const existing = resultMap.get(key);
        const changedDate = new Date(itemFields["System.ChangedDate"] || 0);
        if (!existing || changedDate > existing._changedDate) {
          resultMap.set(key, { _changedDate: changedDate, version: itemFields["Custom.PackageVersion"] || "", apiReviewStatus: itemFields["Custom.APIReviewStatus"] || "", namespaceApproval: itemFields["Custom.PackageNameApprovalStatus"] || "" });
        }
      }
    } catch (err) { console.warn("Package WI error:", err.message); }
  }
  return resultMap;
}

async function fetchAzureSdkPackageList() {
  try {
    const response = await fetch("https://azure.github.io/azure-sdk/");
    if (!response.ok) return "";
    return await response.text();
  } catch { return ""; }
}

function isKnownPackage(name, page) { return name && page && page.toLowerCase().includes(name.toLowerCase()); }

/** Checks if a version string represents a GA (non-preview) release. */
function isGAVersion(version) {
  if (!version) return false;
  const lower = version.toLowerCase();
  return !lower.includes("beta") && !lower.includes("alpha") && !lower.includes("preview") && !lower.includes("rc") && !/[-.]b\d/.test(lower);
}

export {
  DEVOPS_ORG,
  DEVOPS_PROJECT,
  API_VERSION,
  LANGUAGES,
  LANGUAGE_DISPLAY,
  LANGUAGE_PACKAGE_WI,
  RELEASE_PLAN_FIELDS,
  API_SPEC_FIELDS,
  PACKAGE_FIELDS,
  devopsRequest,
  runWiql,
  fetchWorkItemsBatch,
  extractChildIds,
  getField,
  mapReleasePlan,
  fetchPackageWorkItems,
  fetchAzureSdkPackageList,
  isKnownPackage,
  isGAVersion,
};
