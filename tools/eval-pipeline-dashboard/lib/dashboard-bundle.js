import { mkdir, readFile, readdir, stat, writeFile } from "node:fs/promises";
import { dirname, join, posix, relative, resolve, sep } from "node:path";
import { strFromU8, strToU8, unzipSync, zipSync } from "fflate";

export const DASHBOARD_BUNDLE_NAME = "dashboard-bundle.zip";
const MAX_ENTRIES = 10_000;
const MAX_EXTRACTED_BYTES = 250 * 1024 * 1024;

function normalizeEntryName(name) {
  const normalized = name.replaceAll("\\", "/");
  if (
    normalized.startsWith("/") ||
    /^[A-Za-z]:/.test(normalized) ||
    normalized.split("/").includes("..")
  ) {
    throw new Error(`Unsafe ZIP entry: '${name}'.`);
  }
  return posix.normalize(normalized).replace(/^\.\//, "");
}

function parseManifest(bytes) {
  let manifest;
  try {
    manifest = JSON.parse(strFromU8(bytes));
  } catch (error) {
    throw new Error(
      `Invalid manifest.json: ${error instanceof Error ? error.message : String(error)}`
    );
  }
  if (manifest === null || typeof manifest !== "object" || Array.isArray(manifest)) {
    throw new Error("manifest.json must contain a JSON object.");
  }
  return manifest;
}

async function collectFiles(root, current = root) {
  const files = [];
  for (const entry of await readdir(current, { withFileTypes: true })) {
    const fullPath = join(current, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await collectFiles(root, fullPath)));
    } else if (entry.isFile()) {
      files.push({
        entryName: relative(root, fullPath).split(sep).join("/"),
        fullPath,
      });
    }
  }
  return files;
}

function readArchive(bundleBytes) {
  const rawEntries = unzipSync(bundleBytes);
  const entries = new Map();
  let extractedBytes = 0;

  for (const [rawName, bytes] of Object.entries(rawEntries)) {
    const name = normalizeEntryName(rawName);
    if (!name || name.endsWith("/")) continue;
    if (entries.has(name)) throw new Error(`Duplicate ZIP entry: '${name}'.`);
    extractedBytes += bytes.byteLength;
    if (entries.size + 1 > MAX_ENTRIES || extractedBytes > MAX_EXTRACTED_BYTES) {
      throw new Error("Dashboard bundle exceeds extraction limits.");
    }
    entries.set(name, bytes);
  }

  for (const required of ["manifest.json", "results.jsonl", "eval-summary.md"]) {
    if (!entries.has(required)) throw new Error(`Dashboard bundle is missing '${required}'.`);
  }
  if (![...entries.keys()].some((name) => name.startsWith("junit/") && name.endsWith(".xml"))) {
    throw new Error("Dashboard bundle is missing JUnit XML under 'junit/'.");
  }

  return { entries, manifest: parseManifest(entries.get("manifest.json")) };
}

export async function createDashboardBundle({ inputDirectory, outputPath, manifest }) {
  const inputRoot = resolve(inputDirectory);
  const files = await collectFiles(inputRoot);
  const byName = new Map(files.map((file) => [file.entryName, file]));
  for (const required of ["results.jsonl", "eval-summary.md"]) {
    if (!byName.has(required)) throw new Error(`Bundle input is missing '${required}'.`);
  }
  if (![...byName.keys()].some((name) => name.startsWith("junit/") && name.endsWith(".xml"))) {
    throw new Error("Bundle input is missing JUnit XML under 'junit/'.");
  }

  const archiveEntries = {
    "manifest.json": strToU8(`${JSON.stringify(manifest, null, 2)}\n`),
  };
  for (const { entryName, fullPath } of files) {
    if (entryName === "manifest.json") continue;
    archiveEntries[normalizeEntryName(entryName)] = new Uint8Array(await readFile(fullPath));
  }

  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, zipSync(archiveEntries, { level: 6 }));
  return outputPath;
}

export async function readDashboardBundleManifest(bundlePath) {
  const { manifest } = readArchive(new Uint8Array(await readFile(bundlePath)));
  return manifest;
}

export async function extractDashboardBundle(bundlePath, outputDirectory) {
  const { entries, manifest } = readArchive(new Uint8Array(await readFile(bundlePath)));
  const root = resolve(outputDirectory);
  await mkdir(root, { recursive: true });
  for (const [entryName, bytes] of entries) {
    const outputPath = resolve(root, ...entryName.split("/"));
    if (outputPath !== root && !outputPath.startsWith(`${root}${sep}`)) {
      throw new Error(`ZIP entry escapes extraction root: '${entryName}'.`);
    }
    await mkdir(dirname(outputPath), { recursive: true });
    await writeFile(outputPath, bytes);
  }
  return manifest;
}

export async function getFileSize(path) {
  return (await stat(path)).size;
}