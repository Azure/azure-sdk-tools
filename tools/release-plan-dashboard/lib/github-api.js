import { Octokit } from "@octokit/rest";

// ══════════════════════════════════════════════════════════════
// ── GitHub API helpers (for release plan data) ────────────────
// ══════════════════════════════════════════════════════════════

const GITHUB_API_TIMEOUT_MS = 30000;
const API_PAGE_SIZE = 100;
const APIVIEW_URL_REGEX = /https:\/\/(?:spa\.)?apiview\.dev\/[^\s)\]"<>]+/;
const GITHUB_PR_URL_REGEX = /^https?:\/\/(www\.)?github\.com\/([^/]+)\/([^/]+)\/pull\/(\d+)/;
const TSPCONFIG_MARKERS = ["tspconfig.yaml", "main.tsp", "client.tsp"];
// Check run conclusions that are considered non-failures
const PASSING_CONCLUSIONS = ["success", "skipped", "neutral", "cancelled"];

function getOctokit(token) {
  const auth = token || process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN;
  if (!auth) return null;
  return new Octokit({
    auth,
    userAgent: "release-plan-dashboard",
    request: { timeout: GITHUB_API_TIMEOUT_MS },
    retry: { enabled: true, retries: 2 },
    throttle: { enabled: false },
  });
}


/** Parses a GitHub PR URL into { owner, repo, number } or null if invalid. */
function parseGitHubPrUrl(url) {
  if (!url) return null;
  const match = url.match(GITHUB_PR_URL_REGEX);
  return match ? { owner: match[2], repo: match[3], number: match[4] } : null;
}

/**
 * Extracts PR status from GitHub API data.
 * Priority: merged > closed > draft > state — closed is checked before draft
 * because GitHub keeps draft=true on closed draft PRs.
 */
function _extractPrStatus(data) {
  if (!data) return null;
  if (data.merged_at || data.merged) return "merged";
  if (data.state === "closed") return "closed";
  if (data.draft) return "draft";
  return data.state || "unknown";
}

async function getGitHubPrStatus(prUrl) {
  const pr = parseGitHubPrUrl(prUrl);
  if (!pr) return null;
  const octokit = getOctokit();
  if (!octokit) return null;
  try {
    const { data } = await octokit.pulls.get({ owner: pr.owner, repo: pr.repo, pull_number: Number(pr.number) });
    return _extractPrStatus(data);
  } catch (err) {
    console.warn(`GitHub PR status error ${prUrl}:`, err.message);
    return null;
  }
}

async function getGitHubPrDetails(prUrl) {
  const pr = parseGitHubPrUrl(prUrl);
  if (!pr) return null;
  const octokit = getOctokit();
  if (!octokit) return null;
  try {
    const [{ data: prData }, { data: reviews }] = await Promise.all([
      octokit.pulls.get({ owner: pr.owner, repo: pr.repo, pull_number: Number(pr.number) }),
      octokit.pulls.listReviews({ owner: pr.owner, repo: pr.repo, pull_number: Number(pr.number) }),
    ]);
    if (!prData) return null;
    return _buildPrDetailsResult(prData, reviews, pr, octokit);
  } catch (err) {
    console.warn(`GitHub PR details error ${prUrl}:`, err.message);
    return null;
  }
}

/** Extracts check run names that have failed (non-passing conclusions). */
function extractFailedChecks(checkRuns) {
  return checkRuns
    .filter(cr => cr.status === "completed" && cr.conclusion && !PASSING_CONCLUSIONS.includes(cr.conclusion))
    .map(cr => cr.name);
}

/** Extracts unique approver logins from PR review data. */
function extractApprovers(reviews) {
  const approvers = new Set();
  if (Array.isArray(reviews)) {
    for (const review of reviews) {
      if (review.state === "APPROVED" && review.user) approvers.add(review.user.login);
    }
  }
  return [...approvers];
}

