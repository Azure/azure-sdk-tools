#!/usr/bin/env node
/**
 * fetch-service-timeline.js
 *
 * Fetches raw PR data for a full service timeline (multiple spec PRs + all SDK PRs)
 * using the `gh` CLI and TypeSpec metadata for discovery.
 *
 * Usage:
 *   node scripts/fetch-service-timeline.js <tsp-project-path> [options]
 *
 * Options:
 *   --lookback <days>           Lookback period in days (default: 365)
 *   --tool-calls-csv <path>     Path to query3-tool-calls.csv for tool call telemetry
 *   --skip-releases             Skip Azure DevOps release pipeline lookups
 *   --release-csv <dir>         Path to azure-sdk/_data/releases dir for CSV lookup
 *
 * Output: JSON to stdout (pipe to file)
 */

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const { resolve: resolveMetadata } = require('./lib/typespec-metadata');
const { fetchReleaseForPR, inferServiceName: inferSvcName } = require('./lib/release-pipeline');

const SPEC_REPO = 'Azure/azure-rest-api-specs';

const LANG_MAP = {
  'azure-sdk-for-java': 'Java',
  'azure-sdk-for-go': 'Go',
  'azure-sdk-for-python': 'Python',
  'azure-sdk-for-net': '.NET',
  'azure-sdk-for-js': 'JavaScript'
};

/* ── GitHub CLI helpers ───────────────────────────────────── */

function gh(args) {
  try {
    return execSync(`gh ${args}`, {
      encoding: 'utf-8',
      maxBuffer: 10 * 1024 * 1024,
      timeout: 60000
    }).trim();
  } catch (e) {
    const msg = (e.stderr || e.message || '').slice(0, 120);
    console.error(`  ⚠ gh failed: gh ${args.slice(0, 80)}... ${msg}`);
    return null;
  }
}

function ghJson(args) {
  const result = gh(args);
  if (!result) return null;
  try {
    return JSON.parse(result);
  } catch { return null; }
}

function ghJsonPaginate(args) {
  const result = gh(`${args} --paginate`);
  if (!result) return [];
  try {
    // --paginate concatenates JSON arrays like [a,b][c,d]; merge into one flat array
    const merged = JSON.parse(result.replace(/\]\s*\[/g, ','));
    return Array.isArray(merged) ? merged : [merged];
  } catch {
    try { return JSON.parse(result); } catch { return []; }
  }
}

/* ── PR fetch (reused from fetch-timeline.js) ─────────────── */

function fetchPR(repo, number) {
  console.error(`  Fetching PR: ${repo}#${number}`);
  return ghJson(`api repos/${repo}/pulls/${number}`);
}

function fetchComments(repo, number) {
  return ghJsonPaginate(`api repos/${repo}/issues/${number}/comments`) || [];
}

function fetchReviews(repo, number) {
  return ghJsonPaginate(`api repos/${repo}/pulls/${number}/reviews`) || [];
}

function fetchReviewComments(repo, number) {
  return ghJsonPaginate(`api repos/${repo}/pulls/${number}/comments`) || [];
}

function fetchCommits(repo, number) {
  return ghJsonPaginate(`api repos/${repo}/pulls/${number}/commits`) || [];
}

function fetchIssueEvents(repo, number) {
  return ghJsonPaginate(`api repos/${repo}/issues/${number}/events`) || [];
}

function detectGenerationFlow(pr) {
  const title = pr.title || '';
  const author = pr.user?.login || '';
  if (title.startsWith('[AutoPR ') || author === 'azure-sdk') return 'automated';
  return 'manual';
}

/**
 * Check if an SDK PR targets a different sub-package than our target.
 * Returns true if the PR should be EXCLUDED (it's for a sibling package).
 *
 * Generalized approach — no hardcoded package names:
 * 1. AutoPR title: [AutoPR <package-name>] — compare against known target names
 * 2. Changed files: if files exist, check whether any touch the target packageDir
 */
