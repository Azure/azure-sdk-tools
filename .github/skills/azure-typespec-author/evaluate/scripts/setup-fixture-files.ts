#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { parseArgs } from "node:util";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "../../../../..");
export const fixtureDependencies = [
  "@azure-tools/typespec-autorest",
  "@azure-tools/typespec-azure-core",
  "@azure-tools/typespec-azure-resource-manager",
  "@azure-tools/typespec-azure-rulesets",
  "@azure-tools/typespec-client-generator-core",
  "@typespec/compiler",
  "@typespec/events",
  "@typespec/http",
  "@typespec/openapi",
  "@typespec/openapi3",
  "@typespec/rest",
  "@typespec/sse",
  "@typespec/streams",
  "@typespec/versioning",
];

export function prepareFixtureFiles(specRoot: string, fixtureRoot: string) {
  const instructionsSource = path.join(specRoot, ".github", "copilot-instructions.md");
  const workspaceFile = path.join(specRoot, "pnpm-workspace.yaml");
  const instructionsDestination = path.join(fixtureRoot, "instructions-test", "copilot-instructions.md");
  const fixturePackage = path.join(fixtureRoot, "Microsoft.Widget", "Widget", "package.json");
  for (const requiredFile of [instructionsSource, workspaceFile]) {
    if (!fs.existsSync(requiredFile)) {
      throw new Error(`Required fixture input does not exist: ${requiredFile}`);
    }
  }
  const catalog = new Map<string, string>();
  let inCatalog = false;
  for (const line of fs.readFileSync(workspaceFile, "utf8").split(/\r?\n/)) {
    if (line === "catalog:") {
      inCatalog = true;
      continue;
    }
    if (inCatalog && /^\S/.test(line)) {
      break;
    }
    if (!inCatalog) {
      continue;
    }
    const match = line.match(/^[ ]{2}"([^"]+)":\s*(\S+)\s*$/);
    if (match) {
      catalog.set(match[1], match[2]);
    }
  }
  const devDependencies: Record<string, string> = {};
  for (const dependency of fixtureDependencies) {
    // The specs workspace wraps this ruleset; standalone fixtures use its npm package.
    const version = catalog.get(dependency) ??
      (dependency === "@azure-tools/typespec-azure-rulesets"
        ? catalog.get("@azure-tools/typespec-azure-rulesets-original") : undefined);
    if (!version || /^(workspace|catalog):/.test(version)) {
      throw new Error(`Required dependency is missing from the pnpm catalog or is not installable: ${dependency}`);
    }
    devDependencies[dependency] = version;
  }
  fs.mkdirSync(path.dirname(fixturePackage), { recursive: true });
  fs.writeFileSync(fixturePackage, `${JSON.stringify({
    name: "microsoft-widget-eval-fixture",
    private: true,
    type: "module",
    devDependencies,
  }, null, 2)}\n`);
  console.log(`generated ${fixturePackage}`);
  fs.mkdirSync(path.dirname(instructionsDestination), { recursive: true });
  fs.copyFileSync(instructionsSource, instructionsDestination);
  console.log(`copied ${instructionsSource} -> ${instructionsDestination}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const { values } = parseArgs({ options: {
    "spec-root": { type: "string" },
    "fixture-root": { type: "string" },
  } });
  const specRoot = path.resolve(values["spec-root"] ?? path.join(repoRoot, "artifacts", "azure-rest-api-specs"));
  if (!values["spec-root"]) {
    const git = (...args: string[]) => execFileSync("git", args, { stdio: ["ignore", 2, "inherit"] });
    if (!fs.existsSync(path.join(specRoot, ".git"))) {
      fs.mkdirSync(path.dirname(specRoot), { recursive: true });
      git("clone", "--filter=blob:none", "--sparse", "--depth=1", "--branch", "main",
        "https://github.com/Azure/azure-rest-api-specs.git", specRoot);
      git("-C", specRoot, "sparse-checkout", "set", "--no-cone", "/*", "!/specification/");
    } else {
      git("-C", specRoot, "fetch", "--depth=1", "origin", "main");
      git("-C", specRoot, "checkout", "--detach", "FETCH_HEAD");
    }
  }
  prepareFixtureFiles(specRoot, path.resolve(values["fixture-root"] ?? path.join(scriptDir, "..", "fixtures")));
}
