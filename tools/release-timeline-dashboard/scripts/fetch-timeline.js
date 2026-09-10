#!/usr/bin/env node
/**
 * fetch-timeline.js
 *
 * Fetches raw PR data for an Azure SDK generation timeline using the `gh` CLI.
 * Usage: node scripts/fetch-timeline.js <spec-pr-url> [--sdk-prs <url1> <url2> ...]
 *
 * If SDK PR URLs are not provided, the script will attempt to discover them
 * by searching SDK repos for references to the spec PR.
 */

const { execSync } = require('child_process');

const fs = require('fs');
const path = require('path');

const SDK_REPOS = [
  'Azure/azure-sdk-for-java',
  'Azure/azure-sdk-for-go',
  'Azure/azure-sdk-for-python',
  'Azure/azure-sdk-for-net',
  'Azure/azure-sdk-for-js'
];

const LANG_MAP = {
  'azure-sdk-for-java': 'Java',
  'azure-sdk-for-go': 'Go',
  'azure-sdk-for-python': 'Python',
  'azure-sdk-for-net': '.NET',
  'azure-sdk-for-js': 'JavaScript'
};

const DEVOPS_ORG = 'https://dev.azure.com/azure-sdk';
const DEVOPS_PROJECT = 'internal';

// Pipeline naming conventions per language
const PIPELINE_PATTERNS = {
  'Java':       (svc) => [`java - ${svc}`],
  'Go':         (svc) => [`go - arm${svc}`],
  'Python':     (svc) => [`python - ${svc}`],
  '.NET':       (svc) => [`net - ${svc} - mgmt`, `net - ${svc}`],
  'JavaScript': (svc) => [`js - ${svc} - mgmt`, `js - ${svc}`]
};

// Package name patterns per language for CSV lookup
const PACKAGE_PATTERNS = {
  'Java':       (svc) => `azure-resourcemanager-${svc}`,
  'Go':         (svc) => `sdk/resourcemanager/${svc}/arm${svc}`,
  'Python':     (svc) => `azure-mgmt-${svc}`,
  '.NET':       (svc) => `Azure.ResourceManager.${svc.charAt(0).toUpperCase() + svc.slice(1)}`,
  'JavaScript': (svc) => `@azure/arm-${svc}`
};

// CSV file naming per language
const CSV_LANG_MAP = {
  'Java': 'java',
  'Go': 'go',
  'Python': 'python',
  '.NET': 'dotnet',
  'JavaScript': 'js'
};

function gh(args) {
  try {
    const result = execSync(`gh ${args}`, {
      encoding: 'utf-8',
      maxBuffer: 10 * 1024 * 1024,
      timeout: 30000
    });
    return result.trim();
  } catch (e) {
    console.error(`gh command failed: gh ${args}`);
    console.error(e.stderr || e.message);
    return null;
  }
}

function ghJson(args) {
  const result = gh(args);
  if (!result) return null;
  try {
    return JSON.parse(result);
  } catch {
    return null;
  }
}

function parseGitHubPRUrl(url) {
  const match = url.match(/github\.com\/([^/]+\/[^/]+)\/pull\/(\d+)/);
  if (!match) throw new Error(`Invalid PR URL: ${url}`);
  return { repo: match[1], number: parseInt(match[2]) };
}

function fetchPR(repo, number) {
  console.error(`Fetching PR: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/pulls/${number}`);
}

function fetchComments(repo, number) {
  console.error(`  Fetching comments: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/issues/${number}/comments --paginate`) || [];
}

function fetchReviews(repo, number) {
  console.error(`  Fetching reviews: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/pulls/${number}/reviews --paginate`) || [];
}

function fetchReviewComments(repo, number) {
  console.error(`  Fetching review comments: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/pulls/${number}/comments --paginate`) || [];
}

function fetchCommits(repo, number) {
  console.error(`  Fetching commits: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/pulls/${number}/commits --paginate`) || [];
}

function fetchTimelineEvents(repo, number) {
  console.error(`  Fetching timeline events: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/issues/${number}/timeline --paginate -H "Accept: application/vnd.github+json"`) || [];
}

