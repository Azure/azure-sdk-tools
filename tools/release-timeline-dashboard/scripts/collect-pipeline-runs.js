#!/usr/bin/env node

const { existsSync } = require("node:fs");
const {
  adoRequest,
  concurrentMap,
  parseArgs,
  parsePipelineUrl,
  readJson,
  writeJson,
} = require("./lib/v2-common");

const args = parseArgs(process.argv.slice(2), {
  input: "cache/v2/release-plans.json",
  output: "cache/v2/pipeline-runs.json",
  concurrency: 6,
});

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});

async function main() {
  const source = readJson(args.input);
  const previous = existsSync(args.output) ? readJson(args.output) : null;
  const cachedRuns = new Map(
    (previous?.runs || []).map((run) => [run.id, run]),
  );
  const references = new Map();
  for (const plan of source.plans) {
    for (const language of plan.languages) {
      addReference(
        references,
        language.generationPipelineUrl,
        plan.id,
        language.id,
        "generation",
      );
      addReference(
        references,
        language.releasePipelineUrl,
        plan.id,
        language.id,
        "release",
      );
    }
  }
  let cacheHits = 0;
  const results = await concurrentMap(
    [...references.values()],
    args.concurrency,
    async (reference) => {
      const cached = cachedRuns.get(reference.id);
      if (cached?.finishAt) {
        cacheHits++;
        return {
          run: { ...cached, references: reference.references },
          skipped: null,
        };
      }
      try {
        return { run: await collectRun(reference), skipped: null };
      } catch (error) {
        if (/Azure DevOps (401|403)/.test(error.message)) throw error;
        return {
          run: unavailableRun(reference, error.message),
          skipped: {
            id: reference.id,
            url: reference.url,
            reason: error.message.slice(0, 300),
          },
        };
      }
    },
  );
  const runs = results.map((result) => result.run);
  const skippedRuns = results.map((result) => result.skipped).filter(Boolean);
  writeJson(args.output, {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    runCount: runs.length,
    cacheHits,
    skippedRunCount: skippedRuns.length,
    skippedRuns,
    runs,
  });
  console.log(
    `Collected ${runs.length} pipeline runs (${cacheHits} cached, ${skippedRuns.length} skipped) into ${args.output}`,
  );
}

function unavailableRun(reference, reason) {
  return {
    ...reference,
    name: null,
    definition: null,
    status: "unavailable",
    result: null,
    queueAt: null,
    startAt: null,
    finishAt: null,
    failures: [],
    collectionError: reason.slice(0, 300),
  };
}

function addReference(map, rawUrl, planId, language, role) {
  if (!rawUrl) return;
  const parsed = parsePipelineUrl(rawUrl);
  if (!parsed) return;
  if (!map.has(parsed.id)) map.set(parsed.id, { ...parsed, references: [] });
  map.get(parsed.id).references.push({ planId, language, role });
}

async function collectRun(reference) {
  const build = await adoRequest(
    `/${reference.project}/_apis/build/builds/${reference.buildId}?api-version=7.1`,
  );
  const timeline = await adoRequest(
    `/${reference.project}/_apis/build/builds/${reference.buildId}/timeline?api-version=7.1`,
  );
  return {
    ...reference,
    name: build.buildNumber,
    definition: build.definition?.name || null,
    status: build.status,
    result: build.result || null,
    queueAt: build.queueTime || null,
    startAt: build.startTime || null,
    finishAt: build.finishTime || null,
    failures: (timeline.records || [])
      .filter(
        (record) =>
          ["failed", "partiallySucceeded"].includes(record.result) &&
          ["Stage", "Job", "Task"].includes(record.type),
      )
      .map((record) => ({
        id: record.id,
        name: record.name,
        type: record.type.toLowerCase(),
        result: record.result,
        startAt: record.startTime || null,
        finishAt: record.finishTime || null,
      })),
  };
}
