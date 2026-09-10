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
  github: "cache/v2/github-prs.json",
  output: "cache/v2/github-releases.json",
  concurrency: 4,
  matchWindowDays: 14,
});

const REPOSITORIES = {
  ".NET": "azure-sdk-for-net",
  JavaScript: "azure-sdk-for-js",
  Python: "azure-sdk-for-python",
  Java: "azure-sdk-for-java",
  Go: "azure-sdk-for-go",
};
const RELEASE_INDEX_FILES = {
  ".NET": "dotnet",
  JavaScript: "js",
  Python: "python",
  Java: "java",
  Go: "go",
};
const releaseIndexCache = new Map();

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});

async function main() {
  const source = readJson(args.input);
  const githubSource = existsSync(args.github) ? readJson(args.github) : null;
  const prMap = new Map(
    (githubSource?.prs || []).map((pr) => [pr.id, pr]),
  );
  const previous = existsSync(args.output) ? readJson(args.output) : null;
  const cached = new Map(
    (previous?.matches || []).map((match) => [match.id, match]),
  );
  const token = execFileSync("gh", ["auth", "token"], {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "inherit"],
  }).trim();
  const requests = source.plans.flatMap((plan) => {
    if (String(plan.state).toLowerCase() !== "finished") return [];
    return plan.languages.flatMap((language) => {
      const releasedAt = firstReleasedAt(plan, language);
      if (releasedAt && language.releasedVersion) return [];
      return [{
        id: `${plan.id}:${language.id}`,
        planId: plan.id,
        language: language.id,
        package: language.package || null,
        releaseType: plan.releaseType || null,
        sdkPrId: language.sdkPr?.id || null,
        prVersionEvidence: language.sdkPr?.id
          ? JSON.stringify(prMap.get(language.sdkPr.id) || {})
          : "",
        recordedVersion: language.releasedVersion || null,
        observedReleasedAt: releasedAt,
      }];
    });
  });
  let cacheHits = 0;
  const matches = await concurrentMap(
    requests,
    args.concurrency,
    async (request) => {
      const prior = cached.get(request.id);
      if (prior?.status === "matched") {
        cacheHits++;
        return prior;
      }
      return collectRelease(request, token);
    },
  );
  writeJson(args.output, {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    matchWindowDays: args.matchWindowDays,
    requestedCount: requests.length,
    matchedCount: matches.filter((match) => match.status === "matched").length,
    ambiguousCount: matches.filter((match) => match.status === "ambiguous").length,
    missingCount: matches.filter((match) => match.status === "missing").length,
    cacheHits,
    matches,
  });
  console.log(
    `Collected ${matches.filter((match) => match.status === "matched").length} GitHub Release fallbacks (${cacheHits} cached, ${matches.filter((match) => match.status === "ambiguous").length} ambiguous) into ${args.output}`,
  );
}

async function collectRelease(request, token) {
  const repository = REPOSITORIES[request.language];
  const prefix = tagPrefix(request.language, request.package);
  if (!repository || !prefix) {
    return { ...request, status: "missing", reason: "unsupported-package-tag" };
  }
  const exactTag = request.recordedVersion
    ? `${prefix}${String(request.recordedVersion).replace(/^v/, "")}`
    : null;
  if (exactTag) {
    const release = await githubRelease(repository, exactTag, token);
    return release
      ? matched(request, repository, release, "exact-version")
      : { ...request, repository, tagPrefix: prefix, status: "missing", reason: "exact-tag-release-missing" };
  }
  if (!request.observedReleasedAt) {
    return { ...request, repository, tagPrefix: prefix, status: "missing", reason: "missing-version-and-release-observation" };
  }
  const indexed = await indexedReleases(request, repository, token);
  const indexedMatch = uniqueNearbyRelease(
    indexed,
    request.observedReleasedAt,
  );
  if (indexedMatch.status === "matched")
    return matched(
      request,
      repository,
      indexedMatch.release,
      indexedMatch.release.indexMatchMethod,
    );
  if (indexedMatch.status === "ambiguous") {
    return {
      ...request,
      repository,
      tagPrefix: prefix,
      status: "ambiguous",
      reason: "multiple-release-index-candidates",
      candidateTags: indexedMatch.candidates.map((release) => release.tagName),
    };
  }
  const tags = await matchingTags(repository, prefix, token);
  const releases = await githubReleases(repository, tags, token);
  const nearby = uniqueNearbyRelease(releases, request.observedReleasedAt);
  if (nearby.status !== "matched") {
    return {
      ...request,
      repository,
      tagPrefix: prefix,
      status: nearby.status,
      reason:
        nearby.status === "ambiguous"
          ? "multiple-nearby-releases"
          : "no-nearby-release",
      candidateTags: nearby.candidates.map((release) => release.tagName),
    };
  }
  return matched(request, repository, nearby.release, "unique-nearby-release");
}