function fetchIssueEvents(repo, number) {
  console.error(`  Fetching issue events: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/issues/${number}/events --paginate`) || [];
}

/* ── Azure DevOps Release Pipeline Helpers ──────────────── */

function azDevOps(area, resource, routeParams, queryParams) {
  const route = Object.entries(routeParams || {})
    .map(([k, v]) => `${k}=${v}`).join(' ');
  const query = Object.entries(queryParams || {})
    .map(([k, v]) => `"${k.replace(/\$/g, '\\$')}=${v}"`).join(' ');
  const cmd = `az devops invoke --area ${area} --resource ${resource}` +
    ` --organization ${DEVOPS_ORG}` +
    ` --route-parameters project=${DEVOPS_PROJECT} ${route}` +
    (query ? ` --query-parameters ${query}` : '') +
    ` --output json`;
  try {
    const result = execSync(cmd, {
      encoding: 'utf-8',
      maxBuffer: 10 * 1024 * 1024,
      timeout: 30000
    });
    return JSON.parse(result.trim());
  } catch (e) {
    return null;
  }
}

function inferServiceName(prTitle) {
  // Extract service name from AutoPR title patterns
  // e.g. "[AutoPR azure-resourcemanager-durabletask]" → "durabletask"
  const patterns = [
    /\[AutoPR\s+(?:azure-resourcemanager-|azure-mgmt-|arm-)([^\]]+)\]/i,
    /\[AutoPR\s+([^\]]+)\]/i,
    /specification\/([^/]+)\//i
  ];
  for (const pat of patterns) {
    const m = prTitle.match(pat);
    if (m) return m[1].toLowerCase().replace(/[_-]$/, '');
  }
  return null;
}

function findReleasePipeline(language, serviceName) {
  const patternFn = PIPELINE_PATTERNS[language];
  if (!patternFn) return null;
  
  const names = patternFn(serviceName);
  for (const name of names) {
    console.error(`    Searching DevOps pipeline: "${name}"`);
    const result = azDevOps('build', 'definitions', {}, { name });
    const defs = result?.value || [];
    if (defs.length > 0) {
      return { definitionId: defs[0].id, name: defs[0].name, url: defs[0]._links?.web?.href };
    }
  }
  return null;
}

function findReleaseBuilds(definitionId, afterDate) {
  console.error(`    Fetching builds for definition ${definitionId}...`);
  // Use reasonFilter=manual to only get release builds, and minFinishTime to narrow window
  const params = {
    definitions: String(definitionId),
    $top: '20',
    queryOrder: 'finishTimeDescending',
    reasonFilter: 'manual'
  };
  if (afterDate) {
    // Look for builds finishing after the PR merge date
    params.minFinishTime = afterDate;
  }
  const result = azDevOps('build', 'builds', {}, params);
  
  const builds = (result?.value || []).filter(b => {
    if (afterDate && b.finishTime && b.finishTime < afterDate) return false;
    return true;
  });
  
  return builds;
}

function getReleaseStage(buildId) {
  console.error(`    Fetching timeline for build ${buildId}...`);
  const result = azDevOps('build', 'timeline', { buildId: String(buildId) }, {});
  if (!result?.records) return null;
  
  const stage = result.records.find(r =>
    r.type === 'Stage' && /releas/i.test(r.name)
  );
  
  if (!stage) return null;
  return {
    name: stage.name,
    state: stage.state,
    result: stage.result,
    startTime: stage.startTime,
    finishTime: stage.finishTime
  };
}