function isSiblingPackagePR(pr, targetPackageDir, targetPackageName, files) {
  const title = (pr.title || '');
  const targetDirLower = (targetPackageDir || '').toLowerCase();
  const targetDirBase = targetDirLower.split('/').pop();
  const targetNameLower = (targetPackageName || '').toLowerCase();

  // Build set of acceptable name variants from the target
  // e.g. "Azure.ResourceManager.ContainerRegistry" → "azure.resourcemanager.containerregistry"
  // e.g. "sdk/containerregistry/arm-containerregistry" → "arm-containerregistry"
  const targetVariants = new Set();
  if (targetDirBase) targetVariants.add(targetDirBase);
  if (targetNameLower) {
    targetVariants.add(targetNameLower);
    // Strip common prefixes: @azure/, azure-, com.azure.resourcemanager:
    const stripped = targetNameLower
      .replace(/^@azure[\/-]/, '')
      .replace(/^azure[\.-]mgmt[\.-]/, '')
      .replace(/^azure[\.-]/, '')
      .replace(/^com\.azure\.resourcemanager:/, '')
      .replace(/^sdk\/resourcemanager\/[^/]+\//, '');
    if (stripped) targetVariants.add(stripped);
  }

  // Signal 1: AutoPR title with explicit package name
  const autoprMatch = title.match(/^\[AutoPR\s+([^\]]+)\]/i);
  if (autoprMatch) {
    const autoprPkg = autoprMatch[1].toLowerCase().trim();

    // Check if autoprPkg matches any of our target variants
    let matchesTarget = false;
    for (const variant of targetVariants) {
      if (autoprPkg === variant || autoprPkg.endsWith('/' + variant)) {
        matchesTarget = true;
        break;
      }
    }

    if (!matchesTarget) {
      console.error(`    ⏭ Skipping sibling-package PR (AutoPR: "${autoprPkg}" ≠ target "${targetDirBase}")`);
      return true;
    }
  }

  // Signal 2: Changed files — if present, check whether any touch the target packageDir
  if (files && files.length > 0 && targetDirLower) {
    const touchesTarget = files.some(f => {
      const fn = (f.filename || '').toLowerCase();
      return fn.startsWith(targetDirLower + '/') || fn === targetDirLower;
    });
    if (!touchesTarget) {
      // Files exist but none are in our target packageDir — likely a sibling
      console.error(`    ⏭ Skipping sibling-package PR (no files in ${targetDirBase})`);
      return true;
    }
  }

  return false;
}

function fetchPRFiles(repo, number) {
  // Fetch first page of changed files (100 max)
  return ghJson(`api "repos/${repo}/pulls/${number}/files?per_page=100"`) || [];
}

function countDistinctServiceDirs(repo, files) {
  const dirs = new Set();
  const repoShort = repo.split('/')[1];
  for (const f of files) {
    const p = f.filename || '';
    if (repoShort === 'azure-rest-api-specs') {
      const m = p.match(/^specification\/([^/]+)/);
      if (m) dirs.add(m[1]);
    } else {
      const m = p.match(/^sdk\/([^/]+)/);
      if (m) dirs.add(m[1]);
    }
  }
  return dirs.size;
}

// Mass-change heuristic — broad refactors, bulk regenerations, cross-service PRs.
// Checked early in the fetch to skip expensive API calls (reviews, comments, etc.).
const MASS_DIR_THRESHOLD = 5;
const MASS_FILES_THRESHOLD = 200;
const MASS_FILES_DIR_THRESHOLD = 3;
const MASS_LARGE_FILES_THRESHOLD = 500;
const MASS_LARGE_FILES_DIR_THRESHOLD = 2;
const MASS_TITLE_PATTERNS = [/^\[automation\] regenerate sdk\b/i, /^\*{3}NO_CI\*{3}/i];

// Noise patterns — release automation PRs that should be skipped entirely at fetch time
const NOISE_TITLE_PATTERNS = [
  /^\[AutoRelease\]/i,
  /^increment\s+versions?\s+for\s+/i,
  /^update\s+typespec\s+emitter\s+version\s+/i,
];

