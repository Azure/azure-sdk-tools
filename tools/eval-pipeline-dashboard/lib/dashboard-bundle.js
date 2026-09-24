import { mkdir, readFile, readdir, stat, writeFile } from "node:fs/promises";
import { dirname, join, posix, relative, resolve, sep } from "node:path";
import { strFromU8, strToU8, unzipSync, zipSync } from "fflate";

export const DASHBOARD_BUNDLE_NAME = "dashboard-bundle.zip";
export const MAX_BUNDLE_BYTES = 32 * 1024 * 1024;
const MAX_ENTRIES = 10_000;
const MAX_EXTRACTED_BYTES = 128 * 1024 * 1024;

function normalizeEntryName(name) {
  const normalized = name.replaceAll("\\", "/");
  if (
    normalized.startsWith("/") ||
    /[\u0000-\u001f\u007f:]/.test(normalized) ||
    normalized.split("/").some((part) => part === ".." || /[. ]$/.test(part) || /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(part))
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
  if (bundleBytes.byteLength > MAX_BUNDLE_BYTES) throw new Error("Dashboard bundle exceeds upload limits.");
  const declared = new Map();
  let declaredBytes = 0;
  let entryCount = 0;
  // Inspect every directory entry before allocating decompressed buffers.
  unzipSync(bundleBytes, { filter(file) {
    const name = normalizeEntryName(file.name);
    const key = name.toLowerCase();
    if (declared.has(key)) throw new Error(`Duplicate ZIP entry: '${name}'.`);
    if (!Number.isSafeInteger(file.originalSize) || file.originalSize < 0) throw new Error("Invalid ZIP entry size.");
    declaredBytes += file.originalSize;
    if (++entryCount > MAX_ENTRIES || declaredBytes > MAX_EXTRACTED_BYTES) {
      throw new Error("Dashboard bundle exceeds extraction limits.");
    }
    if (!["manifest.json", "results.jsonl", "eval-summary.md", "junit/"].includes(name) && !/^junit\/[^/]+\.xml$/.test(name)) {
      throw new Error(`Unexpected bundle entry: '${name}'.`);
    }
    declared.set(key, file.originalSize);
    return false;
  } });
  const rawEntries = unzipSync(bundleBytes);
  const entries = new Map();
  let extractedBytes = 0;

  for (const [rawName, bytes] of Object.entries(rawEntries)) {
    const name = normalizeEntryName(rawName);
    if (!name || name.endsWith("/")) continue;
    if (entries.has(name)) throw new Error(`Duplicate ZIP entry: '${name}'.`);
    if (bytes.byteLength !== declared.get(name.toLowerCase())) throw new Error(`ZIP size mismatch: '${name}'.`);
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
  if ((await stat(bundlePath)).size > MAX_BUNDLE_BYTES) throw new Error("Dashboard bundle exceeds upload limits.");
  const { manifest } = readArchive(new Uint8Array(await readFile(bundlePath)));
  return manifest;
}

export async function extractDashboardBundle(bundlePath, outputDirectory) {
  if ((await stat(bundlePath)).size > MAX_BUNDLE_BYTES) throw new Error("Dashboard bundle exceeds upload limits.");
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