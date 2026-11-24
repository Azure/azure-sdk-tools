import { normalizePath } from "@typespec/compiler";
import { lstat, readdir } from "node:fs/promises";
import { join } from "node:path";
import { removeDirectory } from "./fs.js";
import type { TspLocation } from "./typespec.js";

// Keep the opt-in pattern inputs literal and portable (no Git wildcards or Windows aliases).
function isLiteralSegment(segment: string): boolean {
  return (
    segment.length > 0 &&
    !/[^A-Za-z0-9_.-]/.test(segment) &&
    !segment.endsWith(".") &&
    segment.toLowerCase() !== ".git" &&
    !/^(?:con|prn|aux|nul|com[0-9]|lpt[0-9])(?:\.|$)/i.test(segment)
  );
}

export function getSparseCheckoutExclusions(location: TspLocation, project: string): string[] {
  const value = location.sparseCheckoutExclusions;
  if (value === undefined) return [];
  if (
    !Array.isArray(value) ||
    !value.every((name) => typeof name === "string" && isLiteralSegment(name))
  ) {
    throw new Error(
      "sparseCheckoutExclusions must be a list of literal direct child directory names in tsp-location.yaml",
    );
  }
  if (!value.length) return [];

  const additional = location.additionalDirectories ?? [];
  for (const path of [project, ...additional]) {
    if (!path.split("/").every(isLiteralSegment)) {
      throw new Error(
        `sparseCheckoutExclusions requires portable repository-relative directory paths: '${path}'`,
      );
    }
  }

  const projectName = project.split("/").at(-1)!.toLowerCase();
  const entrypoint =
    typeof location.entrypointFile === "string"
      ? normalizePath(location.entrypointFile).toLowerCase()
      : undefined;
  for (const name of value) {
    const child = name.toLowerCase();
    if (entrypoint === child || entrypoint?.startsWith(`${child}/`)) {
      throw new Error(
        `sparseCheckoutExclusions entry '${name}' contains entrypointFile '${location.entrypointFile}'`,
      );
    }
    const excluded = `${project.toLowerCase()}/${child}`;
    for (const path of additional) {
      const lower = path.toLowerCase();
      if (
        lower === excluded ||
        lower.startsWith(`${excluded}/`) ||
        excluded.startsWith(`${lower}/`)
      ) {
        throw new Error(
          `sparseCheckoutExclusions entry '${name}' overlaps additionalDirectories entry '${path}'`,
        );
      }
      if (lower.split("/").at(-1) === projectName) {
        throw new Error(
          `sparseCheckoutExclusions cannot be combined with additionalDirectories entry '${path}' because it stages into the project's directory`,
        );
      }
    }
  }
  return [...new Set(value)];
}

/** A previous full sync may have left these directories in the copy destination. */
export async function clearStagedExclusions(
  tempRoot: string,
  stagedProject: string,
  exclusions: string[],
  ignoreCase: boolean,
) {
  if (!(await lstat(tempRoot)).isDirectory() || !(await lstat(stagedProject)).isDirectory()) {
    throw new Error(
      "sparseCheckoutExclusions requires real TempTypeSpecFiles and staged project directories (not symlinks)",
    );
  }
  const key = (name: string) =>
    ignoreCase ? name.replace(/[A-Z]/g, (char) => char.toLowerCase()) : name;
  const excluded = new Set(exclusions.map(key));
  for (const child of await readdir(stagedProject, { withFileTypes: true })) {
    if (child.isDirectory() && excluded.has(key(child.name))) {
      await removeDirectory(join(stagedProject, child.name));
    }
  }
}