function isMassChange(pr, serviceDirCount, isSpec) {
  const dirs = serviceDirCount || 0;
  const files = pr.changed_files || 0;
  const title = pr.title || '';
  if (dirs >= MASS_DIR_THRESHOLD) return true;
  if (files >= MASS_FILES_THRESHOLD && dirs >= MASS_FILES_DIR_THRESHOLD) return true;
  if (files >= MASS_LARGE_FILES_THRESHOLD && dirs >= MASS_LARGE_FILES_DIR_THRESHOLD) return true;
  if (MASS_TITLE_PATTERNS.some(re => re.test(title))) return true;
  if (!isSpec && dirs === 0 && files > 0) return true;
  if (files === 0 && dirs >= 2) return true;
  return false;
}

function fetchFullPRData(repo, number, isSpec) {
  const pr = fetchPR(repo, number);
  if (!pr) return null;

  // Skip noise/automation PRs before any expensive API calls
  if (!isSpec && NOISE_TITLE_PATTERNS.some(re => re.test(pr.title || ''))) {
    console.error(`    ⏭ Skipping noise/automation PR: ${(pr.title || '').substring(0, 60)}`);
    return null;
  }

  // Early mass-change detection — skip expensive calls if PR is a broad refactor
  const files = fetchPRFiles(repo, number);
  const serviceDirCount = countDistinctServiceDirs(repo, files);
  if (isMassChange(pr, serviceDirCount, isSpec)) {
    console.error(`    ⏭ Skipping mass-change PR (${serviceDirCount} dirs, ${pr.changed_files || 0} files)`);
    return null;
  }

  const comments = fetchComments(repo, number);
  const reviews = fetchReviews(repo, number);
  const reviewComments = fetchReviewComments(repo, number);
  const commits = fetchCommits(repo, number);
  const issueEvents = fetchIssueEvents(repo, number);

  const draftEvents = (issueEvents || []).filter(e =>
    e.event === 'ready_for_review' || e.event === 'convert_to_draft'
  );
  const readyForReviewEvent = draftEvents.find(e => e.event === 'ready_for_review');
  const readyForReviewAt = readyForReviewEvent ? readyForReviewEvent.created_at : null;

  const repoShort = repo.split('/')[1];
  const language = LANG_MAP[repoShort] || null;

  const result = {
    repo, language, number,
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
    _raw: { pr, comments, reviews, reviewComments, commits, issueEvents: draftEvents }
  };

  // Store changed file paths (used for API version detection and sub-package filtering)
  result._changedFiles = files.map(f => f.filename).filter(Boolean);

  return result;
}

/* ── Spec PR Discovery ────────────────────────────────────── */

function discoverSpecPRs(specPath, lookbackDate) {
  console.error(`\nDiscovering spec PRs for ${specPath} since ${lookbackDate}...`);
  const seen = new Set();
  const results = [];

  // Strategy: find commits touching the spec path, then map to PRs
  const commits = ghJsonPaginate(
    `api "repos/${SPEC_REPO}/commits?path=${encodeURIComponent(specPath)}&per_page=100&since=${lookbackDate}"`
  );
  console.error(`  Found ${commits.length} commits touching ${specPath}`);

  for (const commit of commits) {
    if (!commit.sha) continue;
    const prs = ghJson(`api "repos/${SPEC_REPO}/commits/${commit.sha}/pulls"`) || [];
    for (const pr of prs) {
      if (seen.has(pr.number)) continue;
      // Only include PRs whose creation falls within the lookback window
      if (pr.created_at && pr.created_at < lookbackDate) {
        console.error(`  Skipping PR #${pr.number} (created ${pr.created_at.slice(0, 10)}, before lookback)`);
        continue;
      }
      seen.add(pr.number);
      results.push({ repo: SPEC_REPO, number: pr.number, url: pr.html_url, createdAt: pr.created_at });
      console.error(`  Found spec PR #${pr.number}: ${(pr.title || '').slice(0, 60)}`);
    }
  }

  // Sort by creation date
  results.sort((a, b) => (a.createdAt || '').localeCompare(b.createdAt || ''));
  console.error(`  Total unique spec PRs: ${results.length}`);
  return results;
}

