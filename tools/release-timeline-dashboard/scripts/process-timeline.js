#!/usr/bin/env node
/**
 * process-timeline.js
 *
 * Processes raw fetch-timeline.js output into a final timeline JSON
 * suitable for the timeline-viz website.
 *
 * Usage: node scripts/process-timeline.js <raw-json> <output-json> <title>
 *
 * The raw JSON is produced by fetch-timeline.js and contains `_raw` fields
 * with comments, reviews, reviewComments, and commits arrays. This script
 * classifies events, detects nags/manual fixes, computes idle gaps,
 * and generates insights.
 */

const fs = require('fs');
const path = require('path');

const {
  daysBetween,
  computeReviewWaitDays,
  processEvents,
  generateInsights
} = require('./lib/event-processor');

const ALL_LANGUAGES = {
  Java:       'Azure/azure-sdk-for-java',
  Go:         'Azure/azure-sdk-for-go',
  Python:     'Azure/azure-sdk-for-python',
  '.NET':     'Azure/azure-sdk-for-net',
  JavaScript: 'Azure/azure-sdk-for-js'
};

function main() {
  const args = process.argv.slice(2);
  if (args.length < 3) {
    console.error('Usage: node process-timeline.js <raw-json> <output-json> <title>');
    process.exit(1);
  }

  const [rawFile, outFile, title] = args;
  const data = JSON.parse(fs.readFileSync(rawFile, 'utf8'));
  const spec = data.specPR;
  const sdks = data.sdkPRs;
  const owner = spec.author;

  // Process spec PR events
  const specOut = { ...spec };
  delete specOut._raw;
  specOut.events = processEvents(spec, owner);

  // Compute review wait for spec PR
  const specWait = computeReviewWaitDays(specOut.events, specOut.readyForReviewAt || null);
  specOut.reviewWaitDays = specWait.reviewWaitDays;
  specOut.reviewWaitCycles = specWait.reviewWaitCycles;

  // Process SDK PR events
  const sdkOuts = sdks.map(pr => {
    const out = { ...pr };
    delete out._raw;
    out.events = processEvents(pr, owner);
    const w = computeReviewWaitDays(out.events, out.readyForReviewAt || null);
    out.reviewWaitDays = w.reviewWaitDays;
    out.reviewWaitCycles = w.reviewWaitCycles;
    return out;
  });

  // Add missing language placeholders
  const presentLangs = new Set(sdkOuts.map(pr => pr.language));
  for (const [lang, repo] of Object.entries(ALL_LANGUAGES)) {
    if (!presentLangs.has(lang)) {
      sdkOuts.push({
        repo, language: lang, number: null, url: null,
        title: 'No SDK PR generated', author: null,
        createdAt: null, mergedAt: null,
        state: 'missing', generationFlow: null, events: []
      });
    }
  }

  // Collect all timestamps
  const allTs = [];
  for (const e of specOut.events) {
    allTs.push(e.timestamp);
    if (e.endTimestamp) allTs.push(e.endTimestamp);
  }
  for (const pr of sdkOuts) {
    for (const e of pr.events) {
      allTs.push(e.timestamp);
      if (e.endTimestamp) allTs.push(e.endTimestamp);
    }
  }
  allTs.sort();
  const startDate = allTs[0] || spec.createdAt;
  const endDate = allTs[allTs.length - 1] || spec.mergedAt || spec.createdAt;

  // Generate insights and summary
  const { insights, pipeGap, specDays, durations, nags, slowest, fastest } = 
    generateInsights(specOut, sdkOuts, spec);

  const manuals = sdkOuts.reduce((n, pr) =>
    n + pr.events.filter(e => e.type === 'manual_fix').length, 0);
  const reviewers = new Set();
  for (const pr of [specOut, ...sdkOuts]) {
    for (const e of pr.events) {
      if (e.actorRole === 'reviewer') reviewers.add(e.actor);
    }
  }

  // Aggregate review wait across all PRs
  const allPRsForWait = [specOut, ...sdkOuts.filter(p => p.state !== 'missing')];
  const totalReviewWaitDays = allPRsForWait.reduce((sum, pr) => sum + (pr.reviewWaitDays || 0), 0);
  const totalReviewWaitCycles = allPRsForWait.reduce((sum, pr) => sum + (pr.reviewWaitCycles || 0), 0);

  // Check if any PRs had draft phases (ready_for_review events exist)
  const hasDraftPRs = allPRsForWait.some(pr =>
    pr.readyForReviewAt || pr.events.some(e => e.type === 'ready_for_review')
  );

  const totalDays = daysBetween(startDate, endDate);

  const output = {
    title, owner, startDate, endDate,
    specPR: specOut,
    sdkPRs: sdkOuts,
    insights,
    summary: {
      totalDurationDays: totalDays != null ? Math.round(totalDays * 100) / 100 : null,
      specPRDays: specDays != null ? Math.round(specDays * 100) / 100 : null,
      pipelineGapDays: pipeGap != null ? Math.round(pipeGap * 100) / 100 : null,
      sdkPRMaxDays: slowest ? Math.round(slowest.days * 100) / 100 : null,
      fastestSDKPR: fastest ? { language: fastest.language, days: Math.round(fastest.days * 100) / 100 } : null,
      slowestSDKPR: slowest ? { language: slowest.language, days: Math.round(slowest.days * 100) / 100 } : null,
      totalUniqueReviewers: reviewers.size,
      totalNags: nags,
      totalManualFixes: manuals,
      totalPREdits: 0,
      totalReviewWaitDays: Math.round(totalReviewWaitDays * 100) / 100,
      totalReviewWaitCycles,
      hasDraftPRs
    }
  };

  fs.writeFileSync(outFile, JSON.stringify(output, null, 2));
  console.log(`✅ ${outFile} — spec=${specOut.events.length} events, ${sdkOuts.length} SDK PRs, ${insights.length} insights`);
  for (const pr of sdkOuts) {
    const num = pr.number || '—';
    console.log(`   ${(pr.language || '?').padEnd(12)} #${String(num).padStart(6)} ${(pr.state || '?').padEnd(8)} ${pr.events.length} events`);
  }
}

main();
