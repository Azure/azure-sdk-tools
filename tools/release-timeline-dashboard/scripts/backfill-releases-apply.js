#!/usr/bin/env node
// Reads raw DevOps pipeline data from backfill-release-data/ and updates
// sample JSON files with release info, events, and insights.
//
// Usage: node scripts/backfill-releases-apply.js

const fs = require('fs');
const path = require('path');

const DATA_DIR = 'backfill-release-data';
const SAMPLE_DIR = 'data';

const PIPELINE_PATTERNS = {
  'Java':       (svc) => [`java - ${svc}`],
  'Go':         (svc) => [`go - arm${svc}`],
  'Python':     (svc) => [`python - ${svc}`],
  '.NET':       (svc) => [`net - ${svc} - mgmt`, `net - ${svc}`],
  'JavaScript': (svc) => [`js - ${svc} - mgmt`, `js - ${svc}`]
};

const PACKAGE_PATTERNS = {
  'Java':       (svc) => `azure-resourcemanager-${svc}`,
  'Go':         (svc) => `sdk/resourcemanager/${svc}/arm${svc}`,
  'Python':     (svc) => `azure-mgmt-${svc}`,
  '.NET':       (svc) => `Azure.ResourceManager.${svc.charAt(0).toUpperCase() + svc.slice(1)}`,
  'JavaScript': (svc) => `@azure/arm-${svc}`
};

function inferServiceName(prTitle) {
  const patterns = [
    /\[AutoPR\s+(?:azure-resourcemanager-|azure-mgmt-|arm-)([^\]]+)\]/i,
    /\[AutoPR\s+(?:Azure\.ResourceManager\.)([^\]]+)\]/i,
    /\[AutoPR\s+sdk-resourcemanager\/([^/\]]+)/i,
    /\[AutoPR\s+@azure-arm-([^\]]+)\]/i,
    /\[AutoPR\s+@azure-([^\]]+)\]/i,
    /\[AutoPR\s+([^\]]+)\]/i,
  ];
  for (const pat of patterns) {
    const m = prTitle.match(pat);
    if (m) return m[1].toLowerCase().replace(/[-_]$/, '').replace(/-generated$/, '');
  }
  return null;
}

function readJson(filepath) {
  try {
    return JSON.parse(fs.readFileSync(filepath, 'utf-8'));
  } catch {
    return null;
  }
}

