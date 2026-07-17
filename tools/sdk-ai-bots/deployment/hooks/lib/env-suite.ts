/**
 * env-suite — minimal reader for infra/environments/environment-suite.yaml.
 *
 * The suite file is the single source of truth for per-environment metadata
 * (subscription, resource group prefix, Teams app id, ...). Hooks read values
 * from it via `getEnvSuiteValue(envName, key)`; the pipeline reads the same
 * file via `yq` in pipelines/templates/load-environment-suite.yml.
 *
 * Uses `yq` when available (matches the pipeline path). Otherwise falls back
 * to a small regex parser that handles the flat scalar fields under each
 * `environments.<env>:` block — enough for the keys hooks currently need.
 * Adds no runtime dependencies.
 */

import { execFileSync, execSync } from "child_process";
import { readFileSync, existsSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

// hooks/lib/env-suite.ts → ../../infra/environments/environment-suite.yaml
const DEFAULT_SUITE_PATH = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../infra/environments/environment-suite.yaml",
);

let yqChecked = false;
let yqAvailable = false;

function hasYq(): boolean {
  if (yqChecked) return yqAvailable;
  yqChecked = true;
  const lookup = process.platform === "win32" ? "where" : "command -v";
  try {
    execSync(`${lookup} yq`, { stdio: "ignore" });
    yqAvailable = true;
  } catch {
    yqAvailable = false;
  }
  return yqAvailable;
}

/**
 * Read a scalar field for `envName` from environment-suite.yaml. Returns
 * `undefined` when the field is missing or the file is absent. Never throws
 * for missing fields — callers decide whether the value is required.
 */
export function getEnvSuiteValue(
  envName: string,
  key: string,
  suitePath: string = DEFAULT_SUITE_PATH,
): string | undefined {
  if (!existsSync(suitePath)) return undefined;

  if (hasYq()) {
    const raw = execFileSync(
      "yq",
      ["-r", `.environments.${envName}.${key} // ""`, suitePath],
      { encoding: "utf8" },
    ).trim();
    return raw === "" || raw === "null" ? undefined : raw;
  }

  // Fallback: locate the `<envName>:` block and grep the flat `key: value`
  // line at its top-level indentation. Handles single-quoted, double-quoted,
  // and unquoted scalars. Sufficient for the flat scalar fields the hooks
  // currently need (subscriptionId, resourceGroupPrefix, teamsAppId, ...).
  const text = readFileSync(suitePath, "utf8");
  const envHeader = new RegExp(`^(\\s+)${envName}:\\s*$`, "m");
  const headerMatch = envHeader.exec(text);
  if (!headerMatch) return undefined;
  const indent = headerMatch[1].length + 4; // block members are one level deeper
  // Slice from just after the env header until the next line at the same or
  // shallower indent (i.e. the next sibling env or top-level block).
  const startIdx = headerMatch.index + headerMatch[0].length + 1;
  const rest = text.slice(startIdx);
  const siblingRe = new RegExp(`\\n {0,${indent - 1}}[^\\s#][^\\n]*`, "");
  const siblingMatch = siblingRe.exec("\n" + rest);
  const block = siblingMatch ? rest.slice(0, siblingMatch.index) : rest;
  const fieldRe = new RegExp(`^ {${indent}}${key}:\\s*(.*)$`, "m");
  const fieldMatch = fieldRe.exec(block);
  if (!fieldMatch) return undefined;
  let value = fieldMatch[1].trim();
  // Strip trailing inline comment.
  value = value.replace(/\s+#.*$/, "").trim();
  // Strip matching quotes.
  if (
    (value.startsWith("'") && value.endsWith("'")) ||
    (value.startsWith('"') && value.endsWith('"'))
  ) {
    value = value.slice(1, -1);
  }
  return value === "" ? undefined : value;
}
