#!/usr/bin/env node
/**
 * backfill-consolidate-windows.js
 *
 * Re-applies the release-window detection + consolidation logic to existing
 * service timeline JSON files (in-place). This groups follow-up spec PRs
 * under their API-version anchor windows and recalculates summaries.
 *
 * Usage: node scripts/backfill-consolidate-windows.js data/service-*.json
 */

const fs = require('fs');
const path = require('path');

const { daysBetween, computeReviewWaitDays } = require('./lib/event-processor');

function run(filePath) {
  const d = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  if (d.type !== 'service-timeline') {
    console.error(`Skipping ${filePath}: not a service-timeline`);
    return;
  }

  console.error(`Processing ${filePath}...`);
  console.error(`  Before: ${d.releaseWindows.length} windows`);

  // Remove noise/automation/mass-change PRs from sdkPRs
  const excludeNums = new Set();
  const noisePatterns = [
    /^increment\s+versions?\s+for\s+/i,
    /^update\s+typespec\s+emitter\s+version\s+/i,
    /^prepare\s+for\s+release\b/i,
    /^update\s+changelog\b/i,
    /^\[AutoRelease\]/i,
  ];
  const massPatterns = [/^\*{3}NO_CI\*{3}/, /^\[automation\]\s+regenerate\s+sdk\b/i];
  for (const [lang, prs] of Object.entries(d.sdkPRs || {})) {
    for (const pr of prs) {
      const title = pr.title || '';
      if (noisePatterns.some(re => re.test(title))) {
        excludeNums.add(pr.number);
      } else if (massPatterns.some(re => re.test(title))) {
        excludeNums.add(pr.number);
      }
    }
    d.sdkPRs[lang] = prs.filter(pr => !excludeNums.has(pr.number));
  }
  if (excludeNums.size > 0) console.error(`  Removed ${excludeNums.size} noise/automation PRs`);

  // Re-detect windows from scratch using the spec + SDK PR data
  const specPRs = d.specPRs || [];
  const sdkPRs = d.sdkPRs || {};

  const windows = detectReleaseWindows(specPRs, sdkPRs, d.specPath);

  // Recompute per-window summaries
  for (const win of windows) {
    win.summary = computeWindowSummary(win, specPRs, sdkPRs);
  }

  d.releaseWindows = windows;

  // Update overall summary counts
  if (d.summary) {
    d.summary.totalReleaseWindows = windows.length;
  }

  console.error(`  After: ${windows.length} windows`);
  for (const w of windows) {
    const sdkCount = Object.values(w.sdkPRNumbers || {}).flat().length;
    console.error(`    ${w.label} | ${w.specPRNumbers.length} specs, ${sdkCount} SDKs | ${w.startDate?.slice(0, 10)} → ${w.endDate?.slice(0, 10)}`);
  }

  fs.writeFileSync(filePath, JSON.stringify(d, null, 2) + '\n');
  console.error(`  Saved ${filePath}`);
}

/* ── Release Window Detection (mirrors process-service-timeline.js) ── */

