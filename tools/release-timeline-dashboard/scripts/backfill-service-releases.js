#!/usr/bin/env node
/**
 * backfill-service-releases.js
 *
 * Fetches release pipeline data from Azure DevOps and adds it to existing
 * service timeline JSON files (data/service-*.json).
 *
 * Usage: node scripts/backfill-service-releases.js [data/service-*.json ...]
 *
 * If no files are specified, processes all service-*.json files in data/.
 */

const fs = require('fs');
const path = require('path');
const {
  fetchReleaseForPR,
  inferServiceName,
  attachReleaseToPR
} = require('./lib/release-pipeline');

function deriveServiceName(lang, sdkPRs, packages) {
  // Try TypeSpec metadata first
  const pkg = packages?.[lang];
  if (pkg?.serviceDir) {
    const segments = pkg.serviceDir.replace(/^sdk\//, '').split('/').filter(Boolean);
    if (segments.length > 0) return segments[segments.length - 1];
  }

  // Fallback: infer from first PR title
  for (const pr of sdkPRs) {
    const svc = inferServiceName(pr.title || '');
    if (svc) return svc;
  }
  return null;
}

function main() {
  const args = process.argv.slice(2);

  let files;
  if (args.length > 0) {
    files = args;
  } else {
    const dataDir = path.join(__dirname, '..', 'data');
    files = fs.readdirSync(dataDir)
      .filter(f => f.startsWith('service-') && f.endsWith('.json'))
      .map(f => path.join(dataDir, f));
  }

  if (files.length === 0) {
    console.error('No service timeline files found');
    process.exit(1);
  }

  let totalUpdated = 0;

  for (const filepath of files) {
    const name = path.basename(filepath, '.json');
    console.error(`\n=== Processing ${name} ===`);

    const data = JSON.parse(fs.readFileSync(filepath, 'utf-8'));
    if (data.type !== 'service-timeline') {
      console.error(`  Skipping — not a service timeline`);
      continue;
    }

    let fileUpdated = false;

    for (const [lang, prs] of Object.entries(data.sdkPRs)) {
      const serviceName = deriveServiceName(lang, prs, data.packages);
      const packageName = data.packages?.[lang]?.name || null;

      if (!serviceName) {
        console.error(`  ⚠ ${lang}: can't derive service name, skipping`);
        continue;
      }

      console.error(`\n  ${lang}: service="${serviceName}", package="${packageName}"`);
      let langUpdated = 0;

      for (const pr of prs) {
        // Skip PRs that already have release data or aren't merged
        if (pr.release || !pr.mergedAt) continue;

        console.error(`    PR #${pr.number} merged ${pr.mergedAt.substring(0, 10)}...`);
        const releaseData = fetchReleaseForPR({
          language: lang,
          serviceName,
          packageName,
          mergedAt: pr.mergedAt
        });

        if (releaseData) {
          attachReleaseToPR(pr, releaseData);
          langUpdated++;
          fileUpdated = true;
          const status = pr.release?.status || 'unknown';
          const gap = pr.release?.releaseGapDays;
          console.error(`      → ${status}${gap != null ? ` (${gap}d gap)` : ''}`);
        } else {
          console.error(`      → no pipeline found`);
        }
      }

      console.error(`  ${lang}: ${langUpdated} PRs updated with release data`);
    }

    if (fileUpdated) {
      // Update endDate if releases extend it
      const releaseDates = [];
      for (const prs of Object.values(data.sdkPRs)) {
        for (const pr of prs) {
          if (pr.release?.releasedAt) releaseDates.push(pr.release.releasedAt);
        }
      }
      if (releaseDates.length > 0) {
        const latest = releaseDates.sort().pop();
        if (latest > data.endDate) {
          console.error(`  Extending endDate: ${data.endDate.substring(0, 10)} → ${latest.substring(0, 10)}`);
          data.endDate = latest;
        }
      }

      // Update window endDates to include release dates
      for (const win of (data.releaseWindows || [])) {
        const winReleaseDates = [];
        for (const [lang, prNums] of Object.entries(win.sdkPRNumbers || {})) {
          for (const num of prNums) {
            const pr = (data.sdkPRs[lang] || []).find(p => p.number === num);
            if (pr?.release?.releasedAt) winReleaseDates.push(pr.release.releasedAt);
          }
        }
        if (winReleaseDates.length > 0) {
          const latest = winReleaseDates.sort().pop();
          if (latest > win.endDate) {
            win.endDate = latest;
          }
        }
      }

      // Recalculate window summaries with release data
      for (const win of (data.releaseWindows || [])) {
        const windowPRs = [];
        for (const [lang, prNums] of Object.entries(win.sdkPRNumbers || {})) {
          for (const num of prNums) {
            const pr = (data.sdkPRs[lang] || []).find(p => p.number === num);
            if (pr) windowPRs.push(pr);
          }
        }
        const released = windowPRs.filter(p => p.release?.status === 'released');
        const gaps = released.map(p => p.release.releaseGapDays).filter(g => g != null);
        if (gaps.length > 0) {
          win.summary.avgReleaseGapDays = parseFloat((gaps.reduce((a, b) => a + b, 0) / gaps.length).toFixed(1));
          win.summary.maxReleaseGapDays = parseFloat(Math.max(...gaps).toFixed(1));
        }
        const pending = windowPRs.filter(p => p.mergedAt && (!p.release || p.release.status === 'pending'));
        if (pending.length > 0 || released.length > 0) {
          win.summary.pendingReleases = pending.length;
        }
      }

      fs.writeFileSync(filepath, JSON.stringify(data, null, 2));
      totalUpdated++;
      console.error(`\n  📝 Saved ${path.basename(filepath)}`);
    } else {
      console.error(`\n  No changes needed for ${name}`);
    }
  }

  console.error(`\n✅ Done! Updated ${totalUpdated} file(s).`);
}

main();
