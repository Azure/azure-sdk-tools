import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

export function parseArgs(argv, options = {}) {
  const result = { ...options.defaults };
  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith("--")) {
      (result._ ??= []).push(token);
      continue;
    }
    const [rawName, inlineValue] = token.slice(2).split("=", 2);
    const name = rawName.replaceAll("-", "_");
    if (options.booleans?.includes(rawName)) {
      result[name] = inlineValue === undefined ? true : inlineValue !== "false";
      continue;
    }
    const value = inlineValue ?? argv[++index];
    if (value === undefined || value.startsWith("--")) {
      throw new Error(`Missing value for --${rawName}.`);
    }
    result[name] = value;
  }
  for (const required of options.required ?? []) {
    if (result[required.replaceAll("-", "_")] === undefined) {
      throw new Error(`Missing required argument --${required}.`);
    }
  }
  return result;
}

export function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

export function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
}

export function isMain(metaUrl) {
  return process.argv[1] && metaUrl === pathToFileURL(path.resolve(process.argv[1])).href;
}

export async function runMain(action) {
  try {
    await action();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
