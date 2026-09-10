#!/usr/bin/env node
/**
 * process-service-timeline.js
 *
 * Processes raw fetch-service-timeline.js output into a final service timeline JSON.
 *
 * Usage: node scripts/process-service-timeline.js <raw-json> <output-json>
 *
 * Classifies events per PR (reuses shared event-processor), detects release windows,
 * computes per-window and all-up metrics, and generates insights.
 */

const fs = require('fs');
const path = require('path');

const {
  daysBetween,
  computeReviewWaitDays,
  processEvents,
  generateInsights,
  isBot
} = require('./lib/event-processor');

const { attachReleaseToPR } = require('./lib/release-pipeline');

/* ── Tool Call Event Conversion ───────────────────────────── */

function convertToolCalls(rawToolCalls, metadata) {
  if (!rawToolCalls || rawToolCalls.length === 0) return [];

  // Build package→language map
  const pkgLangMap = {};
  for (const [lang, pkg] of Object.entries(metadata.packages || {})) {
    pkgLangMap[pkg.name.toLowerCase()] = lang;
  }

  return rawToolCalls.map(tc => {
    // Normalize timestamp from CSV format (e.g. "3/13/2026, 7:40:28.978...")
    let ts;
    try {
      ts = new Date(tc.timestamp).toISOString();
    } catch {
      return null;
    }

    const lang = pkgLangMap[(tc.packageName || '').toLowerCase()] || tc.language || 'Unknown';
    return {
      type: 'tool_call',
      timestamp: ts,
      actor: 'developer',
      actorRole: 'tool',
      description: `${tc.toolName} — ${tc.success ? 'OK' : 'Failed'} (${(tc.durationMs / 1000).toFixed(1)}s)`,
      sentiment: tc.success ? 'neutral' : 'negative',
      language: lang,
      details: {
        toolName: tc.toolName,
        success: tc.success,
        durationMs: tc.durationMs,
        clientName: tc.clientName,
        clientType: tc.clientType,
        language: lang,
        packageName: tc.packageName
      }
    };
  }).filter(Boolean);
}

/* ── Release Window Detection ─────────────────────────────── */

/**
 * Detect API version from spec PR changed files.
 * Looks for paths matching: stable/<ver>/, preview/<ver>/, examples/<ver>/
 * Returns array of { version, confidence } sorted by confidence desc.
 */
