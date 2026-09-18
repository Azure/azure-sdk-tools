import { mkdir, mkdtemp, rm, writeFile, stat, readFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, it, expect } from "vitest";
import { joinPaths } from "@typespec/compiler";
import { copyRepoNpmrcToTemp, relativeRepoNpmrcPath } from "../src/fs.js";

describe("eng/common/.npmrc support", () => {
  let repoRoot: string;
  let tempRoot: string;

  beforeEach(async () => {
    repoRoot = await mkdtemp(join(tmpdir(), "tsp-client-npmrc-repo-"));
    tempRoot = await mkdtemp(join(tmpdir(), "tsp-client-npmrc-temp-"));
  });

  afterEach(async () => {
    await rm(repoRoot, { recursive: true, force: true });
    await rm(tempRoot, { recursive: true, force: true });
  });

  async function writeRepoNpmrc(contents: string): Promise<string> {
    const npmrcPath = joinPaths(repoRoot, relativeRepoNpmrcPath);
    await mkdir(join(repoRoot, "eng", "common"), { recursive: true });
    await writeFile(npmrcPath, contents);
    return npmrcPath;
  }

  it("copyRepoNpmrcToTemp copies the file into the temp directory as .npmrc", async () => {
    const contents = "registry=https://example.com/\n";
    await writeRepoNpmrc(contents);

    await copyRepoNpmrcToTemp(repoRoot, tempRoot);

    const destination = joinPaths(tempRoot, ".npmrc");
    const destStat = await stat(destination);
    expect(destStat.isFile()).toBe(true);
    expect(await readFile(destination, "utf8")).toBe(contents);
  });

  it("copyRepoNpmrcToTemp is a no-op when the repo has no eng/common/.npmrc", async () => {
    await expect(copyRepoNpmrcToTemp(repoRoot, tempRoot)).resolves.toBeUndefined();
    await expect(stat(joinPaths(tempRoot, ".npmrc"))).rejects.toThrow();
  });
});
