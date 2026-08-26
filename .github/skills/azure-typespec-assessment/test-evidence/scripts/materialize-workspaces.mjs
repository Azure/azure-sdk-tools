#!/usr/bin/env node

import { existsSync, mkdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const evidenceRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function git(cwd, args) {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

function parseArgs(argv) {
  const options = {
    case: "all",
    output: join(evidenceRoot, ".workspaces"),
  };
  for (let index = 0; index < argv.length; index += 1) {
    if (argv[index] === "--repository") {
      options.repository = resolve(argv[++index]);
    } else if (argv[index] === "--case") {
      options.case = argv[++index];
    } else if (argv[index] === "--output") {
      options.output = resolve(argv[++index]);
    } else {
      throw new Error(`Unknown argument: ${argv[index]}`);
    }
  }
  if (!options.repository) throw new Error("--repository is required");
  return options;
}

export function materializeWorkspaces(options) {
  const repository = resolve(options.repository);
  if (!existsSync(join(repository, ".git"))) {
    throw new Error(`Not a Git repository: ${repository}`);
  }
  const cases = JSON.parse(
    readFileSync(join(evidenceRoot, "cases.json"), "utf8"),
  ).filter(
    ({ pr }) =>
      options.case === undefined ||
      options.case === "all" ||
      String(pr) === String(options.case),
  );
  if (cases.length === 0) throw new Error(`Unknown case: ${options.case}`);
  mkdirSync(options.output, { recursive: true });

  for (const testCase of cases) {
    for (const commit of [testCase.baseCommit, testCase.headCommit]) {
      try {
        git(repository, ["cat-file", "-e", `${commit}^{commit}`]);
      } catch {
        throw new Error(
          `Commit ${commit} for PR ${testCase.pr} is unavailable in ${repository}`,
        );
      }
    }
    const destination = join(options.output, String(testCase.pr));
    if (!existsSync(destination)) {
      git(repository, [
        "clone",
        "--shared",
        "--no-checkout",
        repository,
        destination,
      ]);
      git(destination, ["checkout", "--detach", testCase.headCommit]);
    }
    const head = git(destination, ["rev-parse", "HEAD"]);
    if (head !== testCase.headCommit) {
      throw new Error(
        `${destination} is at ${head}; expected ${testCase.headCommit}`,
      );
    }
    process.stdout.write(`PR ${testCase.pr}: ${destination}\n`);
  }
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  try {
    materializeWorkspaces(parseArgs(process.argv.slice(2)));
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