function matched(request, repository, release, method) {
  const prefix = tagPrefix(request.language, request.package);
  const version = release.tagName.startsWith(prefix)
    ? release.tagName.slice(prefix.length)
    : release.tagName.match(/_v?([^_]+)$/)?.[1] ||
      release.tagName.match(/\/v([^/]+)$/)?.[1] ||
      null;
  return {
    ...request,
    repository,
    status: "matched",
    method,
    tagName: release.tagName,
    version,
    publishedAt: release.publishedAt,
    url: release.url,
    confidence:
      method === "exact-version" || method === "release-index-pr-version"
        ? "corroborated"
        : "inferred",
  };
}

function firstReleasedAt(plan, language) {
  return (
    plan.revisionEvents
      .filter(
        (event) =>
          event.type === "release.status_changed" &&
          event.trackId.startsWith(`sdk:${language.id}:`) &&
          /released/i.test(event.value),
      )
      .map((event) => event.occurredAt)
      .sort()[0] || null
  );
}

function tagPrefix(language, packageName) {
  let value = String(packageName || "").trim();
  if (!value) return null;
  if (language === "Java") {
    value = value.includes(":") ? value.split(":").at(-1) : value.split(/\s+\(/)[0];
  }
  return language === "Go" ? `${value.replace(/\/+$/, "")}/v` : `${value}_`;
}

async function indexedReleases(request, repository, token) {
  const file = RELEASE_INDEX_FILES[request.language];
  if (!file) return [];
  const observed = new Date(request.observedReleasedAt);
  const entries = (
    await Promise.all(
      [-1, 0, 1].map((offset) =>
        releaseIndexEntries(shiftMonth(observed, offset), file, token),
      ),
    )
  ).flat();
  const packageName = String(request.package || "")
    .replace(/-generated$/i, "")
    .toLowerCase();
  let matchingEntries = entries.filter((entry) => {
    const name = entry.name.toLowerCase();
    return (
      name === packageName ||
      name.endsWith(`+${packageName}`) ||
      entry.tagName.toLowerCase().startsWith(`${packageName}_`) ||
      entry.tagName.toLowerCase().includes(`+${packageName}_`)
    );
  });
  const evidenceMatches = matchingEntries.filter(
    (entry) =>
      entry.version &&
      String(request.prVersionEvidence || "").includes(entry.version),
  );
  const indexMatchMethod =
    evidenceMatches.length === 1
      ? "release-index-pr-version"
      : "release-index-version";
  if (evidenceMatches.length === 1)
    matchingEntries = evidenceMatches;
  else
    matchingEntries = matchingEntries.filter((entry) =>
      releaseTypeMatches(request.releaseType, entry.versionType),
    );
  const releases = await githubReleases(
    repository,
    [...new Set(matchingEntries.map((entry) => entry.tagName))],
    token,
  );
  return releases.map((release) => ({ ...release, indexMatchMethod }));
}

async function releaseIndexEntries(month, file, token) {
  const key = `${month}:${file}`;
  if (!releaseIndexCache.has(key)) {
    releaseIndexCache.set(
      key,
      githubRaw(
        `https://api.github.com/repos/Azure/azure-sdk/contents/_data/releases/${month}/${file}.yml?ref=main`,
        token,
      ).then(parseReleaseIndex),
    );
  }
  return releaseIndexCache.get(key);
}

function parseReleaseIndex(source) {
  const entries = [];
  let current = null;
  for (const line of source.split(/\r?\n/)) {
    const start = line.match(/^- Name:\s*(.+)\s*$/);
    if (start) {
      if (current?.name && current.tagName) entries.push(current);
      current = {
        name: yamlScalar(start[1]),
        tagName: null,
        version: null,
        versionType: null,
      };
      continue;
    }
    if (!current) continue;
    const changelog = line.match(/^\s+ChangelogUrl:\s*(.+)\s*$/);
    if (changelog) {
      const url = yamlScalar(changelog[1]);
      const tag = url.match(/\/tree\/(.+?)\/sdk\//)?.[1];
      current.tagName = tag ? decodeURIComponent(tag) : null;
    }
    const version = line.match(/^\s+Version:\s*(.+)\s*$/);
    if (version) current.version = yamlScalar(version[1]);
    const versionType = line.match(/^\s+VersionType:\s*(.+)\s*$/);
    if (versionType) current.versionType = yamlScalar(versionType[1]);
  }
  if (current?.name && current.tagName) entries.push(current);
  return entries;
}

function releaseTypeMatches(planReleaseType, versionType) {
  const planType = String(planReleaseType || "").toLowerCase();
  const indexType = String(versionType || "").toLowerCase();
  if (!planType || !indexType) return true;
  if (planType.includes("preview")) return indexType === "beta";
  if (planType.includes("ga")) return indexType === "ga";
  return true;
}

function yamlScalar(value) {
  const trimmed = String(value).trim();
  if (
    (trimmed.startsWith('"') && trimmed.endsWith('"')) ||
    (trimmed.startsWith("'") && trimmed.endsWith("'"))
  )
    return trimmed.slice(1, -1);
  return trimmed;
}

function shiftMonth(value, offset) {
  const shifted = new Date(
    Date.UTC(value.getUTCFullYear(), value.getUTCMonth() + offset, 1),
  );
  return `${shifted.getUTCFullYear()}-${String(shifted.getUTCMonth() + 1).padStart(2, "0")}`;
}

function uniqueNearbyRelease(releases, observedReleasedAt) {
  const windowMs = Number(args.matchWindowDays) * 86_400_000;
  const observed = new Date(observedReleasedAt);
  const candidates = releases
    .map((release) => ({
      ...release,
      distanceMs: Math.abs(new Date(release.publishedAt) - observed),
    }))
    .filter((release) => release.distanceMs <= windowMs)
    .sort((left, right) => left.distanceMs - right.distanceMs);
  return {
    status:
      candidates.length === 1
        ? "matched"
        : candidates.length
          ? "ambiguous"
          : "missing",
    release: candidates.length === 1 ? candidates[0] : null,
    candidates,
  };
}

async function matchingTags(repository, prefix, token) {
  const values = await github(
    `https://api.github.com/repos/Azure/${repository}/git/matching-refs/tags/${encodeURIComponent(prefix)}`,
    token,
    true,
  );
  return values.map((value) => value.ref.replace(/^refs\/tags\//, ""));
}

async function githubRelease(repository, tagName, token) {
  const releases = await githubReleases(repository, [tagName], token);
  return releases[0] || null;
}

async function githubReleases(repository, tags, token) {
  const values = [];
  for (let index = 0; index < tags.length; index += 40) {
    const batch = tags.slice(index, index + 40);
    const fields = batch
      .map(
        (tag, tagIndex) =>
          `r${tagIndex}: release(tagName: ${JSON.stringify(tag)}) { tagName publishedAt url }`,
      )
      .join("\n");
    const result = await github(
      "https://api.github.com/graphql",
      token,
      false,
      { query: `query { repository(owner: "Azure", name: ${JSON.stringify(repository)}) { ${fields} } }` },
    );
    values.push(
      ...Object.values(result.data?.repository || {}).filter(
        (release) => release?.publishedAt,
      ),
    );
  }
  return values;
}

async function github(url, token, missingIsEmpty = false, body = null) {
  const response = await fetch(url, {
    method: body ? "POST" : "GET",
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: "application/vnd.github+json",
      "Content-Type": "application/json",
      "X-GitHub-Api-Version": "2022-11-28",
    },
    body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(45_000),
  });
  if (response.status === 404 && missingIsEmpty) return [];
  if (!response.ok) {
    const error = new Error(`GitHub ${response.status} for ${url}`);
    error.status = response.status;
    throw error;
  }
  return response.json();
}

async function githubRaw(url, token) {
  const response = await fetch(url, {
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: "application/vnd.github.raw+json",
      "X-GitHub-Api-Version": "2022-11-28",
    },
    signal: AbortSignal.timeout(45_000),
  });
  if (response.status === 404) return "";
  if (!response.ok) throw new Error(`GitHub ${response.status} for ${url}`);
  return response.text();
}