/* ── SDK PR Discovery ─────────────────────────────────────── */

/**
 * Primary strategy: discover SDK PRs outward from spec PRs.
 * For each spec PR, search SDK repos for references to the spec PR number,
 * merge commit SHA, or spec PR URL.
 */
function discoverSDKPRsFromSpecPRs(specPRs, metadata) {
  console.error(`\nDiscovering SDK PRs from ${specPRs.length} spec PRs...`);
  const results = {}; // language -> [{ repo, number, url }]

  for (const [lang, pkg] of Object.entries(metadata.packages)) {
    results[lang] = [];
  }

  const seenPerLang = {};
  for (const lang of Object.keys(metadata.packages)) {
    seenPerLang[lang] = new Set();
  }

  for (const specPR of specPRs) {
    const specNum = specPR.number;
    const specMergeCommit = specPR._raw?.pr?.merge_commit_sha || null;

    for (const [lang, pkg] of Object.entries(metadata.packages)) {
      const repo = pkg.repo;
      const seen = seenPerLang[lang];

      // Search by spec repo PR reference (more specific than bare number to avoid
      // matching SDK PRs that happen to have the same number)
      const searchResult = ghJson(
        `api "search/issues?q=repo:${repo}+%22azure-rest-api-specs%2Fpull%2F${specNum}%22+is:pr&per_page=30&sort=created&order=desc"`
      );
      if (searchResult?.items?.length) {
        for (const item of searchResult.items) {
          if (seen.has(item.number)) continue;
          seen.add(item.number);
          results[lang].push({ repo, number: item.number, url: item.html_url });
          console.error(`    [${lang}] Found via spec PR #${specNum}: ${repo}#${item.number}`);
        }
      }

      // Search by merge commit SHA (for newer AutoPR bots)
      if (specMergeCommit) {
        const shaSearch = ghJson(
          `api "search/issues?q=repo:${repo}+${specMergeCommit.slice(0, 12)}+is:pr&per_page=10"`
        );
        if (shaSearch?.items?.length) {
          for (const item of shaSearch.items) {
            if (seen.has(item.number)) continue;
            seen.add(item.number);
            results[lang].push({ repo, number: item.number, url: item.html_url });
            console.error(`    [${lang}] Found via commit SHA: ${repo}#${item.number}`);
          }
        }
      }
    }
  }

  return { results, seenPerLang };
}

/**
 * Fallback strategy: discover SDK PRs by commits touching the package directory.
 * Only finds PRs not already discovered via spec-PR-outward strategy.
 */
function discoverSDKPRsByPath(metadata, lookbackDate, seenPerLang) {
  console.error(`\nDiscovering additional SDK PRs by path...`);
  const results = {}; // language -> [{ repo, number, url }]

  for (const [lang, pkg] of Object.entries(metadata.packages)) {
    const additionalPRs = [];
    const repo = pkg.repo;
    const packageDir = pkg.packageDir;
    const seen = seenPerLang[lang] || new Set();

    const commits = ghJsonPaginate(
      `api "repos/${repo}/commits?path=${encodeURIComponent(packageDir)}&per_page=100&since=${lookbackDate}"`
    );
    console.error(`  [${lang}] Commits-by-path found ${commits.length} commits in ${packageDir}`);

    for (const commit of commits) {
      if (!commit.sha) continue;
      const prs = ghJson(`api "repos/${repo}/commits/${commit.sha}/pulls"`) || [];
      for (const pr of prs) {
        if (seen.has(pr.number)) continue;
        if (pr.created_at && pr.created_at < lookbackDate) continue;
        seen.add(pr.number);
        additionalPRs.push({ repo, number: pr.number, url: pr.html_url });
        console.error(`    [${lang}] Found via path: ${repo}#${pr.number}`);
      }
    }

    results[lang] = additionalPRs;
  }

  return results;
}

/* ── Tool Call Telemetry ──────────────────────────────────── */