/** Finds the first APIView URL in PR comments, and the latest non-bot comment. */
function extractCommentData(comments) {
  let latestComment = null;
  let apiViewUrl = "";
  if (!Array.isArray(comments)) return { latestComment, apiViewUrl };

  // Find latest non-bot comment (search from end)
  for (let i = comments.length - 1; i >= 0; i--) {
    const comment = comments[i];
    const login = (comment.user && comment.user.login) || "";
    const isBot = login.includes("[bot]") || login.includes("bot") || (comment.user && comment.user.type === "Bot");
    if (!isBot && comment.body) {
      latestComment = { author: login, body: comment.body.substring(0, 300), createdAt: comment.created_at || "" };
      break;
    }
  }

  // Find APIView URL
  for (const comment of comments) {
    const body = comment.body || "";
    if (body.includes("API Change Check") || body.includes("APIView") || body.includes("apiview")) {
      const urlMatch = body.match(APIVIEW_URL_REGEX);
      if (urlMatch) { apiViewUrl = urlMatch[0]; break; }
    }
  }

  return { latestComment, apiViewUrl };
}

/** Builds a normalized PR details result from GitHub API data. */
async function _buildPrDetailsResult(prData, reviews, pr, octokit) {
  const approvers = extractApprovers(reviews);
  const result = {
    mergeable: prData.mergeable || false, mergeableState: prData.mergeable_state || "",
    isApproved: approvers.length > 0, approvedBy: approvers, failedChecks: [], apiViewUrl: "",
    title: prData.title || "", requestedReviewers: [], latestComment: null,
    updatedAt: prData.updated_at || "",
  };
  if (Array.isArray(prData.requested_reviewers)) {
    result.requestedReviewers = prData.requested_reviewers.map(reviewer => reviewer.login).filter(Boolean);
  }

  const headSha = prData.head && prData.head.sha;
  if (headSha) {
    try {
      const { data: checks } = await octokit.checks.listForRef({ owner: pr.owner, repo: pr.repo, ref: headSha, per_page: API_PAGE_SIZE });
      if (checks && Array.isArray(checks.check_runs)) {
        result.failedChecks = extractFailedChecks(checks.check_runs);
      }
    } catch { /* check runs may not be available for all repos */ }
  }

  try {
    const { data: comments } = await octokit.issues.listComments({ owner: pr.owner, repo: pr.repo, issue_number: Number(pr.number), per_page: API_PAGE_SIZE });
    const commentData = extractCommentData(comments);
    result.latestComment = commentData.latestComment;
    result.apiViewUrl = commentData.apiViewUrl;
  } catch (err) { console.warn("APIView comment fetch error:", err.message); }

  return result;
}

/** Processes items in parallel with concurrency control and delay between batches. */
async function throttledMap(items, fn, { concurrency = 10, delayMs = 50 } = {}) {
  const results = [];
  for (let i = 0; i < items.length; i += concurrency) {
    const chunk = items.slice(i, i + concurrency);
    results.push(...await Promise.all(chunk.map(fn)));
    if (i + concurrency < items.length) await new Promise(r => setTimeout(r, delayMs));
  }
  return results;
}

/** Fetches PR statuses for a list of URLs, deduplicating and batching requests. */
async function batchFetchPrStatuses(urls) {
  const unique = [...new Set(urls.filter(Boolean))];
  const statusMap = new Map();
  if (!unique.length || !(process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN)) return statusMap;
  await throttledMap(unique, async (url) => {
    try { statusMap.set(url, await getGitHubPrStatus(url)); } catch { statusMap.set(url, null); }
  }, { concurrency: 10, delayMs: 50 });
  return statusMap;
}

/** Fetches detailed PR information for a list of URLs, deduplicating and batching requests. */
async function batchFetchPrDetails(urls) {
  const unique = [...new Set(urls.filter(Boolean))];
  const detailsMap = new Map();
  if (!unique.length || !(process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN)) return detailsMap;
  await throttledMap(unique, async (url) => {
    try { detailsMap.set(url, await getGitHubPrDetails(url)); } catch { detailsMap.set(url, null); }
  }, { concurrency: 10, delayMs: 50 });
  return detailsMap;
}

