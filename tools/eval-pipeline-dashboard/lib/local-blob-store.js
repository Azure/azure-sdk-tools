import { createHash, randomUUID } from "node:crypto";
import { createReadStream, existsSync } from "node:fs";
import { copyFile, mkdir, readdir, rename, rm, stat } from "node:fs/promises";
import { basename, dirname, join, relative, resolve, sep } from "node:path";
import { DASHBOARD_BUNDLE_NAME } from "./dashboard-bundle.js";

function validateSegment(name, value) {
  const segment = String(value ?? "").trim();
  if (!segment || segment === "." || segment === ".." || /[\\/]/.test(segment)) {
    throw new Error(`Invalid ${name}: '${segment}'.`);
  }
  return segment;
}

async function sha256(path) {
  const hash = createHash("sha256");
  await new Promise((resolvePromise, reject) => {
    const stream = createReadStream(path);
    stream.on("data", (chunk) => hash.update(chunk));
    stream.on("end", resolvePromise);
    stream.on("error", reject);
  });
  return hash.digest("hex");
}

async function findBundles(root, current = root) {
  const bundles = [];
  for (const entry of await readdir(current, { withFileTypes: true })) {
    const fullPath = join(current, entry.name);
    if (entry.isDirectory()) bundles.push(...(await findBundles(root, fullPath)));
    else if (entry.isFile() && entry.name === DASHBOARD_BUNDLE_NAME) bundles.push(fullPath);
  }
  return bundles;
}

async function describeBlob(blobRoot, filePath) {
  const properties = await stat(filePath);
  return {
    name: relative(resolve(blobRoot), resolve(filePath)).split(sep).join("/"),
    path: filePath,
    etag: await sha256(filePath),
    size: properties.size,
    lastModified: properties.mtime.toISOString(),
  };
}

export function getLocalBlobPath(blobRoot, manifest) {
  const segments = [
    validateSegment("ADO project", manifest.adoProject),
    validateSegment("repository", manifest.repo),
    validateSegment("pipeline definition ID", manifest.pipelineDefinitionId),
    validateSegment("build ID", manifest.buildId),
    validateSegment("summary attempt", manifest.summaryAttempt),
  ];
  return join(resolve(blobRoot), ...segments, DASHBOARD_BUNDLE_NAME);
}

export async function publishLocalBlob({ blobRoot, bundlePath, manifest }) {
  const destination = getLocalBlobPath(blobRoot, manifest);
  if (existsSync(destination)) {
    throw new Error(`Immutable Blob already exists: '${destination}'.`);
  }
  await mkdir(dirname(destination), { recursive: true });
  const temporaryPath = `${destination}.uploading-${randomUUID()}`;
  try {
    await copyFile(bundlePath, temporaryPath);
    await rename(temporaryPath, destination);
  } catch (error) {
    await rm(temporaryPath, { force: true });
    throw error;
  }
  return describeBlob(blobRoot, destination);
}

export async function listLocalBlobs(blobRoot) {
  if (!existsSync(blobRoot)) return [];
  const bundles = await findBundles(resolve(blobRoot));
  const descriptions = await Promise.all(bundles.map((path) => describeBlob(blobRoot, path)));
  return descriptions.sort((left, right) => left.name.localeCompare(right.name));
}

export async function downloadLocalBlob(blob, outputPath) {
  await mkdir(dirname(outputPath), { recursive: true });
  await copyFile(blob.path, outputPath);
  return outputPath;
}

export function getBlobBasename(blob) {
  return basename(blob.path);
}