function parseToolCallsCSV(csvPath, packageNames) {
  if (!csvPath || !fs.existsSync(csvPath)) return [];
  console.error(`\nParsing tool calls CSV: ${csvPath}`);

  const content = fs.readFileSync(csvPath, 'utf-8');
  const lines = content.split('\n');
  if (lines.length < 2) return [];

  // Parse header (handle BOM)
  const header = lines[0].replace(/^\uFEFF/, '').replace(/^"/, '').split('","').map(h => h.replace(/"$/, ''));
  const tsIdx = header.indexOf('timestamp');
  const evtIdx = header.indexOf('event_type');
  const pkgIdx = header.indexOf('package_name');
  const toolIdx = header.indexOf('tool_name');
  const successIdx = header.indexOf('success');
  const durIdx = header.indexOf('duration');
  const dimsIdx = header.indexOf('custom_dims');

  if (tsIdx === -1 || pkgIdx === -1) {
    console.error('  CSV missing required columns');
    return [];
  }

  // Normalize package names for matching
  const normalizedNames = new Set(
    packageNames.map(n => n.toLowerCase().replace(/^@/, ''))
  );

  const results = [];
  for (let i = 1; i < lines.length; i++) {
    const line = lines[i].trim();
    if (!line) continue;

    // Simple CSV parse handling quoted fields
    const cols = parseCSVLine(line);
    const pkg = (cols[pkgIdx] || '').trim();
    const pkgLower = pkg.toLowerCase().replace(/^@/, '');

    if (!normalizedNames.has(pkgLower)) continue;

    // Parse custom dimensions for extra metadata
    let clientType = 'human';
    let clientName = '';
    let language = '';
    let operationStatus = '';
    try {
      const dims = JSON.parse(cols[dimsIdx] || '{}');
      clientName = dims.clientname || dims.clientName || '';
      language = dims.language || '';
      operationStatus = dims.operation_status || '';
      if (/copilot|agent/i.test(clientName)) clientType = 'agent';
    } catch { /* ignore parse errors */ }

    results.push({
      timestamp: cols[tsIdx],
      eventType: (cols[evtIdx] || '').trim(),
      packageName: pkg,
      toolName: (cols[toolIdx] || '').trim(),
      success: (cols[successIdx] || '').trim().toLowerCase() === 'true',
      durationMs: parseInt((cols[durIdx] || '0').replace(/,/g, ''), 10) || 0,
      clientType,
      clientName,
      language,
      operationStatus
    });
  }

  console.error(`  Found ${results.length} tool calls matching service packages`);
  return results;
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

/* ── Main ─────────────────────────────────────────────────── */

