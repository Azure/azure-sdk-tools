import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { getVallyShardVerdict } from "../../../../../eng/common/scripts/eval/lib/verdict.ts";

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const SHARED_EVAL_SCRIPT_DIR = path.resolve(SCRIPT_DIR, "../../../../../eng/common/scripts/eval");

function normalizeExtraArgs(extraArgs) {
  if (!extraArgs || extraArgs === "$(TypeSpecAuthorEvalExtraArgs)") {
    return [];
  }

  return extraArgs.split(/\s+/).filter(Boolean);
}

export function runShard({ evalArgs, shardName, outputDir, threshold = 0.8, extraArgs = "" }) {
  const evalArgList = evalArgs.split(/\s+/).filter(Boolean);
  const thresholdArg = String(threshold);
  const extraArgList = normalizeExtraArgs(extraArgs);

  console.log(
    `Running: vally eval ${evalArgs} --junit --threshold ${thresholdArg} --output-dir "${outputDir}" ${extraArgList.join(" ")}`
  );

  const proc = spawnSync(
    "npm",
    [
      "exec",
      "--no",
      "--prefix",
      SHARED_EVAL_SCRIPT_DIR,
      "--",
      "vally",
      "eval",
      ...evalArgList,
      "--junit",
      "--threshold",
      thresholdArg,
      "--output-dir",
      outputDir,
      ...extraArgList,
    ],
    { stdio: "inherit", shell: process.platform === "win32" }
  );
  const vallyExit = proc.status ?? 1;

  const verdict = getVallyShardVerdict({ resultsDir: outputDir, threshold });
  for (const line of verdict.lines) {
    console.log(`  ${line}`);
  }

  if (!verdict.found) {
    console.log(
      `##vso[task.logissue type=error]Shard '${shardName}' produced no usable verdict (vally exit ${vallyExit}). Treating as failure.`
    );
    return 1;
  }

  if (verdict.passed) {
    if (verdict.hadExecutionErrors) {
      console.log(
        `Shard '${shardName}' passed the pass-rate threshold; vally flagged execution errors (post-run teardown noise, not blocking).`
      );
    }
    if (vallyExit !== 0) {
      console.log(
        `vally exited ${vallyExit} during post-run shutdown; shard '${shardName}' is PASSED per results.jsonl (exit code ignored).`
      );
    }
    console.log(`##[section]Shard '${shardName}' PASSED (verdict from results.jsonl).`);
    return 0;
  }

  console.log(
    `##vso[task.logissue type=error]Shard '${shardName}' FAILED - one or more evals are below the pass-rate threshold.`
  );
  return 1;
}

function parseArgs(argv) {
  const options = { threshold: 0.8, extraArgs: "" };
  for (let i = 0; i < argv.length; i++) {
    const next = () => argv[++i];
    switch (argv[i]) {
      case "--eval-args":
        options.evalArgs = next();
        break;
      case "--shard-name":
        options.shardName = next();
        break;
      case "--output-dir":
        options.outputDir = next();
        break;
      case "--threshold":
        options.threshold = Number(next());
        break;
      case "--extra-args":
        options.extraArgs = next();
        break;
      default:
        throw new Error(`Unknown argument: ${argv[i]}`);
    }
  }

  for (const required of ["evalArgs", "shardName", "outputDir"]) {
    if (!options[required]) {
      throw new Error(`Missing required argument for ${required}.`);
    }
  }

  return options;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    process.exit(runShard(parseArgs(process.argv.slice(2))));
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
}