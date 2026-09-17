import { createReadStream } from "node:fs";
import { access, copyFile, mkdir, mkdtemp, open, readdir, readFile, rm, writeFile } from "node:fs/promises";
import { createInterface } from "node:readline";
import { join, resolve, sep } from "node:path";
import { tmpdir } from "node:os";
import { createDashboardBundle, readDashboardBundleManifest } from "./dashboard-bundle.js";
import { validateSubmissionManifest } from "./submission-manifest.js";

export function pipelineManifest(env) {
  const organization = new URL(env.SYSTEM_COLLECTIONURI || "https://invalid.example");
  if (organization.protocol !== "https:" || organization.hostname !== "dev.azure.com" || !/^\/[^/]+\/?$/.test(organization.pathname)) {
    throw new Error("SYSTEM_COLLECTIONURI must identify the Azure DevOps organization at dev.azure.com.");
  }
  const manifest = {
    schemaVersion: 1, adoOrganization: organization.pathname.split("/")[1], adoProject: env.SYSTEM_TEAMPROJECT,
    repo: env.BUILD_REPOSITORY_NAME, pipeline: env.BUILD_DEFINITIONNAME,
    pipelineDefinitionId: env.SYSTEM_DEFINITIONID, buildId: env.BUILD_BUILDID,
    summaryAttempt: Number(env.SYSTEM_JOBATTEMPT), branch: env.BUILD_SOURCEBRANCH,
    sourceVersion: env.BUILD_SOURCEVERSION, runTimestamp: new Date().toISOString(),
  };
  validateSubmissionManifest(manifest);
  return manifest;
}

export function selectShardAttempts(source) {
  if (!source || source.schemaVersion !== 1 || !Array.isArray(source.expectedShards) || !Array.isArray(source.attempts) || !source.expectedShards.length) {
    throw new Error("Shard input requires schemaVersion 1, expectedShards, and attempts.");
  }
  const expected = new Set(source.expectedShards);
  if (expected.size !== source.expectedShards.length || [...expected].some((name) => typeof name !== "string" || !name.trim())) {
    throw new Error("Expected shard names must be unique non-empty strings.");
  }
  const selected = new Map();
  const seen = new Set();
  for (const attempt of source.attempts) {
    if (!expected.has(attempt.shard) || !Number.isSafeInteger(attempt.attempt) || attempt.attempt < 1 ||
        typeof attempt.directory !== "string" || !attempt.directory) throw new Error("Invalid shard attempt.");
    const key = JSON.stringify([attempt.shard, attempt.attempt]);
    if (seen.has(key)) throw new Error(`Duplicate shard attempt: ${attempt.shard}.`);
    seen.add(key);
    if (!selected.has(attempt.shard) || selected.get(attempt.shard).attempt < attempt.attempt) selected.set(attempt.shard, attempt);
  }
  for (const shard of expected) if (!selected.has(shard)) throw new Error(`Missing expected shard: ${shard}.`);
  return [...selected.values()].sort((a, b) => a.shard.localeCompare(b.shard));
}

export async function preparePipelineBundle({ shardInput, resultsRoot, manifest, outputPath }) {
  validateSubmissionManifest(manifest);
  const selected = selectShardAttempts(shardInput);
  const root = resolve(resultsRoot);
  const temporary = await mkdtemp(join(tmpdir(), "vally-merge-"));
  try {
    await mkdir(join(temporary, "junit"));
    const output = await open(join(temporary, "results.jsonl"), "wx");
    let bytes = 0, trials = 0;
    try {
      for (const [index, attempt] of selected.entries()) {
        const directory = resolve(root, attempt.directory);
        if (!directory.startsWith(root + sep)) throw new Error("Shard directory must remain inside resultsRoot.");
        await access(join(directory, "results.jsonl"));
        const files = await readdir(join(directory, "junit"));
        const xml = files.filter((name) => name.endsWith(".xml")).sort();
        if (!xml.length) throw new Error(`Shard ${attempt.shard} has no JUnit results.`);
        for (const [number, name] of xml.entries()) await copyFile(join(directory, "junit", name), join(temporary, "junit", `${index}-${number}.xml`));
        const input = createReadStream(join(directory, "results.jsonl"), { encoding: "utf8" });
        const lines = createInterface({ input, crlfDelay: Infinity });
        let shardTrials = 0;
        try {
          for await (const line of lines) {
            if (!line.trim()) continue;
            const record = JSON.parse(line);
            if (!record || typeof record !== "object" || Array.isArray(record)) throw new Error("Invalid shard JSONL.");
            if (["log", "run-summary"].includes(record.type)) continue;
            if ((record.type !== undefined && record.type !== "trial-result") || record.experiment || !["success", "error"].includes(record.status)) {
              throw new Error("Only complete plain-eval trial records may be merged.");
            }
            const text = JSON.stringify(record) + "\n";
            bytes += Buffer.byteLength(text);
            if (bytes > 120 * 1024 * 1024) throw new Error("Merged results exceed the bundle expansion budget.");
            await output.writeFile(text); shardTrials++; trials++;
          }
        } finally { lines.close(); input.destroy(); }
        if (!shardTrials) throw new Error(`Shard ${attempt.shard} contains no trials.`);
      }
    } finally { await output.close(); }
    await writeFile(join(temporary, "eval-summary.md"), `# Eval result publication\n\n${selected.length} complete shards; ${trials} trial records.\n`);
    await createDashboardBundle({ inputDirectory: temporary, outputPath, manifest });
    await readDashboardBundleManifest(outputPath);
    return { outputPath, shards: selected.length, trials };
  } finally { await rm(temporary, { recursive: true, force: true }); }
}

export async function loadShardInput(path) { return JSON.parse(await readFile(path, "utf8")); }