function fetchReleaseFromDevOps(language, serviceName, mergedAt) {
  if (!serviceName || !language) return null;
  
  console.error(`  Looking up DevOps release: ${language} / ${serviceName}`);
  const pipeline = findReleasePipeline(language, serviceName);
  if (!pipeline) {
    console.error(`    No pipeline found`);
    return null;
  }
  
  console.error(`    Found pipeline: ${pipeline.name} (${pipeline.definitionId})`);
  const builds = findReleaseBuilds(pipeline.definitionId, mergedAt);
  if (builds.length === 0) {
    console.error(`    No release builds found after ${mergedAt}`);
    return { pipeline, status: 'pending', stage: null };
  }
  
  // Check each build for a release stage
  for (const build of builds.slice(0, 5)) {
    const stage = getReleaseStage(build.id);
    if (!stage) continue;
    
    if (stage.result === 'succeeded' && stage.finishTime) {
      console.error(`    Released via build ${build.id} at ${stage.finishTime}`);
      return { pipeline, status: 'released', stage, buildId: build.id, buildUrl: build._links?.web?.href };
    }
    if (stage.result === 'failed') {
      console.error(`    Release FAILED in build ${build.id}`);
      return { pipeline, status: 'failed', stage, buildId: build.id, buildUrl: build._links?.web?.href };
    }
    if (stage.result === 'skipped') {
      console.error(`    Release SKIPPED in build ${build.id}`);
      // Keep searching — a later build might have released
    }
  }
  
  console.error(`    No successful release found — pending`);
  return { pipeline, status: 'pending', stage: null };
}

/* ── CSV Release Date Lookup ────────────────────────────── */

function lookupReleaseCSV(language, serviceName, releaseCsvDir) {
  if (!releaseCsvDir || !serviceName || !language) return null;
  
  const csvLang = CSV_LANG_MAP[language];
  if (!csvLang) return null;
  
  const csvPath = path.join(releaseCsvDir, 'latest', `${csvLang}-packages.csv`);
  if (!fs.existsSync(csvPath)) {
    console.error(`    CSV not found: ${csvPath}`);
    return null;
  }
  
  const patternFn = PACKAGE_PATTERNS[language];
  if (!patternFn) return null;
  const expectedPkg = patternFn(serviceName);
  
  console.error(`  Looking up CSV: ${expectedPkg} in ${csvPath}`);
  const content = fs.readFileSync(csvPath, 'utf-8');
  const lines = content.split('\n');
  const header = lines[0].split(',').map(h => h.trim());
  
  const pkgIdx = header.indexOf('Package');
  const gaVersionIdx = header.indexOf('VersionGA');
  const gaDateIdx = header.indexOf('LatestGADate');
  
  if (pkgIdx === -1) return null;
  
  for (const line of lines.slice(1)) {
    if (!line.trim()) continue;
    // Simple CSV parse (handles quoted fields)
    const cols = parseCSVLine(line);
    const pkg = cols[pkgIdx]?.trim();
    
    if (pkg === expectedPkg) {
      const version = gaVersionIdx >= 0 ? cols[gaVersionIdx]?.trim() : null;
      const dateStr = gaDateIdx >= 0 ? cols[gaDateIdx]?.trim() : null;
      
      let gaDate = null;
      if (dateStr) {
        // Parse MM/DD/YYYY — use end-of-day since CSV has date-only precision
        const [m, d, y] = dateStr.split('/').map(Number);
        if (y && m && d) {
          gaDate = `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}T23:59:59Z`;
        }
      }
      
      console.error(`    CSV match: ${pkg} v${version} released ${gaDate || 'unknown'}`);
      return { packageName: pkg, packageVersion: version, gaDate };
    }
  }
  
  console.error(`    No CSV match for ${expectedPkg}`);
  return null;
}

function parseCSVLine(line) {
  const result = [];
  let current = '';
  let inQuotes = false;
  for (const ch of line) {
    if (ch === '"') { inQuotes = !inQuotes; }
    else if (ch === ',' && !inQuotes) { result.push(current); current = ''; }
    else { current += ch; }
  }
  result.push(current);
  return result;
}

function fetchReleaseData(language, prTitle, mergedAt, releaseCsvDir) {
  const serviceName = inferServiceName(prTitle);
  if (!serviceName) {
    console.error(`  Could not infer service name from: ${prTitle}`);
    return null;
  }
  
  console.error(`  Service name: ${serviceName}`);
  
  const devOps = fetchReleaseFromDevOps(language, serviceName, mergedAt);
  const csv = lookupReleaseCSV(language, serviceName, releaseCsvDir);
  
  return {
    serviceName,
    language,
    devOps,
    csv,
    packageName: csv?.packageName || (PACKAGE_PATTERNS[language] ? PACKAGE_PATTERNS[language](serviceName) : null)
  };
}

