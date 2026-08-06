/**
 * env-suite — minimal reader for infra/environments/environment-suite.yaml.
 *
 * The suite file is the single source of truth for per-environment metadata
 * (subscription, resource group prefix, Teams app id, ...). Hooks read values
 * from it via `getEnvSuiteValue(envName, key)`; the pipeline reads the same
 * file via `yq` in pipelines/templates/load-environment-suite.yml.
 *
 * Uses `yq` when available (matches the pipeline path). Otherwise falls back
 * to a small indentation-aware reader for scalar and string-array fields.
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

function getEnvironmentBlock(text: string, envName: string): { block: string; fieldIndent: number } | undefined {
  const lines = text.split(/\r?\n/);
  const headerPattern = new RegExp(`^(\\s*)${envName}:\\s*$`);
  const headerIndex = lines.findIndex((line) => headerPattern.test(line));
  if (headerIndex < 0) return undefined;

  const headerMatch = headerPattern.exec(lines[headerIndex]);
  const headerIndent = headerMatch?.[1].length ?? 0;
  let endIndex = lines.length;
  for (let index = headerIndex + 1; index < lines.length; index++) {
    const line = lines[index];
    if (!line.trim() || line.trimStart().startsWith("#")) continue;
    const indent = line.length - line.trimStart().length;
    if (indent <= headerIndent) {
      endIndex = index;
      break;
    }
  }
  return {
    block: lines.slice(headerIndex + 1, endIndex).join("\n"),
    fieldIndent: headerIndent + 4,
  };
}

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
  const environment = getEnvironmentBlock(readFileSync(suitePath, "utf8"), envName);
  if (!environment) return undefined;
  const fieldRe = new RegExp(`^ {${environment.fieldIndent}}${key}:\\s*(.*)$`, "m");
  const fieldMatch = fieldRe.exec(environment.block);
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

/** Read a string-array field for `envName` from environment-suite.yaml. */
export function getEnvSuiteValues(
  envName: string,
  key: string,
  suitePath: string = DEFAULT_SUITE_PATH,
): string[] {
  if (!existsSync(suitePath)) return [];

  if (hasYq()) {
    return execFileSync(
      "yq",
      ["-r", `.environments.${envName}.${key}[]? // ""`, suitePath],
      { encoding: "utf8" },
    )
      .split(/\r?\n/)
      .map((value) => value.trim())
      .filter(Boolean);
  }

  const environment = getEnvironmentBlock(readFileSync(suitePath, "utf8"), envName);
  if (!environment) return [];
  const fieldRe = new RegExp(`^ {${environment.fieldIndent}}${key}:\\s*$`, "m");
  const fieldMatch = fieldRe.exec(environment.block);
  if (!fieldMatch) return [];
  const arrayRest = environment.block.slice(fieldMatch.index + fieldMatch[0].length + 1);
  const itemIndent = environment.fieldIndent + 4;
  const values: string[] = [];
  for (const line of arrayRest.split(/\r?\n/)) {
    const itemMatch = new RegExp(`^ {${itemIndent}}-\\s*(.+)$`).exec(line);
    if (!itemMatch) break;
    let value = itemMatch[1].replace(/\s+#.*$/, "").trim();
    if (
      (value.startsWith("'") && value.endsWith("'")) ||
      (value.startsWith('"') && value.endsWith('"'))
    ) {
      value = value.slice(1, -1);
    }
    if (value) values.push(value);
  }
  return values;
}
