#!/usr/bin/env node

const { execFileSync } = require("node:child_process");
const { existsSync } = require("node:fs");
const {
  concurrentMap,
  parseArgs,
  readJson,
  writeJson,
} = require("./lib/v2-common");

const args = parseArgs(process.argv.slice(2), {
  input: "cache/v2/release-plans.json",
  output: "cache/v2/github-prs.json",
  concurrency: 2,
  maxPages: 10,
});

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});

async function main() {
  const releasePlans = readJson(args.input);
  const previous = existsSync(args.output) ? readJson(args.output) : null;
  const cachedPrs = new Map(
    (previous?.prs || []).map((pr) => [pr.id, pr]),
  );
  const prs = new Map();
  for (const plan of releasePlans.plans) {
    for (const pr of plan.specPrs) prs.set(pr.id, pr);
    for (const language of plan.languages) {
      for (const pr of language.sdkPrHistory || [language.sdkPr].filter(Boolean))
        prs.set(pr.id, pr);
    }
  }
  const token = execFileSync("gh", ["auth", "token"], {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "inherit"],
  }).trim();
  let cacheHits = 0;
  const results = await concurrentMap(
    [...prs.values()],
    args.concurrency,
    async (pr) => {
      const cached = cachedPrs.get(pr.id);
      if (cached && ["merged", "closed"].includes(cached.state)) {
        try {
          const value = Array.isArray(cached.releasePlanIds)
            ? cached
            : {
                ...cached,
                releasePlanIds: await collectReleasePlanIds(pr, token),
              };
          cacheHits++;
          return { pr: value, skipped: null };
        } catch (error) {
          if ([401, 403, 429].includes(error.status)) throw error;
        }
      }
      try {
        return { pr: await collectPr(pr, token), skipped: null };
      } catch (error) {
        if ([401, 403, 429].includes(error.status)) throw error;
        return {
          pr: unavailablePr(pr, error.message),
          skipped: {
            id: pr.id,
            url: pr.url,
            reason: error.message.slice(0, 300),
          },
        };
      }
    },
  );
  const values = results.map((result) => result.pr);
  const skippedPrs = results.map((result) => result.skipped).filter(Boolean);
  writeJson(args.output, {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    prCount: values.length,
    cacheHits,
    skippedPrCount: skippedPrs.length,
    skippedPrs,
    prs: values,
  });
  console.log(
    `Collected ${values.length} GitHub PRs (${cacheHits} cached, ${skippedPrs.length} skipped) into ${args.output}`,
  );
}

async function collectReleasePlanIds(pr, token) {
  const metadata = await github(
    `https://api.github.com/repos/${pr.owner}/${pr.repo}/pulls/${pr.number}`,
    token,
  );
  return releasePlanIds(metadata.body);
}

async function collectPr(pr, token) {
  const base = `https://api.github.com/repos/${pr.owner}/${pr.repo}`;
  const [metadata, reviews, issueComments, reviewComments, commits] =
    await Promise.all([
      github(`${base}/pulls/${pr.number}`, token),
      githubPages(`${base}/pulls/${pr.number}/reviews`, token, args.maxPages),
      githubPages(`${base}/issues/${pr.number}/comments`, token, args.maxPages),
      githubPages(`${base}/pulls/${pr.number}/comments`, token, args.maxPages),
      githubPages(`${base}/pulls/${pr.number}/commits`, token, args.maxPages),
    ]);
  return {
    ...pr,
    title: metadata.title,
    state: metadata.merged_at ? "merged" : metadata.state,
    draft: metadata.draft,
    author: actor(metadata.user),
    labels: metadata.labels.map((label) => label.name),
    releasePlanIds: releasePlanIds(metadata.body),
    createdAt: metadata.created_at,
    updatedAt: metadata.updated_at,
    mergedAt: metadata.merged_at,
    closedAt: metadata.closed_at,
    requestedReviewers: metadata.requested_reviewers.map(actor),
    reviews: reviews.map((review) => ({
      id: review.id,
      state: review.state,
      submittedAt: review.submitted_at,
      actor: actor(review.user),
      excerpt: excerpt(review.body),
      url: review.html_url,
    })),
    issueComments: issueComments.map((comment) => ({
      id: comment.id,
      createdAt: comment.created_at,
      updatedAt: comment.updated_at,
      actor: actor(comment.user),
      excerpt: excerpt(comment.body),
      url: comment.html_url,
    })),
    reviewComments: reviewComments.map((comment) => ({
      id: comment.id,
      createdAt: comment.created_at,
      actor: actor(comment.user),
      excerpt: excerpt(comment.body),
      url: comment.html_url,
    })),
    commits: commits.map((commit) => ({
      id: commit.sha,
      createdAt:
        commit.commit?.author?.date || commit.commit?.committer?.date || null,
      actor: actor(commit.author),
      url: commit.html_url,
    })),
  };
}

function releasePlanIds(value) {
  const text = String(value || "");
  const ids = new Set();
  for (const pattern of [
    /[?&]releasePlan=(\d+)/gi,
    /\/_workitems\/edit\/(\d+)/gi,
  ]) {
    for (const match of text.matchAll(pattern)) ids.add(match[1]);
  }
  return [...ids];
}

async function github(url, token) {
  const response = await fetch(url, {
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: "application/vnd.github+json",
      "X-GitHub-Api-Version": "2022-11-28",
    },
    signal: AbortSignal.timeout(45_000),
  });
  if (!response.ok) {
    const error = new Error(`GitHub ${response.status} for ${url}`);
    error.status = response.status;
    throw error;
  }
  return response.json();
}

async function githubPages(url, token, maxPages) {
  const values = [];
  for (let page = 1; ; page++) {
    if (page > maxPages)
      throw new Error(
        `Outlier skipped: ${url} exceeded ${maxPages * 100} records`,
      );
    const join = url.includes("?") ? "&" : "?";
    const batch = await github(`${url}${join}per_page=100&page=${page}`, token);
    values.push(...batch);
    if (batch.length < 100) break;
  }
  return values;
}

function unavailablePr(pr, reason) {
  return {
    ...pr,
    title: "PR enrichment unavailable",
    state: "unavailable",
    draft: false,
    author: { kind: "unknown", publicId: null },
    labels: [],
    releasePlanIds: [],
    createdAt: null,
    updatedAt: null,
    mergedAt: null,
    closedAt: null,
    requestedReviewers: [],
    reviews: [],
    issueComments: [],
    reviewComments: [],
    commits: [],
    collectionError: reason.slice(0, 300),
  };
}

function actor(user) {
  if (!user) return { kind: "unknown", publicId: null };
  return {
    kind: user.type === "Bot" || /\[bot\]$/i.test(user.login) ? "bot" : "human",
    publicId: user.login,
  };
}

function excerpt(value) {
  return String(value || "")
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, 240);
}