function discoverSDKPRs(specRepo, specNumber) {
  console.error(`Discovering SDK PRs linked to ${specRepo}#${specNumber}...`);
  const results = [];
  const seen = new Set();

  // Strategy 1: Search for spec PR URL in SDK PR bodies
  for (const repo of SDK_REPOS) {
    console.error(`  Searching ${repo} for spec PR reference...`);
    const searchResult = ghJson(
      `api "search/issues?q=repo:${repo}+${specRepo}/pull/${specNumber}+is:pr&per_page=5"`
    );

    if (searchResult?.items?.length) {
      for (const item of searchResult.items) {
        const key = `${repo}#${item.number}`;
        if (!seen.has(key)) {
          seen.add(key);
          results.push({ repo, number: item.number, url: item.html_url });
        }
      }
    }
  }

  // Strategy 2: If few results, also search by merge commit SHA
  // (newer AutoPR PRs include the commit SHA but not the spec PR URL)
  if (results.length < SDK_REPOS.length) {
    const specPR = ghJson(`api repos/${specRepo}/pulls/${specNumber}`);
    const mergeCommitSha = specPR?.merge_commit_sha;
    if (mergeCommitSha) {
      const shortSha = mergeCommitSha.substring(0, 12);
      for (const repo of SDK_REPOS) {
        console.error(`  Searching ${repo} for commit SHA ${shortSha}...`);
        const searchResult = ghJson(
          `api "search/issues?q=repo:${repo}+${shortSha}+is:pr&per_page=5"`
        );
        if (searchResult?.items?.length) {
          for (const item of searchResult.items) {
            const key = `${repo}#${item.number}`;
            if (!seen.has(key)) {
              seen.add(key);
              results.push({ repo, number: item.number, url: item.html_url });
              console.error(`    Found via commit SHA: ${repo}#${item.number}`);
            }
          }
        }
      }
    }
  }

  // Strategy 3: Search by service name extracted from spec PR files
  // Works even when strategies 1/2 fail (e.g., batched SDK generation uses
  // a different commit SHA than the spec PR merge commit)
  if (results.length < SDK_REPOS.length) {
    const files = ghJson(`api "repos/${specRepo}/pulls/${specNumber}/files?per_page=100"`);
    if (files?.length) {
      // Extract service name from any specification/ path, not just tspconfig.yaml.
      // Handles cases where the spec PR only changes .tsp models or examples.
      let serviceName = null;
      const specFile = files.find(f => f.filename?.startsWith('specification/'));
      if (specFile) {
        const parts = specFile.filename.split('/');
        serviceName = parts[1]; // e.g. "confluent", "computeschedule"
      }
      if (serviceName) {
        for (const repo of SDK_REPOS) {
          const repoShort = repo.split('/')[1];
          const lang = LANG_MAP[repoShort];
          if (!lang) continue;
          // Check if we already found a PR for this repo
          if (results.some(r => r.repo === repo)) continue;

          console.error(`  Searching ${repo} for service name "${serviceName}"...`);
          const searchResult = ghJson(
            `api "search/issues?q=repo:${repo}+${serviceName}+is:pr+author:azure-sdk&per_page=5&sort=created&order=desc"`
          );
          if (searchResult?.items?.length) {
            for (const item of searchResult.items) {
              const key = `${repo}#${item.number}`;
              if (!seen.has(key)) {
                seen.add(key);
                results.push({ repo, number: item.number, url: item.html_url });
                console.error(`    Found via service name: ${repo}#${item.number}`);
              }
            }
          }
        }
      }
    }
  }

  return results;
}

function detectGenerationFlow(pr) {
  const title = pr.title || '';
  const author = pr.user?.login || '';
  if (title.startsWith('[AutoPR ') || author === 'azure-sdk') {
    return 'automated';
  }
  return 'manual';
}