function detectApiVersionFromFiles(changedFiles, specPath) {
  if (!changedFiles || changedFiles.length === 0) return [];
  const versionHits = {};
  const specPathLower = (specPath || '').toLowerCase();
  for (const filePath of changedFiles) {
    const fp = filePath.toLowerCase();
    if (specPathLower && !fp.startsWith(specPathLower)) continue;
    const stableMatch = fp.match(/\bstable\/(\d{4}-\d{2}-\d{2})\//);
    const previewMatch = fp.match(/\bpreview\/(\d{4}-\d{2}-\d{2}-preview)\//);
    const exampleMatch = fp.match(/\bexamples\/(\d{4}-\d{2}-\d{2}(?:-preview)?)\//);
    if (stableMatch) {
      const ver = stableMatch[1];
      if (!versionHits[ver]) versionHits[ver] = { count: 0, confidence: 0 };
      versionHits[ver].count++;
      versionHits[ver].confidence = Math.max(versionHits[ver].confidence, 3);
    }
    if (previewMatch) {
      const ver = previewMatch[1];
      if (!versionHits[ver]) versionHits[ver] = { count: 0, confidence: 0 };
      versionHits[ver].count++;
      versionHits[ver].confidence = Math.max(versionHits[ver].confidence, 3);
    }
    if (exampleMatch && !stableMatch && !previewMatch) {
      const ver = exampleMatch[1];
      if (!versionHits[ver]) versionHits[ver] = { count: 0, confidence: 0 };
      versionHits[ver].count++;
      versionHits[ver].confidence = Math.max(versionHits[ver].confidence, 1);
    }
  }
  return Object.entries(versionHits)
    .map(([version, { count, confidence }]) => ({ version, count, confidence }))
    .sort((a, b) => b.confidence - a.confidence || b.count - a.count);
}

function detectReleaseWindows(specPRs, sdkPRs, specPath) {
  const windows = [];
  const claimed = {};
  for (const lang of Object.keys(sdkPRs)) {
    claimed[lang] = new Set();
  }

  const apiVersionRegex = /(?:API [Vv]ersion|Spec API version)[:\s]+(\d{4}-\d{2}-\d{2}(?:-preview)?)/;

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

  // Pass 1: explicit matches (spec PR number or merge commit in body/title)
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

  // Build intermediate labeled windows (determines API version for each)
  const labeledWindows = [];
  for (const win of windows) {
    const spec = win.spec;

    const fileVersions = detectApiVersionFromFiles(spec._changedFiles, specPath);
    const distinctHighConf = fileVersions.filter(v => v.confidence >= 3);
    const highConfFileVer = distinctHighConf.length === 1 ? distinctHighConf[0] : null;

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
      for (const ver of versions.sort()) {
        labeledWindows.push({ spec, sdkPRs: prsByVersion[ver], label: `API ${ver}` });
      }
      if (Object.keys(unversioned).length > 0) {
        const firstWin = labeledWindows[labeledWindows.length - versions.length];
        for (const [lang, nums] of Object.entries(unversioned)) {
          if (!firstWin.sdkPRs[lang]) firstWin.sdkPRs[lang] = [];
          firstWin.sdkPRs[lang].push(...nums);
        }
      }
    } else {
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

  // Pass 3: temporal proximity for remaining unclaimed SDK PRs
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

  // Build final window objects
  const result = labeledWindows.map(win =>
    buildWindow(win.spec, win.sdkPRs, sdkPRs, win.label)
  );

  // Consolidation: merge follow-up spec PRs into API-version anchors
  const consolidated = consolidateWindows(result, specPRs, sdkPRs);
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
        if (pr.release?.releasedAt) allDates.push(pr.release.releasedAt);
      }
    }
  }
  if (spec.mergedAt) allDates.push(spec.mergedAt);
  allDates.sort();

  return {
    id: 'rw-0',
    label,
    startDate: allDates[0] || spec.createdAt,
    endDate: allDates[allDates.length - 1] || spec.createdAt,
    specPRNumbers: [spec.number],
    sdkPRNumbers: windowSdkPRs,
    summary: {}
  };
}

/* ── Per-Window Summary ── */

function computeWindowSummary(window, specPRs, sdkPRs) {
  const windowSpecPRs = window.specPRNumbers
    .map(n => specPRs.find(p => p.number === n))
    .filter(Boolean);
  const specPR = windowSpecPRs[0];
  const windowSdkPRs = [];

  for (const [lang, nums] of Object.entries(window.sdkPRNumbers)) {
    for (const num of nums) {
      const pr = (sdkPRs[lang] || []).find(p => p.number === num);
      if (pr) windowSdkPRs.push(pr);
    }
  }

  const totalDays = daysBetween(window.startDate, window.endDate);
  const specDaysArr = windowSpecPRs
    .filter(sp => sp.createdAt && sp.mergedAt)
    .map(sp => daysBetween(sp.createdAt, sp.mergedAt))
    .filter(d => d != null);
  const specDays = specDaysArr.length > 0
    ? specDaysArr.reduce((a, b) => a + b, 0) : null;

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

  const allPRs = [...windowSpecPRs, ...windowSdkPRs];
  const totalNags = allPRs.reduce((n, pr) =>
    n + (pr.events || []).filter(e => e.type === 'author_nag').length, 0);
  const totalManualFixes = allPRs.reduce((n, pr) =>
    n + (pr.events || []).filter(e => e.type === 'manual_fix').length, 0);

  const reviewers = new Set();
  for (const pr of allPRs) {
    for (const e of (pr.events || [])) {
      if (e.actorRole === 'reviewer') reviewers.add(e.actor);
    }
  }

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

// Process all files passed as arguments
const files = process.argv.slice(2);
if (files.length === 0) {
  console.error('Usage: node scripts/backfill-consolidate-windows.js data/service-*.json');
  process.exit(1);
}
for (const f of files) run(f);