function findDefId(lang, svc) {
  const names = PIPELINE_PATTERNS[lang]?.(svc) || [];
  for (const name of names) {
    const safe = name.replace(/ /g, '_').replace(/\//g, '_');
    const data = readJson(path.join(DATA_DIR, `def-${safe}.json`));
    if (data?.value?.length > 0) {
      return { defId: data.value[0].id, name: data.value[0].name };
    }
  }
  return null;
}

function findReleaseBuild(defId, afterDate) {
  // Try to find builds file
  const files = fs.readdirSync(DATA_DIR).filter(f => f.startsWith(`builds-${defId}-`));
  for (const file of files) {
    const data = readJson(path.join(DATA_DIR, file));
    if (!data?.value) continue;
    // Filter to builds after merge date
    const builds = data.value.filter(b => {
      if (afterDate && b.finishTime && b.finishTime < afterDate) return false;
      return true;
    });
    // Prefer succeeded, then any
    const succeeded = builds.find(b => b.result === 'succeeded');
    if (succeeded) return succeeded;
    if (builds.length > 0) return builds[0];
  }
  return null;
}

function getReleaseStage(buildId) {
  const data = readJson(path.join(DATA_DIR, `timeline-${buildId}.json`));
  if (!data?.records) return null;
  const stage = data.records.find(r =>
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

// Process each sample file
const sampleFiles = fs.readdirSync(SAMPLE_DIR)
  .filter(f => f.startsWith('sample-') && f.endsWith('.json'))
  .sort();

let totalUpdated = 0;

for (const file of sampleFiles) {
  const filepath = path.join(SAMPLE_DIR, file);
  const data = JSON.parse(fs.readFileSync(filepath, 'utf-8'));
  const name = file.replace('sample-', '').replace('.json', '');

  let updated = false;

  for (const pr of data.sdkPRs) {
    if (!pr.mergedAt || pr.release) continue;

    const svc = inferServiceName(pr.title);
    if (!svc) {
      console.log(`  ⚠️  ${name}/${pr.language}: can't infer service from: ${pr.title.substring(0, 60)}`);
      continue;
    }

    const pipeline = findDefId(pr.language, svc);
    if (!pipeline) {
      console.log(`  ❌ ${name}/${pr.language}: no pipeline for ${svc}`);
      continue;
    }

    const build = findReleaseBuild(pipeline.defId, pr.mergedAt);
    if (!build) {
      console.log(`  ❌ ${name}/${pr.language}: no release build for ${pipeline.name} after ${pr.mergedAt.substring(0, 10)}`);
      // Add pending release
      const pkgFn = PACKAGE_PATTERNS[pr.language];
      pr.release = {
        packageName: pkgFn ? pkgFn(svc) : null,
        pipelineName: pipeline.name,
        pipelineUrl: null,
        buildId: null,
        releasedAt: null,
        releaseGapDays: null,
        status: 'pending'
      };
      pr.events.push({
        type: 'release_pending',
        timestamp: pr.mergedAt,
        actor: 'system',
        actorRole: 'bot',
        description: `Package release pending: ${pipeline.name}`,
        sentiment: 'neutral',
        details: {}
      });
      updated = true;
      continue;
    }

    const stage = getReleaseStage(build.id);
    const buildUrl = `https://dev.azure.com/azure-sdk/internal/_build/results?buildId=${build.id}`;
    const pkgFn = PACKAGE_PATTERNS[pr.language];
    const packageName = pkgFn ? pkgFn(svc) : null;

    if (stage?.result === 'succeeded' && stage.finishTime) {
      const mergeMs = new Date(pr.mergedAt).getTime();
      const relMs = new Date(stage.finishTime).getTime();
      const gapDays = parseFloat(((relMs - mergeMs) / (1000 * 60 * 60 * 24)).toFixed(1));

      pr.release = {
        packageName,
        packageVersion: null,
        pipelineName: pipeline.name,
        pipelineUrl: buildUrl,
        buildId: String(build.id),
        releasedAt: stage.finishTime,
        releaseGapDays: gapDays,
        status: 'released'
      };

      pr.events.push({
        type: 'release_pipeline_started',
        timestamp: stage.startTime || stage.finishTime,
        actor: 'azure-pipelines[bot]',
        actorRole: 'bot',
        description: `Release pipeline started: ${pipeline.name}`,
        sentiment: 'neutral',
        details: { url: buildUrl, body: `Build ${build.id}` }
      });
      pr.events.push({
        type: 'release_pipeline_completed',
        timestamp: stage.finishTime,
        actor: 'azure-pipelines[bot]',
        actorRole: 'bot',
        description: `Package released: ${packageName}`,
        sentiment: 'positive',
        details: { url: buildUrl, body: `Build ${build.id} succeeded` }
      });

      console.log(`  ✅ ${name}/${pr.language}: released ${stage.finishTime.substring(0, 10)} (${gapDays}d gap)`);
      updated = true;
    } else if (stage?.result === 'failed') {
      pr.release = {
        packageName,
        pipelineName: pipeline.name,
        pipelineUrl: buildUrl,
        buildId: String(build.id),
        releasedAt: null,
        releaseGapDays: null,
        status: 'failed'
      };
      pr.events.push({
        type: 'release_pipeline_started',
        timestamp: stage.startTime || build.finishTime,
        actor: 'azure-pipelines[bot]',
        actorRole: 'bot',
        description: `Release pipeline started: ${pipeline.name}`,
        sentiment: 'neutral',
        details: { url: buildUrl, body: `Build ${build.id}` }
      });
      pr.events.push({
        type: 'release_pipeline_failed',
        timestamp: stage.finishTime || build.finishTime,
        actor: 'azure-pipelines[bot]',
        actorRole: 'bot',
        description: `Release pipeline failed: ${pipeline.name}`,
        sentiment: 'negative',
        details: { url: buildUrl, body: `Build ${build.id} failed` }
      });
      console.log(`  ⚠️  ${name}/${pr.language}: release FAILED build ${build.id}`);
      updated = true;
    } else if (stage?.result === 'skipped') {
      pr.release = {
        packageName,
        pipelineName: pipeline.name,
        pipelineUrl: buildUrl,
        buildId: String(build.id),
        releasedAt: null,
        releaseGapDays: null,
        status: 'pending'
      };
      pr.events.push({
        type: 'release_pending',
        timestamp: pr.mergedAt,
        actor: 'system',
        actorRole: 'bot',
        description: `Release skipped in pipeline: ${pipeline.name}`,
        sentiment: 'neutral',
        details: { url: buildUrl }
      });
      console.log(`  ⏳ ${name}/${pr.language}: release skipped in build ${build.id}`);
      updated = true;
    } else {
      console.log(`  ❓ ${name}/${pr.language}: build ${build.id} has no release stage`);
    }
  }

  if (updated) {
    // Re-sort events for each PR
    for (const pr of data.sdkPRs) {
      pr.events.sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));
    }

    // Update endDate if releases extend it
    const allDates = data.sdkPRs
      .map(p => p.release?.releasedAt)
      .filter(Boolean);
    if (allDates.length) {
      const latest = allDates.sort().pop();
      if (latest > data.endDate) data.endDate = latest;
    }

    // Update summary
    const released = data.sdkPRs.filter(p => p.release?.status === 'released');
    const gaps = released.map(p => p.release.releaseGapDays).filter(g => g != null);
    if (gaps.length) {
      data.summary.avgReleaseGapDays = parseFloat((gaps.reduce((a, b) => a + b, 0) / gaps.length).toFixed(1));
      data.summary.maxReleaseGapDays = parseFloat(Math.max(...gaps).toFixed(1));
    }
    const pending = data.sdkPRs.filter(p => p.release?.status === 'pending' || (p.mergedAt && !p.release));
    data.summary.pendingReleases = pending.length;
    data.summary.totalDurationDays = parseFloat(
      ((new Date(data.endDate) - new Date(data.startDate)) / (1000 * 60 * 60 * 24)).toFixed(1)
    );

    // Update insights
    data.insights = data.insights.filter(i => i.type !== 'release_pending' && i.type !== 'release_delay');
    if (released.length === data.sdkPRs.filter(p => p.mergedAt).length && released.length > 0) {
      data.insights.push({
        type: 'summary', severity: 'info',
        description: `All ${released.length} SDK packages released. Avg gap: ${data.summary.avgReleaseGapDays}d.`,
        durationDays: data.summary.avgReleaseGapDays
      });
    }
    if (pending.length > 0) {
      data.insights.push({
        type: 'release_pending', severity: 'critical',
        description: `${pending.length} SDK package(s) still pending release.`
      });
    }
    if (data.summary.maxReleaseGapDays > 3) {
      const slowest = released.reduce((a, b) =>
        (a.release?.releaseGapDays || 0) > (b.release?.releaseGapDays || 0) ? a : b
      );
      data.insights.push({
        type: 'release_delay', severity: 'warning',
        description: `${slowest.language} had longest release gap: ${slowest.release.releaseGapDays}d.`,
        durationDays: slowest.release.releaseGapDays,
        prRef: `${slowest.repo}#${slowest.number}`
      });
    }

    fs.writeFileSync(filepath, JSON.stringify(data, null, 2));
    totalUpdated++;
    console.log(`  📝 Saved ${file}`);
  }
}

console.log(`\nDone! Updated ${totalUpdated} dataset(s).`);