function fetchFullPRData(repo, number) {
  const pr = fetchPR(repo, number);
  if (!pr) return null;

  const comments = fetchComments(repo, number);
  const reviews = fetchReviews(repo, number);
  const reviewComments = fetchReviewComments(repo, number);
  const commits = fetchCommits(repo, number);
  const issueEvents = fetchIssueEvents(repo, number);

  // Extract draft lifecycle events
  const draftEvents = (issueEvents || []).filter(e =>
    e.event === 'ready_for_review' || e.event === 'convert_to_draft'
  );
  const readyForReviewEvent = draftEvents.find(e => e.event === 'ready_for_review');
  const readyForReviewAt = readyForReviewEvent ? readyForReviewEvent.created_at : null;

  const repoShort = repo.split('/')[1];
  const language = LANG_MAP[repoShort] || null;

  return {
    repo,
    language,
    number,
    url: pr.html_url,
    title: pr.title,
    author: pr.user?.login,
    createdAt: pr.created_at,
    mergedAt: pr.merged_at,
    closedAt: pr.closed_at,
    mergedBy: pr.merged_by?.login,
    state: pr.merged ? 'merged' : pr.state,
    isDraft: pr.draft || false,
    readyForReviewAt,
    generationFlow: detectGenerationFlow(pr),
    labels: (pr.labels || []).map(l => l.name),
    reviewers: [
      ...(pr.requested_reviewers || []).map(r => r.login),
      ...reviews.filter(r => r.state === 'APPROVED').map(r => r.user?.login)
    ].filter((v, i, a) => a.indexOf(v) === i),
    _raw: {
      pr,
      comments,
      reviews,
      reviewComments,
      commits,
      issueEvents: draftEvents
    }
  };
}

function main() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    console.error('Usage: node fetch-timeline.js <spec-pr-url> [--sdk-prs <url1> <url2> ...] [--release-csv <dir>] [--skip-releases]');
    process.exit(1);
  }

  const specUrl = args[0];
  const { repo: specRepo, number: specNumber } = parseGitHubPRUrl(specUrl);

  // Parse SDK PR URLs if provided
  let sdkPRUrls = [];
  const sdkIdx = args.indexOf('--sdk-prs');
  if (sdkIdx !== -1) {
    const nextFlagIdx = args.findIndex((a, i) => i > sdkIdx && a.startsWith('--'));
    sdkPRUrls = args.slice(sdkIdx + 1, nextFlagIdx === -1 ? undefined : nextFlagIdx);
  }

  // Parse release CSV dir
  const csvIdx = args.indexOf('--release-csv');
  const releaseCsvDir = csvIdx !== -1 ? args[csvIdx + 1] : null;

  // Check skip-releases flag
  const skipReleases = args.includes('--skip-releases');

  // Fetch spec PR data
  const specData = fetchFullPRData(specRepo, specNumber);
  if (!specData) {
    console.error('Failed to fetch spec PR');
    process.exit(1);
  }

  // Discover or use provided SDK PRs
  let sdkPRs = [];
  if (sdkPRUrls.length > 0) {
    for (const url of sdkPRUrls) {
      const { repo, number } = parseGitHubPRUrl(url);
      const data = fetchFullPRData(repo, number);
      if (data) sdkPRs.push(data);
    }
  } else {
    const discovered = discoverSDKPRs(specRepo, specNumber);
    for (const { repo, number } of discovered) {
      const data = fetchFullPRData(repo, number);
      if (data) sdkPRs.push(data);
    }
  }

  // Fetch release data for merged SDK PRs
  if (!skipReleases) {
    for (const sdk of sdkPRs) {
      if (!sdk.mergedAt) continue;
      console.error(`\nLooking up release for ${sdk.language} (${sdk.repo}#${sdk.number})...`);
      const release = fetchReleaseData(sdk.language, sdk.title, sdk.mergedAt, releaseCsvDir);
      if (release) {
        sdk._release = release;
      }
    }
  }

  // Output the raw data for agent processing
  const output = {
    _meta: {
      fetchedAt: new Date().toISOString(),
      specUrl,
      releaseCsvDir: releaseCsvDir || null,
      note: 'This is raw fetched data. It needs agent processing to produce the final timeline JSON.'
    },
    specPR: specData,
    sdkPRs
  };

  console.log(JSON.stringify(output, null, 2));
}

main();