function detectApiVersionFromFiles(changedFiles, specPath) {
  if (!changedFiles || changedFiles.length === 0) return [];

  const versionHits = {};  // version -> { count, confidence }
  const specPathLower = (specPath || '').toLowerCase();

  for (const filePath of changedFiles) {
    const fp = filePath.toLowerCase();
    // Only consider files within our TypeSpec project path
    if (specPathLower && !fp.startsWith(specPathLower.toLowerCase())) continue;

    // High confidence: stable/<version>/ or preview/<version>/
    const stableMatch = fp.match(/\bstable\/(\d{4}-\d{2}-\d{2})\//);
    const previewMatch = fp.match(/\bpreview\/(\d{4}-\d{2}-\d{2}-preview)\//);
    // Medium confidence: examples/<version>/
    const exampleMatch = fp.match(/\bexamples\/(\d{4}-\d{2}-\d{2}(?:-preview)?)\//);

    if (stableMatch) {
      const ver = stableMatch[1];
      if (!versionHits[ver]) versionHits[ver] = { count: 0, confidence: 0 };
      versionHits[ver].count++;
      versionHits[ver].confidence = Math.max(versionHits[ver].confidence, 3); // high
    }
    if (previewMatch) {
      const ver = previewMatch[1];
      if (!versionHits[ver]) versionHits[ver] = { count: 0, confidence: 0 };
      versionHits[ver].count++;
      versionHits[ver].confidence = Math.max(versionHits[ver].confidence, 3); // high
    }
    if (exampleMatch && !stableMatch && !previewMatch) {
      const ver = exampleMatch[1];
      if (!versionHits[ver]) versionHits[ver] = { count: 0, confidence: 0 };
      versionHits[ver].count++;
      versionHits[ver].confidence = Math.max(versionHits[ver].confidence, 1); // low
    }
  }

  return Object.entries(versionHits)
    .map(([version, { count, confidence }]) => ({ version, count, confidence }))
    .sort((a, b) => b.confidence - a.confidence || b.count - a.count);
}

/**
 * Build the set of acceptable package name variants from target metadata.
 * Used by both sibling detection and noise filtering.
 */
function buildTargetVariants(targetPackageName, targetPackageDir) {
  const targetNameLower = (targetPackageName || '').toLowerCase();
  const targetDirBase = (targetPackageDir || '').split('/').pop().toLowerCase();
  const variants = new Set();
  if (targetDirBase) variants.add(targetDirBase);
  if (targetNameLower) {
    variants.add(targetNameLower);
    const stripped = targetNameLower
      .replace(/^@azure[\/-]/, '')
      .replace(/^azure[\.-]mgmt[\.-]/, '')
      .replace(/^azure[\.-]/, '')
      .replace(/^com\.azure\.resourcemanager:/, '')
      .replace(/^sdk\/resourcemanager\/[^/]+\//, '');
    if (stripped) variants.add(stripped);
  }
  return { variants, targetDirBase, targetNameLower };
}

// Noise exclusion patterns — post-release automation, release automation, mass changes
const SDK_NOISE_TITLE_PATTERNS = [
  /^increment\s+versions?\s+for\s+/i,
  /^update\s+typespec\s+emitter\s+version\s+/i,
  /^prepare\s+for\s+release\b/i,
  /^update\s+changelog\b/i,
  /^\[AutoRelease\]/i,
];

const SDK_MASS_CHANGE_FILE_THRESHOLD = 500;
const SDK_MASS_CHANGE_TITLE_PATTERNS = [
  /^\*{3}NO_CI\*{3}/,
  /^\[automation\]\s+regenerate\s+sdk\b/i,
];

/**
 * Check if an SDK PR should be excluded entirely (noise, automation, mass change).
 * Runs BEFORE event processing so _raw data is still available.
 */
function shouldExcludeSDKPR(pr) {
  const title = pr.title || '';

  // Post-release automation (version bumps, release PRs)
  if (SDK_NOISE_TITLE_PATTERNS.some(re => re.test(title))) return 'noise';

  // Mass changes (broad refactors touching many packages)
  const changedFiles = pr._raw?.pr?.changed_files || 0;
  if (SDK_MASS_CHANGE_TITLE_PATTERNS.some(re => re.test(title)) && changedFiles >= 50) return 'mass-change';
  if (changedFiles >= SDK_MASS_CHANGE_FILE_THRESHOLD) return 'mass-change';

  return false;
}

/**
 * Check if an SDK PR targets a sibling sub-package (not our target).
 * Returns true if the PR should be excluded.
 *
 * Generalized — compares package references against known target names/dirs.
 * Checks: AutoPR title pattern, free-form title package references, changed files.
 */
function isSiblingSubPackage(pr, targetPackageName, targetPackageDir) {
  if (!targetPackageName && !targetPackageDir) return false;
  const title = (pr.title || '');
  const { variants: targetVariants, targetDirBase } = buildTargetVariants(targetPackageName, targetPackageDir);

  // Signal 1: AutoPR pattern [AutoPR <package-name>]
  const autoprMatch = title.match(/^\[AutoPR\s+([^\]]+)\]/i);
  if (autoprMatch) {
    const autoprPkg = autoprMatch[1].toLowerCase().trim();
    let matchesTarget = false;
    for (const variant of targetVariants) {
      if (autoprPkg === variant || autoprPkg.endsWith('/' + variant)) {
        matchesTarget = true;
        break;
      }
    }
    if (!matchesTarget) return true;
  }

  // Signal 2: Free-form title: "<target-package> for <qualifier>" where qualifier
  // looks like a sub-package/module name (not a common word like "api-version").
  // e.g. "Release Azure.ResourceManager.ContainerRegistry for RegistryTasks 2025-03-01-preview"
  if (targetDirBase && !autoprMatch) {
    const COMMON_QUALIFIERS = new Set([
      'api', 'version', 'apiversion', 'api-version', 'release', 'testing',
      'python', 'java', 'net', 'dotnet', 'go', 'javascript', 'js', 'rust',
      'typespec', 'swagger', 'openapi', 'preview', 'stable', 'beta',
    ]);

    for (const variant of targetVariants) {
      const escaped = variant.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const forMatch = title.match(new RegExp(escaped + '\\s+for\\s+(\\S+)', 'i'));
      if (forMatch) {
        const qualifier = forMatch[1].toLowerCase().replace(/[.\-_]/g, '');
        if (qualifier.length > 2
          && !COMMON_QUALIFIERS.has(qualifier)
          && !/^\d{4}/.test(qualifier)
          && !targetVariants.has(qualifier)) {
          return true;
        }
      }
    }
  }

  // Signal 3: Title or path contains an extended form of the target package name
  // e.g. "armcontainerregistrytasks" when target is "armcontainerregistry"
  // This catches non-AutoPR bracket titles like "[Containerregistrytasks-typespec]"
  if (targetDirBase && !autoprMatch) {
    const titleLower = title.toLowerCase().replace(/[.\-_]/g, '');
    const baseNorm = targetDirBase.replace(/[.\-_]/g, '');
    if (baseNorm.length >= 6) {
      const extRe = new RegExp(baseNorm.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '[a-z]');
      if (extRe.test(titleLower)) {
        // Title mentions an extended variant — only exclude if the ONLY occurrence
        // is extended (i.e. the title doesn't also mention the exact target alone)
        const stripped = titleLower.replace(
          new RegExp(baseNorm.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '[a-z]+', 'g'), ''
        );
        const exactStillPresent = stripped.includes(baseNorm);
        if (!exactStillPresent) return true;
      }
    }
  }

  return false;
}

function detectReleaseWindows(specPRs, sdkPRs, specPath) {
  const windows = [];

  // Track which SDK PRs have been claimed (one-to-one assignment)
  const claimed = {}; // lang -> Set of PR numbers
  for (const lang of Object.keys(sdkPRs)) {
    claimed[lang] = new Set();
  }

  const apiVersionRegex = /(?:API [Vv]ersion|Spec API version)[:\s]+(\d{4}-\d{2}-\d{2}(?:-preview)?)/;

  // Helper: extract API version from an SDK PR body/title
  function extractSdkPrVersion(pr) {
    const body = pr?._rawBody || '';
    const title = pr?.title || '';
    const vMatch = body.match(apiVersionRegex) ||
      title.match(/(?:api[- ]?version|for api[- ]version)\s+(\d{4}-\d{2}-\d{2}(?:-preview)?)/i) ||
      title.match(/\b(\d{4}-\d{2}-\d{2}-preview)\b/) ||
      (title.match(/\b(\d{4})\s+(\d{2})\s+(\d{2})(?:\s+preview)?\b/) ?
        { 1: title.match(/\b(\d{4})\s+(\d{2})\s+(\d{2})/).slice(1).join('-') +
          (title.toLowerCase().includes('preview') ? '-preview' : '') } : null);
    return vMatch ? vMatch[1] : null;
  }

  // Pass 1: explicit matches (spec PR number or merge commit in SDK PR body/title)
  for (let i = 0; i < specPRs.length; i++) {
    const spec = specPRs[i];
    const windowSdkPRs = {};

    for (const [lang, prs] of Object.entries(sdkPRs)) {
      const matching = prs.filter(pr => {
        if (claimed[lang].has(pr.number)) return false;
        const body = pr._rawBody || '';
        const title = pr.title || '';
        const mentionsSpec = body.includes(`#${spec.number}`) || title.includes(`#${spec.number}`);
        const mentionsCommit = spec._mergeCommitSha &&
          (body.includes(spec._mergeCommitSha) || body.includes(spec._mergeCommitSha.slice(0, 12)));
        return mentionsSpec || mentionsCommit;
      });

      if (matching.length > 0) {
        windowSdkPRs[lang] = matching.map(pr => pr.number);
        for (const pr of matching) claimed[lang].add(pr.number);
      }
    }

    windows.push({ specIndex: i, spec, windowSdkPRs });
  }

  // Build intermediate window objects with labels (for API-version-based assignment)
  // This determines the API version label for each spec PR window before
  // we try to assign unclaimed SDK PRs.
  const labeledWindows = [];
  for (const win of windows) {
    const spec = win.spec;

    // Signal 1: File-based version detection from spec PR changed files
    const fileVersions = detectApiVersionFromFiles(spec._changedFiles, specPath);
    const distinctHighConfVersions = fileVersions.filter(v => v.confidence >= 3);
    const highConfFileVer = distinctHighConfVersions.length === 1 ? distinctHighConfVersions[0] : null;

    // Signal 2: Determine versions from SDK PRs already claimed by this window
    const prsByVersion = {};
    const unversioned = {};
    for (const [lang, prNums] of Object.entries(win.windowSdkPRs)) {
      for (const num of prNums) {
        const pr = (sdkPRs[lang] || []).find(p => p.number === num);
        const ver = extractSdkPrVersion(pr);
        if (ver) {
          if (!prsByVersion[ver]) prsByVersion[ver] = {};
          if (!prsByVersion[ver][lang]) prsByVersion[ver][lang] = [];
          prsByVersion[ver][lang].push(num);
        } else {
          if (!unversioned[lang]) unversioned[lang] = [];
          unversioned[lang].push(num);
        }
      }
    }

    const versions = Object.keys(prsByVersion);
    if (versions.length > 1) {
      // Multiple API versions from one spec PR — split into separate windows
      // This handles parallel releases: one spec PR triggering different API versions
      for (const ver of versions.sort()) {
        labeledWindows.push({ spec, sdkPRs: prsByVersion[ver], label: `API ${ver}` });
      }
      // Unversioned PRs go into the first version window
      if (Object.keys(unversioned).length > 0) {
        const firstWin = labeledWindows[labeledWindows.length - versions.length];
        for (const [lang, nums] of Object.entries(unversioned)) {
          if (!firstWin.sdkPRs[lang]) firstWin.sdkPRs[lang] = [];
          firstWin.sdkPRs[lang].push(...nums);
        }
      }
    } else {
      // Single or no API version — determine label
      let label;
      if (highConfFileVer) {
        label = `API ${highConfFileVer.version}`;
      } else if (versions.length === 1) {
        label = `API ${versions[0]}`;
      } else {
        const dashDate = (spec.title || '').match(/(\d{4}-\d{2}-\d{2}(?:-preview)?)/);
        const spaceDate = (spec.title || '').match(/(\d{4})\s+(\d{2})\s+(\d{2})/);
        if (dashDate) {
          label = `API ${dashDate[1]}`;
        } else if (spaceDate) {
          const dateStr = `${spaceDate[1]}-${spaceDate[2]}-${spaceDate[3]}`;
          const previewSuffix = (spec.title || '').toLowerCase().includes('preview') ? '-preview' : '';
          label = `API ${dateStr}${previewSuffix}`;
        } else if (fileVersions.length === 1) {
          label = `API ${fileVersions[0].version}`;
        } else {
          label = `Spec PR #${spec.number}`;
        }
      }
      labeledWindows.push({ spec, sdkPRs: { ...win.windowSdkPRs }, label });
    }
  }

  // Pass 2: API-version-based assignment for unclaimed SDK PRs
  // If an unclaimed SDK PR has an explicit API version in its body, assign it
  // to the matching labeled window. This handles parallel development correctly.
  const versionToWindow = {};
  for (const win of labeledWindows) {
    if (win.label.startsWith('API ')) {
      const ver = win.label.replace('API ', '');
      if (!versionToWindow[ver]) versionToWindow[ver] = win;
    }
  }

  for (const [lang, prs] of Object.entries(sdkPRs)) {
    for (const pr of prs) {
      if (claimed[lang].has(pr.number)) continue;
      const ver = extractSdkPrVersion(pr);
      if (ver && versionToWindow[ver]) {
        const win = versionToWindow[ver];
        if (!win.sdkPRs[lang]) win.sdkPRs[lang] = [];
        win.sdkPRs[lang].push(pr.number);
        claimed[lang].add(pr.number);
      }
    }
  }

  // Pass 3: temporal proximity for remaining unclaimed SDK PRs (nearest spec PR)
  for (const [lang, prs] of Object.entries(sdkPRs)) {
    for (const pr of prs) {
      if (claimed[lang].has(pr.number)) continue;
      if (!pr.createdAt) continue;

      let bestWindow = null;
      let bestGap = Infinity;
      for (const win of labeledWindows) {
        const specMergedAt = win.spec.mergedAt;
        if (!specMergedAt) continue;
        const gap = daysBetween(specMergedAt, pr.createdAt);
        if (gap !== null && gap >= -2 && gap <= 30 && Math.abs(gap) < bestGap) {
          bestGap = Math.abs(gap);
          bestWindow = win;
        }
      }

      if (bestWindow) {
        if (!bestWindow.sdkPRs[lang]) bestWindow.sdkPRs[lang] = [];
        bestWindow.sdkPRs[lang].push(pr.number);
        claimed[lang].add(pr.number);
      }
    }
  }

  // Build final window objects from labeled windows
  const result = labeledWindows.map(win =>
    buildWindow(win.spec, win.sdkPRs, sdkPRs, win.label)
  );

  // Consolidate follow-up spec PRs under their API-version anchors.
  // Anchors have labels starting with "API "; non-anchors are follow-up
  // fixes/refactors that should be grouped with the nearest preceding anchor.
  const consolidated = consolidateWindows(result, specPRs, sdkPRs);

  // Re-number IDs
  consolidated.forEach((w, i) => { w.id = `rw-${i + 1}`; });
  return consolidated;
}

function consolidateWindows(windows, specPRs, sdkPRs) {
  // Step 1: Separate anchors (API-version windows) from followers
  const anchorWindows = [];
  const followerWindows = [];
  for (const win of windows) {
    if (win.label.startsWith('API ')) {
      anchorWindows.push(win);
    } else {
      followerWindows.push(win);
    }
  }

  if (anchorWindows.length === 0) return windows;

  // Step 2: Merge same-label anchors FIRST (before follower assignment)
  // e.g. multiple "API 2025-11-01" from different spec PRs → one anchor
  const labelMap = new Map();
  const dedupedAnchors = [];
  for (const win of anchorWindows) {
    if (labelMap.has(win.label)) {
      const target = labelMap.get(win.label);
      mergeWindowInto(target, win);
    } else {
      labelMap.set(win.label, win);
      dedupedAnchors.push(win);
    }
  }

  // Step 3: Assign followers to nearest deduplicated anchor (any direction)
  for (const follower of followerWindows) {
    const followerDate = new Date(follower.startDate);
    let bestAnchor = null;
    let bestDist = Infinity;
    for (const anchor of dedupedAnchors) {
      const dist = Math.abs(followerDate - new Date(anchor.startDate));
      if (dist < bestDist) {
        bestDist = dist;
        bestAnchor = anchor;
      }
    }
    if (bestAnchor) {
      mergeWindowInto(bestAnchor, follower);
    }
  }

  // Step 4: Recalculate date ranges
  for (const win of dedupedAnchors) {
    recalcWindowDates(win, specPRs, sdkPRs);
  }

  return dedupedAnchors;
}

function mergeWindowInto(target, source) {
  for (const num of source.specPRNumbers) {
    if (!target.specPRNumbers.includes(num)) target.specPRNumbers.push(num);
  }
  for (const [lang, nums] of Object.entries(source.sdkPRNumbers)) {
    if (!target.sdkPRNumbers[lang]) target.sdkPRNumbers[lang] = [];
    for (const num of nums) {
      if (!target.sdkPRNumbers[lang].includes(num)) target.sdkPRNumbers[lang].push(num);
    }
  }
}

function recalcWindowDates(win, specPRs, sdkPRs) {
  const allDates = [];
  for (const specNum of win.specPRNumbers) {
    const sp = specPRs.find(p => p.number === specNum);
    if (sp?.createdAt) allDates.push(sp.createdAt);
    if (sp?.mergedAt) allDates.push(sp.mergedAt);
  }
  for (const [lang, nums] of Object.entries(win.sdkPRNumbers)) {
    for (const num of nums) {
      const pr = (sdkPRs[lang] || []).find(p => p.number === num);
      if (pr?.createdAt) allDates.push(pr.createdAt);
      if (pr?.mergedAt) allDates.push(pr.mergedAt);
      if (pr?.release?.releasedAt) allDates.push(pr.release.releasedAt);
    }
  }
  allDates.sort();
  if (allDates.length > 0) {
    win.startDate = allDates[0];
    win.endDate = allDates[allDates.length - 1];
  }
}

function buildWindow(spec, windowSdkPRs, sdkPRs, label) {
  const allDates = [spec.createdAt].filter(Boolean);
  for (const [lang, prNums] of Object.entries(windowSdkPRs)) {
    for (const num of prNums) {
      const pr = (sdkPRs[lang] || []).find(p => p.number === num);
      if (pr) {
        if (pr.createdAt) allDates.push(pr.createdAt);
        if (pr.mergedAt) allDates.push(pr.mergedAt);
        // Include release date to extend window end
        if (pr.release?.releasedAt) allDates.push(pr.release.releasedAt);
      }
    }
  }
  if (spec.mergedAt) allDates.push(spec.mergedAt);
  allDates.sort();

  return {
    id: 'rw-0', // re-numbered later
    label,
    startDate: allDates[0] || spec.createdAt,
    endDate: allDates[allDates.length - 1] || spec.createdAt,
    specPRNumbers: [spec.number],
    sdkPRNumbers: windowSdkPRs,
    summary: {} // filled in below
  };
}

/* ── Per-Window Summary ───────────────────────────────────── */

function computeWindowSummary(window, specPRs, sdkPRs) {
  // Multi-spec-PR windows: gather all spec PRs, use first as the "anchor"
  const windowSpecPRs = window.specPRNumbers
    .map(n => specPRs.find(p => p.number === n))
    .filter(Boolean);
  const specPR = windowSpecPRs[0]; // anchor for pipeline gap calculation
  const windowSdkPRs = [];

  for (const [lang, nums] of Object.entries(window.sdkPRNumbers)) {
    for (const num of nums) {
      const pr = (sdkPRs[lang] || []).find(p => p.number === num);
      if (pr) windowSdkPRs.push(pr);
    }
  }

  const totalDays = daysBetween(window.startDate, window.endDate);
  // Aggregate spec PR review days across all spec PRs in window
  const specDaysArr = windowSpecPRs
    .filter(sp => sp.createdAt && sp.mergedAt)
    .map(sp => daysBetween(sp.createdAt, sp.mergedAt))
    .filter(d => d != null);
  const specDays = specDaysArr.length > 0
    ? specDaysArr.reduce((a, b) => a + b, 0) : null;

  // Pipeline gap: first spec merged → first SDK PR created
  const firstSpecMerged = windowSpecPRs
    .filter(sp => sp.mergedAt)
    .map(sp => sp.mergedAt)
    .sort()[0] || null;
  const firstSdkCreated = windowSdkPRs
    .filter(pr => pr.createdAt)
    .map(pr => pr.createdAt)
    .sort()[0] || null;
  const pipeGap = (firstSpecMerged && firstSdkCreated)
    ? daysBetween(firstSpecMerged, firstSdkCreated)
    : null;

  // Nags and manual fixes across all PRs
  const allPRs = [...windowSpecPRs, ...windowSdkPRs];
  const totalNags = allPRs.reduce((n, pr) =>
    n + (pr.events || []).filter(e => e.type === 'author_nag').length, 0);
  const totalManualFixes = allPRs.reduce((n, pr) =>
    n + (pr.events || []).filter(e => e.type === 'manual_fix').length, 0);

  // Reviewers
  const reviewers = new Set();
  for (const pr of allPRs) {
    for (const e of (pr.events || [])) {
      if (e.actorRole === 'reviewer') reviewers.add(e.actor);
    }
  }

  // Review wait
  const prsWithWait = allPRs.filter(p => p.state !== 'missing');
  const totalReviewWaitDays = prsWithWait.reduce((s, p) => s + (p.reviewWaitDays || 0), 0);
  const totalReviewWaitCycles = prsWithWait.reduce((s, p) => s + (p.reviewWaitCycles || 0), 0);

  return {
    totalDurationDays: totalDays != null ? Math.round(totalDays * 100) / 100 : null,
    specPRDays: specDays != null ? Math.round(specDays * 100) / 100 : null,
    specPRCount: windowSpecPRs.length,
    pipelineGapDays: pipeGap != null && pipeGap > 0 ? Math.round(pipeGap * 100) / 100 : null,
    totalNags,
    totalManualFixes,
    totalUniqueReviewers: reviewers.size,
    totalReviewWaitDays: Math.round(totalReviewWaitDays * 100) / 100,
    totalReviewWaitCycles
  };
}

/* ── All-Up Summary ───────────────────────────────────────── */

function computeServiceSummary(specPRs, sdkPRs, toolCallEvents, releaseWindows) {
  const allSdkPRs = Object.values(sdkPRs).flat();
  const allPRs = [...specPRs, ...allSdkPRs];

  // Cycle times (per release window)
  const cycleTimes = releaseWindows
    .map(w => daysBetween(w.startDate, w.endDate))
    .filter(d => d !== null);
  const avgCycleTime = cycleTimes.length > 0
    ? Math.round((cycleTimes.reduce((s, d) => s + d, 0) / cycleTimes.length) * 100) / 100
    : null;

  // Pipeline gaps (per window)
  const pipeGaps = releaseWindows
    .map(w => w.summary?.pipelineGapDays)
    .filter(d => d != null && d > 0);
  const avgPipeGap = pipeGaps.length > 0
    ? Math.round((pipeGaps.reduce((s, d) => s + d, 0) / pipeGaps.length) * 100) / 100
    : null;

  // Review wait
  const allPRsWait = allPRs.filter(p => p.state !== 'missing');
  const totalReviewWait = allPRsWait.reduce((s, p) => s + (p.reviewWaitDays || 0), 0);
  const avgReviewWait = allPRsWait.length > 0
    ? Math.round((totalReviewWait / allPRsWait.length) * 100) / 100
    : null;

  // Nags and manual fixes
  const totalNags = allPRs.reduce((n, pr) =>
    n + (pr.events || []).filter(e => e.type === 'author_nag').length, 0);
  const totalManualFixes = allPRs.reduce((n, pr) =>
    n + (pr.events || []).filter(e => e.type === 'manual_fix').length, 0);

  // Tool calls
  const totalToolCalls = toolCallEvents.length;
  const successfulToolCalls = toolCallEvents.filter(e => e.details?.success !== false).length;
  const toolCallSuccessRate = totalToolCalls > 0
    ? Math.round((successfulToolCalls / totalToolCalls) * 100) / 100
    : null;

  // Language breakdown
  const languageBreakdown = {};
  for (const [lang, prs] of Object.entries(sdkPRs)) {
    const merged = prs.filter(p => p.mergedAt && p.createdAt);
    const durations = merged.map(p => daysBetween(p.createdAt, p.mergedAt)).filter(d => d !== null);
    const avgDays = durations.length > 0
      ? Math.round((durations.reduce((s, d) => s + d, 0) / durations.length) * 100) / 100
      : null;

    languageBreakdown[lang] = {
      prCount: prs.length,
      mergedCount: merged.length,
      avgDays,
      releaseCount: prs.filter(p => p.release?.status === 'released').length
    };
  }

  // Top reviewers
  const reviewerCounts = {};
  for (const pr of allPRs) {
    for (const e of (pr.events || [])) {
      if (e.actorRole === 'reviewer' && !isBot(e.actor)) {
        reviewerCounts[e.actor] = (reviewerCounts[e.actor] || 0) + 1;
      }
    }
  }
  const topReviewers = Object.entries(reviewerCounts)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10)
    .map(([login, reviewCount]) => ({ login, reviewCount }));

  // Automation rate
  const automatedCount = allSdkPRs.filter(p => p.generationFlow === 'automated').length;
  const automationRate = allSdkPRs.length > 0
    ? Math.round((automatedCount / allSdkPRs.length) * 100) / 100
    : null;

  return {
    totalSpecPRs: specPRs.length,
    totalSDKPRs: allSdkPRs.length,
    totalReleases: allSdkPRs.filter(p => p.release?.status === 'released').length,
    avgCycleTimeDays: avgCycleTime,
    avgPipelineGapDays: avgPipeGap,
    avgReviewWaitDays: avgReviewWait,
    totalNags,
    totalManualFixes,
    totalToolCalls,
    toolCallSuccessRate,
    languageBreakdown,
    topReviewers,
    automationRate
  };
}

/* ── Service-Level Insights ───────────────────────────────── */

function generateServiceInsights(specPRs, sdkPRs, releaseWindows, summary) {
  const insights = [];

  // Recurring bottleneck language
  const langAvgs = Object.entries(summary.languageBreakdown || {})
    .filter(([, v]) => v.avgDays !== null && v.mergedCount >= 2)
    .sort((a, b) => b[1].avgDays - a[1].avgDays);

  if (langAvgs.length >= 2) {
    const slowest = langAvgs[0];
    const fastest = langAvgs[langAvgs.length - 1];
    if (slowest[1].avgDays > fastest[1].avgDays * 2) {
      insights.push({
        type: 'bottleneck', severity: 'warning',
        description: `${slowest[0]} is consistently slowest (avg ${slowest[1].avgDays}d vs ${fastest[0]} at ${fastest[1].avgDays}d)`
      });
    }
  }

  // High nag rate
  if (summary.totalNags > 5) {
    insights.push({
      type: 'nag', severity: 'warning',
      description: `${summary.totalNags} total review nags across all release cycles`
    });
  }

  // Manual fixes on automated PRs
  if (summary.totalManualFixes > 0) {
    insights.push({
      type: 'manual_fix', severity: summary.totalManualFixes > 3 ? 'warning' : 'info',
      description: `${summary.totalManualFixes} manual fix(es) needed on auto-generated PRs`
    });
  }

  // Automation rate
  if (summary.automationRate !== null) {
    if (summary.automationRate < 0.5) {
      insights.push({
        type: 'summary', severity: 'info',
        description: `Low automation rate: ${Math.round(summary.automationRate * 100)}% of SDK PRs are automated`
      });
    } else if (summary.automationRate >= 0.9) {
      insights.push({
        type: 'positive', severity: 'info',
        description: `High automation: ${Math.round(summary.automationRate * 100)}% of SDK PRs are automated`
      });
    }
  }

  // Tool call failure patterns
  if (summary.toolCallSuccessRate !== null && summary.toolCallSuccessRate < 0.8) {
    insights.push({
      type: 'bottleneck', severity: 'warning',
      description: `Tool call success rate is ${Math.round(summary.toolCallSuccessRate * 100)}% (${summary.totalToolCalls} calls)`
    });
  }

  // Average cycle time
  if (summary.avgCycleTimeDays !== null) {
    insights.push({
      type: 'summary', severity: summary.avgCycleTimeDays > 30 ? 'warning' : 'info',
      description: `Average release cycle: ${summary.avgCycleTimeDays}d across ${releaseWindows.length} windows`
    });
  }

  // Open PRs
  const allSdkPRs = Object.values(sdkPRs).flat();
  const openPRs = allSdkPRs.filter(p => p.state === 'open');
  if (openPRs.length > 0) {
    insights.push({
      type: 'bottleneck', severity: 'warning',
      description: `${openPRs.length} SDK PR(s) still open`
    });
  }

  return insights;
}

/* ── Main ─────────────────────────────────────────────────── */

function main() {
  const args = process.argv.slice(2);
  if (args.length < 2) {
    console.error('Usage: node process-service-timeline.js <raw-json> <output-json>');
    process.exit(1);
  }

  const [rawFile, outFile] = args;
  const data = JSON.parse(fs.readFileSync(rawFile, 'utf8'));

  const metadata = data.metadata;
  const rawSpecPRs = data.specPRs || [];
  const rawSdkPRs = data.sdkPRs || {};
  const rawToolCalls = data.toolCalls || [];

  // Determine the primary owner (most frequent spec PR author)
  const authorCounts = {};
  for (const pr of rawSpecPRs) {
    authorCounts[pr.author] = (authorCounts[pr.author] || 0) + 1;
  }
  const owner = Object.entries(authorCounts).sort((a, b) => b[1] - a[1])[0]?.[0] || 'unknown';

  // Process spec PR events
  console.error('Processing spec PRs...');
  const specPRs = rawSpecPRs.map(pr => {
    const out = { ...pr, _mergeCommitSha: pr._raw?.pr?.merge_commit_sha || null };
    const rawBody = pr._raw?.pr?.body || '';
    out._rawBody = rawBody;
    delete out._raw;
    out.events = processEvents(pr, pr.author || owner);
    const w = computeReviewWaitDays(out.events, out.readyForReviewAt || null);
    out.reviewWaitDays = w.reviewWaitDays;
    out.reviewWaitCycles = w.reviewWaitCycles;
    return out;
  });

  // Process SDK PR events (grouped by language)
  console.error('Processing SDK PRs...');
  const sdkPRs = {};
  let noiseDropCount = 0;
  for (const [lang, prs] of Object.entries(rawSdkPRs)) {
    sdkPRs[lang] = prs.map(pr => {
      // Early exclusion — check BEFORE expensive event processing while _raw is available
      const excludeReason = shouldExcludeSDKPR(pr);
      if (excludeReason) {
        noiseDropCount++;
        return null;
      }

      const out = { ...pr };
      const rawBody = pr._raw?.pr?.body || '';
      out._rawBody = rawBody;
      delete out._raw;
      // For bot-authored SDK PRs (e.g. azure-sdk), use the human spec owner
      // so that nag/manual-fix detection works correctly
      const prOwner = isBot(pr.author) ? owner : (pr.author || owner);
      out.events = processEvents(pr, prOwner);
      const w = computeReviewWaitDays(out.events, out.readyForReviewAt || null);
      out.reviewWaitDays = w.reviewWaitDays;
      out.reviewWaitCycles = w.reviewWaitCycles;

      // Attach release pipeline data if present in raw fetch
      if (pr._release) {
        attachReleaseToPR(out, pr._release);
        delete out._release; // clean up raw data
      }

      return out;
    }).filter(Boolean);
  }
  if (noiseDropCount > 0) {
    console.error(`Excluded ${noiseDropCount} noise/automation/mass-change SDK PRs`);
  }

  // Convert tool call telemetry
  const toolCallEvents = convertToolCalls(rawToolCalls, metadata);

  // Inject tool call events into appropriate SDK PR event lists
  for (const tc of toolCallEvents) {
    const lang = tc.language;
    const prs = sdkPRs[lang] || [];
    if (prs.length === 0) continue;

    // Find nearest PR by timestamp
    const tcTime = new Date(tc.timestamp).getTime();
    let bestPR = prs[0];
    let bestDist = Infinity;
    for (const pr of prs) {
      const prStart = new Date(pr.createdAt || 0).getTime();
      const prEnd = new Date(pr.mergedAt || pr.closedAt || Date.now()).getTime();
      // Prefer PRs where the tool call falls within the PR's active period
      if (tcTime >= prStart && tcTime <= prEnd) {
        bestPR = pr;
        bestDist = 0;
        break;
      }
      const dist = Math.min(Math.abs(tcTime - prStart), Math.abs(tcTime - prEnd));
      if (dist < bestDist) { bestDist = dist; bestPR = pr; }
    }
    bestPR.events.push(tc);
    bestPR.events.sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  }

  // Filter out SDK PRs targeting sibling sub-packages
  let siblingDropCount = 0;
  for (const [lang, prs] of Object.entries(sdkPRs)) {
    const pkg = metadata.packages[lang];
    const targetName = pkg?.name || '';
    const targetDir = pkg?.packageDir || '';
    const before = prs.length;
    sdkPRs[lang] = prs.filter(pr => !isSiblingSubPackage(pr, targetName, targetDir));
    const dropped = before - sdkPRs[lang].length;
    if (dropped > 0) {
      console.error(`  Filtered ${dropped} sibling-package ${lang} PRs`);
      siblingDropCount += dropped;
    }
  }
  if (siblingDropCount > 0) {
    console.error(`Filtered ${siblingDropCount} total sibling sub-package PRs`);
  }

  // Detect release windows
  console.error('Detecting release windows...');
  const releaseWindows = detectReleaseWindows(specPRs, sdkPRs, metadata.specPath);

  // Drop SDK PRs not claimed by any release window (outside lookback scope)
  const windowedNums = new Set();
  for (const w of releaseWindows) {
    for (const nums of Object.values(w.sdkPRNumbers)) {
      for (const n of nums) windowedNums.add(n);
    }
  }
  let droppedOrphans = 0;
  for (const lang of Object.keys(sdkPRs)) {
    const before = sdkPRs[lang].length;
    sdkPRs[lang] = sdkPRs[lang].filter(pr => windowedNums.has(pr.number));
    droppedOrphans += before - sdkPRs[lang].length;
  }
  if (droppedOrphans > 0) console.error(`Dropped ${droppedOrphans} orphan SDK PRs (no matching spec PR in lookback)`);

  // Compute per-window summaries
  for (const win of releaseWindows) {
    win.summary = computeWindowSummary(win, specPRs, sdkPRs);
  }

  // Compute all-up summary
  const summary = computeServiceSummary(specPRs, sdkPRs, toolCallEvents, releaseWindows);

  // Generate insights
  const insights = generateServiceInsights(specPRs, sdkPRs, releaseWindows, summary);

  // Compute time range from all events
  const allTs = [];
  for (const pr of specPRs) {
    for (const e of pr.events) {
      allTs.push(e.timestamp);
      if (e.endTimestamp) allTs.push(e.endTimestamp);
    }
  }
  for (const prs of Object.values(sdkPRs)) {
    for (const pr of prs) {
      for (const e of pr.events) {
        allTs.push(e.timestamp);
        if (e.endTimestamp) allTs.push(e.endTimestamp);
      }
    }
  }
  allTs.sort();
  const startDate = allTs[0] || data._meta?.lookbackDate || new Date().toISOString();
  const endDate = allTs[allTs.length - 1] || new Date().toISOString();

  // Clean up internal fields before output
  for (const pr of specPRs) { delete pr._mergeCommitSha; delete pr._rawBody; }
  for (const prs of Object.values(sdkPRs)) {
    for (const pr of prs) { delete pr._rawBody; }
  }

  // Build output
  const output = {
    type: 'service-timeline',
    service: metadata.service,
    specPath: metadata.specPath,
    namespace: metadata.namespace,
    generatedAt: new Date().toISOString(),
    lookback: {
      requestedDays: data._meta?.lookbackDays || 365,
      startDate: data._meta?.lookbackDate || startDate,
      endDate: data._meta?.fetchedAt || endDate
    },
    startDate,
    endDate,
    packages: metadata.packages,
    specPRs,
    sdkPRs,
    releaseWindows,
    summary,
    insights
  };

  fs.writeFileSync(outFile, JSON.stringify(output, null, 2));
  console.error(`\n✅ ${outFile}`);
  console.error(`   ${specPRs.length} spec PRs`);
  for (const [lang, prs] of Object.entries(sdkPRs)) {
    console.error(`   ${lang}: ${prs.length} PRs`);
  }
  console.error(`   ${releaseWindows.length} release windows`);
  console.error(`   ${toolCallEvents.length} tool calls`);
  console.error(`   ${insights.length} insights`);
}

main();