async function getGitHubPrFiles(prUrl) {
  const pr = parseGitHubPrUrl(prUrl);
  if (!pr) return [];
  const octokit = getOctokit();
  if (!octokit) return [];
  try {
    const files = await octokit.paginate(octokit.pulls.listFiles, {
      owner: pr.owner, repo: pr.repo, pull_number: Number(pr.number), per_page: 100,
    }, response => response.data.map(f => f.filename));
    return files;
  } catch (err) {
    console.warn(`GitHub PR files error ${prUrl}:`, err.message);
    return [];
  }
}

/** Derives the TypeSpec project path from a list of changed files in a spec PR. */
function deriveSpecProjectPath(files) {
  if (!files || !files.length) return "";
  for (const marker of TSPCONFIG_MARKERS) {
    const match = files.find(f => f.endsWith("/" + marker) || f === marker);
    if (match) { const idx = match.lastIndexOf("/"); return idx >= 0 ? match.substring(0, idx) : ""; }
  }
  const dirs = files.map(f => { const i = f.lastIndexOf("/"); return i >= 0 ? f.substring(0, i) : ""; }).filter(Boolean);
  if (!dirs.length) return "";
  let common = dirs[0];
  for (let i = 1; i < dirs.length; i++) {
    while (common && !dirs[i].startsWith(common)) { const idx = common.lastIndexOf("/"); common = idx >= 0 ? common.substring(0, idx) : ""; }
    if (!common) break;
  }
  return common;
}

async function batchFetchSpecProjectPaths(urls) {
  const unique = [...new Set(urls.filter(Boolean))];
  const pathMap = new Map();
  if (!unique.length || !(process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN)) return pathMap;
  await throttledMap(unique, async (url) => {
    try { const files = await getGitHubPrFiles(url); pathMap.set(url, deriveSpecProjectPath(files)); } catch { pathMap.set(url, ""); }
  }, { concurrency: 10, delayMs: 50 });
  return pathMap;
}

/** Regex patterns for spec PR labels worth highlighting on cards. */
const SPEC_LABEL_PATTERNS = [/breakingchange/i, /\bARM\b/i, /\bAPI\b/i];

/** Fetches labels for a single GitHub PR URL. Returns an array of { name, color } objects. */
async function getGitHubPrLabels(prUrl) {
  const pr = parseGitHubPrUrl(prUrl);
  if (!pr) return [];
  const octokit = getOctokit();
  if (!octokit) return [];
  try {
    const { data } = await octokit.issues.listLabelsOnIssue({
      owner: pr.owner, repo: pr.repo, issue_number: Number(pr.number), per_page: API_PAGE_SIZE,
    });
    return (data || []).map(label => ({ name: label.name, color: label.color || "" }));
  } catch (err) {
    console.warn(`GitHub PR labels error ${prUrl}:`, err.message);
    return [];
  }
}

/**
 * Fetches labels for a batch of spec PR URLs and filters to only labels matching
 * known patterns (BreakingChange, ARM, API).
 * @returns {Map<string, Array<{name: string, color: string}>>} URL → array of matching labels
 */
async function batchFetchSpecPrLabels(urls) {
  const unique = [...new Set(urls.filter(Boolean))];
  const labelMap = new Map();
  if (!unique.length || !(process.env.GITHUB_PAT_RELEASE_PLAN || process.env.GH_TOKEN)) return labelMap;
  await throttledMap(unique, async (url) => {
    try {
      const allLabels = await getGitHubPrLabels(url);
      const matching = allLabels.filter(label => SPEC_LABEL_PATTERNS.some(re => re.test(label.name)));
      labelMap.set(url, matching);
    } catch { labelMap.set(url, []); }
  }, { concurrency: 10, delayMs: 50 });
  return labelMap;
}

export {
  parseGitHubPrUrl,
  getGitHubPrStatus,
  getGitHubPrDetails,
  batchFetchPrStatuses,
  batchFetchPrDetails,
  batchFetchSpecProjectPaths,
  batchFetchSpecPrLabels,
  getGitHubPrLabels,
  throttledMap,
  _extractPrStatus,
};
