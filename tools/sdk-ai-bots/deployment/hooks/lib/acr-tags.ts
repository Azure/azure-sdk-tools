/**
 * ACR tag helpers used by the pipeline to select the frontend's next
 * auto-incrementing image tag.
 *
 * Versioning scheme: `${prefix}-<major>.<minor>.<patch>` where `prefix` is the
 * azd environment name (e.g. `dev`). A fresh repository yields `${prefix}-1.0.0`
 * and each subsequent build bumps the major (`dev-2.0.0`, `dev-3.0.0`, ...).
 */

import { execSync } from "child_process";

/**
 * Return all tags for `repository`, newest first. Returns [] if the repository
 * does not exist yet (e.g. the very first build) or the lookup otherwise fails.
 */
export function listTags(registry: string, repository: string): string[] {
  try {
    const out = execSync(
      `az acr repository show-tags --name "${registry}" --repository "${repository}" --orderby time_desc --output tsv`,
      { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }
    );
    return out
      .split(/\r?\n/)
      .map((s) => s.trim())
      .filter(Boolean);
  } catch {
    return [];
  }
}

/** Escape a string for safe use inside a RegExp. */
export function escapeRegExp(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Compute the next version tag for `prefix`. Scans existing tags of the form
 * `${prefix}-<major>.<minor>.<patch>` and returns `${prefix}-<maxMajor+1>.0.0`,
 * so an existing `dev-1.0.0` yields `dev-2.0.0`. If no versioned tag exists
 * yet, returns `${prefix}-1.0.0`.
 */
export function getNextVersionTag(
  registry: string,
  repository: string,
  prefix: string
): string {
  const re = new RegExp(`^${escapeRegExp(prefix)}-(\\d+)\\.\\d+\\.\\d+$`);
  let maxMajor = 0;
  let found = false;
  for (const t of listTags(registry, repository)) {
    const m = re.exec(t);
    if (m) {
      found = true;
      const major = parseInt(m[1], 10);
      if (major > maxMajor) maxMajor = major;
    }
  }
  return `${prefix}-${found ? maxMajor + 1 : 1}.0.0`;
}