function main() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    console.error('Usage: node fetch-service-timeline.js <tsp-project-path> [--lookback <days>] [--tool-calls-csv <path>] [--skip-releases]');
    process.exit(1);
  }

  const projectPath = args[0];

  // Parse options
  const lookbackIdx = args.indexOf('--lookback');
  const lookbackDays = lookbackIdx !== -1 ? parseInt(args[lookbackIdx + 1], 10) : 365;

  const toolCsvIdx = args.indexOf('--tool-calls-csv');
  const toolCallsCsvPath = toolCsvIdx !== -1 ? args[toolCsvIdx + 1] : null;

  const skipReleases = args.includes('--skip-releases');

  // Calculate lookback date
  const now = new Date();
  const lookbackDate = new Date(now.getTime() - lookbackDays * 86400000).toISOString();
  console.error(`\nLookback: ${lookbackDays} days (since ${lookbackDate.slice(0, 10)})`);

  // Step 1: Resolve TypeSpec metadata
  const metadata = resolveMetadata(projectPath);
  if (Object.keys(metadata.packages).length === 0) {
    console.error('ERROR: No packages found in TypeSpec metadata');
    process.exit(1);
  }

  // Step 2: Discover spec PRs
  const specPRRefs = discoverSpecPRs(metadata.specPath, lookbackDate);
  console.error(`\n=== Fetching ${specPRRefs.length} spec PRs ===`);

  const specPRs = [];
  for (const ref of specPRRefs) {
    const data = fetchFullPRData(ref.repo, ref.number, true);
    if (data) specPRs.push(data);
  }

  // Step 3: Discover SDK PRs — spec-PR-outward first, then path-based fallback
  const { results: specLinkedSDKPRs, seenPerLang } = discoverSDKPRsFromSpecPRs(specPRs, metadata);
  const pathSDKPRs = discoverSDKPRsByPath(metadata, lookbackDate, seenPerLang);

  // Merge SDK PR refs
  const sdkPRRefs = {};
  for (const [lang] of Object.entries(metadata.packages)) {
    sdkPRRefs[lang] = [...(specLinkedSDKPRs[lang] || []), ...(pathSDKPRs[lang] || [])];
    console.error(`  ${lang}: ${sdkPRRefs[lang].length} total SDK PRs`);
  }

  console.error(`\n=== Fetching SDK PRs ===`);
  const sdkPRs = {};
  for (const [lang, refs] of Object.entries(sdkPRRefs)) {
    sdkPRs[lang] = [];
    const pkg = metadata.packages[lang];
    const targetDir = pkg?.packageDir || '';
    const targetName = pkg?.name || '';
    console.error(`\n  Fetching ${refs.length} ${lang} PRs...`);
    for (const ref of refs) {
      const data = fetchFullPRData(ref.repo, ref.number, false);
      if (!data) continue;
      // Sub-package filter: skip PRs targeting a sibling package
      // Use files already fetched by fetchFullPRData (stored in _changedFiles)
      const filesObj = (data._changedFiles || []).map(fn => ({ filename: fn }));
      if (isSiblingPackagePR(data._raw.pr, targetDir, targetName, filesObj)) continue;
      sdkPRs[lang].push(data);
    }
  }

  // Step 4: Parse tool call telemetry
  const packageNames = Object.values(metadata.packages).map(p => p.name);
  const toolCalls = parseToolCallsCSV(toolCallsCsvPath, packageNames);

  // Step 5: Fetch release pipeline data for merged SDK PRs
  if (!skipReleases) {
    console.error('\n=== Fetching release pipeline data ===');
    // Derive service name from TypeSpec metadata (e.g. "sdk/durabletask" → "durabletask")
    const serviceDirs = {};
    for (const [lang, pkg] of Object.entries(metadata.packages)) {
      const dir = pkg.serviceDir || '';
      // serviceDir is like "sdk/durabletask" — extract the last segment
      const segments = dir.replace(/^sdk\//, '').split('/').filter(Boolean);
      serviceDirs[lang] = segments[segments.length - 1] || null;
    }

    for (const [lang, prs] of Object.entries(sdkPRs)) {
      const pkgInfo = metadata.packages[lang];
      const serviceName = serviceDirs[lang] || inferSvcName(prs[0]?.title || '');
      const packageName = pkgInfo?.name || null;

      if (!serviceName) {
        console.error(`  ⚠ No service name for ${lang}, skipping release lookup`);
        continue;
      }

      console.error(`\n  ${lang}: service="${serviceName}", package="${packageName}"`);
      for (const pr of prs) {
        if (!pr.mergedAt) continue;
        const release = fetchReleaseForPR({ language: lang, serviceName, packageName, mergedAt: pr.mergedAt });
        if (release) {
          pr._release = release;
        }
      }
    }
  }

  // Step 6: Output raw data
  const output = {
    _meta: {
      fetchedAt: now.toISOString(),
      projectPath,
      lookbackDays,
      lookbackDate,
      skipReleases,
      note: 'Raw service timeline data. Process with process-service-timeline.js.'
    },
    metadata,
    specPRs,
    sdkPRs,
    toolCalls
  };

  console.log(JSON.stringify(output, null, 2));
  console.error(`\n✅ Done — ${specPRs.length} spec PRs, ${Object.values(sdkPRs).reduce((s, a) => s + a.length, 0)} SDK PRs, ${toolCalls.length} tool calls`);
}

main();
