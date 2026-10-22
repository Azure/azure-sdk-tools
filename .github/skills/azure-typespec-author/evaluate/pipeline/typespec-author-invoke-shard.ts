import { spawnSync } from "node:child_process";
import fs from "node:fs";
import { createRequire } from "node:module";
import os from "node:os";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { getPassAtKVerdict, setJunitPassAtKThreshold } from "./typespec-author-pass-at-k.ts";

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const SHARED_EVAL_SCRIPT_DIR = path.resolve(SCRIPT_DIR, "../../../../../eng/common/scripts/eval");
const requireEval = createRequire(path.join(SHARED_EVAL_SCRIPT_DIR, "package.json"));

type SkillEnvironment = {
  skills?: string[];
  mcpServers?: Record<string, { cwd?: string }>;
};

export function configureSkillEnvironments(
  environments: Record<string, SkillEnvironment>,
  evalRoot: string,
  noSkill: boolean,
  skillRoot: string
) {
  const author = path.resolve(evalRoot, "../SKILL.md");
  const requiresNaming = fs.readFileSync(author, "utf8").includes("azsdk-common-typespec-naming");
  for (const environment of Object.values(environments)) {
    if (environment.skills !== undefined) {
      environment.skills = noSkill ? [] : environment.skills.flatMap((source) => {
        const directory = path.resolve(evalRoot, "evals", source);
        if (fs.existsSync(path.join(directory, "SKILL.md"))) {
          const staged = path.join(skillRoot, path.basename(directory));
          if (!fs.existsSync(staged)) {
            fs.mkdirSync(staged, { recursive: true });
            for (const entry of ["SKILL.md", "references"]) {
              fs.cpSync(path.join(directory, entry), path.join(staged, entry), { recursive: true });
            }
          }
          return [staged];
        }
        if (path.basename(directory) === "azsdk-common-typespec-naming" && !requiresNaming) {
          console.log("Selected authoring skill predates naming; omitting the absent dependency.");
          return [];
        }
        throw new Error(`Missing declared benchmark skill: ${directory}`);
      });
    }
    for (const server of Object.values(environment.mcpServers ?? {})) {
      if (server.cwd) server.cwd = path.resolve(evalRoot, server.cwd);
    }
  }
}

type ShardRunOptions = {
  evalArgs: string;
  shardName: string;
  outputDir: string;
  extraArgs?: string;
  runs?: number;
};

function normalizeExtraArgs(extraArgs: string): string[] {
  if (!extraArgs || extraArgs === "$(TypeSpecAuthorEvalExtraArgs)") {
    return [];
  }

  return extraArgs.split(/\s+/).filter(Boolean);
}

export async function runShard({ evalArgs, shardName, outputDir, extraArgs = "", runs = 1 }: ShardRunOptions) {
  if (!Number.isInteger(runs) || runs < 1) {
    throw new Error("--runs must be a positive integer.");
  }

  const evalRoot = process.cwd();
  outputDir = path.resolve(outputDir);
  const evalPatterns = evalArgs.split(/\s+/).filter((arg) =>
    arg && !["-e", "--eval-spec"].includes(arg)
  ).map((arg) => path.resolve(evalRoot, arg));
  if (!evalPatterns.length) throw new Error("Provide at least one eval path or pattern.");
  const extraArgList = normalizeExtraArgs(extraArgs);
  const modes = extraArgList.filter((arg) => /^mode=(forced|trigger|no-skill)$/.test(arg));
  if (modes.length !== 1) {
    throw new Error("Run one benchmark mode per shard with --extra-args \"--tag mode=forced|trigger|no-skill\".");
  }
  const { loadProjectConfig, parseTagFilters } =
    await import(pathToFileURL(requireEval.resolve("@microsoft/vally")).href);
  const config = await loadProjectConfig(evalRoot);
  const tags: string[] = [];
  const forwardedArgs: string[] = [];
  for (let index = 0; index < extraArgList.length; index++) {
    if (extraArgList[index] === "--tag") {
      tags.push(extraArgList[++index]);
    } else {
      forwardedArgs.push(extraArgList[index]);
    }
  }
  const { dir: _dir, ...runtimeConfig } = config;
  // Vally 0.14 reloads the source config for explicit -e paths; a runtime suite preserves these mounts.
  runtimeConfig.suites = {
    shard: { evals: evalPatterns, filter: parseTagFilters(tags) },
  };

  console.log(
    `Running: vally eval ${evalArgs} --junit --output-dir "${outputDir}" --runs ${runs} ${extraArgList.join(" ")}`
  );

  const vallyArgs = [
    "exec",
    "--no",
    "--prefix",
    SHARED_EVAL_SCRIPT_DIR,
    "--",
    "vally",
    "eval",
    "--suite",
    "shard",
    "--junit",
    "--output-dir",
    outputDir,
    "--runs",
    String(runs),
    ...forwardedArgs,
  ];

  const runtimeDirectory = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-author-shard-"));
  let vallyExit: number;
  try {
    configureSkillEnvironments(
      config.environments, evalRoot, modes[0] === "mode=no-skill",
      path.join(runtimeDirectory, "skills")
    );
    if (modes[0] === "mode=no-skill") {
      const emptySkills = path.join(runtimeDirectory, "no-skills");
      fs.mkdirSync(emptySkills);
      vallyArgs.push("--skill-dir", emptySkills);
    }
    fs.writeFileSync(path.join(runtimeDirectory, ".vally.yaml"), JSON.stringify(runtimeConfig));
    const proc = spawnSync("npm", vallyArgs, {
      cwd: runtimeDirectory,
      stdio: "inherit",
      shell: process.platform === "win32",
      env: process.env,
    });
    if (proc.error) throw proc.error;
    vallyExit = proc.status ?? 1;
  } finally {
    fs.rmSync(runtimeDirectory, { recursive: true, force: true });
  }

  const updatedJunitFiles = setJunitPassAtKThreshold(outputDir, runs);
  console.log(`Configured ${updatedJunitFiles} JUnit file(s) for pass@${runs} summary gating.`);

  const verdict = getPassAtKVerdict(outputDir);
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
        `Shard '${shardName}' passed pass@${runs}; vally flagged execution errors (post-run teardown noise, not blocking).`
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
    `##vso[task.logissue type=error]Shard '${shardName}' FAILED - one or more stimuli have no passing trial.`
  );
  return 1;
}

function parseArgs(argv: string[]): ShardRunOptions {
  const options: Partial<ShardRunOptions> = { extraArgs: "" };
  for (let index = 0; index < argv.length; index++) {
    const next = () => argv[++index];
    switch (argv[index]) {
      case "--eval-args":
        options.evalArgs = next();
        break;
      case "--shard-name":
        options.shardName = next();
        break;
      case "--output-dir":
        options.outputDir = next();
        break;
      case "--extra-args":
        options.extraArgs = next();
        break;
      case "--runs":
        options.runs = Number(next());
        break;
      default:
        throw new Error(`Unknown argument: ${argv[index]}`);
    }
  }

  for (const required of ["evalArgs", "shardName", "outputDir"] as const) {
    if (!options[required]) {
      throw new Error(`Missing required argument for ${required}.`);
    }
  }

  return options as ShardRunOptions;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    process.exit(await runShard(parseArgs(process.argv.slice(2))));
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}