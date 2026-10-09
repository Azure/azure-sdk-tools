import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { collectJava } from "./collect-java.ts";
import { collectPython } from "./collect-python.ts";
import { assertSourcePath, type ObservationContext, type SourceProvider } from "./collect-source.ts";
import { acceptSnapshot, type Snapshot } from "./dashboard/data.ts";

export async function collectCheckout(language: "java" | "python", repoRoot: string, outputDirectory: string): Promise<string> {
  const repository = `Azure/azure-sdk-for-${language}`;
  const root = resolve(repoRoot);
  const git = (...args: string[]): string => {
    const result = spawnSync("git", ["-C", root, ...args], { encoding: "utf8", maxBuffer: 128 * 1024 * 1024 });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`Git source verification failed: ${result.stderr.trim()}`);
    return result.stdout;
  };
  const origin = git("remote", "get-url", "origin").trim().replace(/\.git$/i, "");
  if (!new RegExp(`(?:github\\.com[:/])${repository}$`, "i").test(origin)) {
    throw new Error(`Native collection requires an actual ${repository} tracked checkout.`);
  }
  const fingerprint = () => {
    const commit = git("rev-parse", "HEAD").trim();
    const diff = git("diff", "--binary", "--no-ext-diff", "HEAD", "--");
    return { commit, isDirty: diff.length > 0, hash: createHash("sha256").update(commit).update(diff).digest("hex") };
  };
  const before = fingerprint();
  const paths = git("ls-files", "-z").split("\0").filter(Boolean);
  const observed = new Map<string, string>();
  const provider: SourceProvider = {
    paths,
    async readText(path) {
      assertSourcePath(path);
      const bytes = await readFile(join(root, path));
      const hash = createHash("sha256").update(bytes).digest("hex");
      const prior = observed.get(path);
      if (prior && prior !== hash) throw new Error(`Tracked source changed during collection: ${path}.`);
      observed.set(path, hash);
      return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
    },
  };
  const context: ObservationContext = {
    repository: { name: language === "java" ? "Azure/azure-sdk-for-java" : "Azure/azure-sdk-for-python",
      commit: before.commit, isDirty: before.isDirty },
    collectedAt: new Date().toISOString(),
  };
  const snapshot: Snapshot = acceptSnapshot(language === "java" ?
    await collectJava(provider, context) : await collectPython(provider, context));
  for (const [path, hash] of observed) {
    if (createHash("sha256").update(await readFile(join(root, path))).digest("hex") !== hash) {
      throw new Error(`Tracked source changed during collection: ${path}. No observation was written.`);
    }
  }
  if (fingerprint().hash !== before.hash) throw new Error("Tracked source or HEAD changed during collection. No observation was written.");
  await mkdir(outputDirectory, { recursive: true });
  const output = join(resolve(outputDirectory), `${snapshot.snapshotId}.json`);
  await writeFile(output, JSON.stringify(snapshot), { flag: "wx" });
  return output;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const options = new Map<string, string>();
  for (let index = 2; index < process.argv.length; index += 2) {
    const key = process.argv[index];
    const value = process.argv[index + 1];
    if (!["--language", "--repo-root", "--output-directory"].includes(key) || !value || options.has(key)) {
      throw new Error("Usage: collect.ts --language java|python --repo-root <tracked checkout> --output-directory <output>");
    }
    options.set(key, value);
  }
  const language = options.get("--language");
  const repoRoot = options.get("--repo-root");
  const output = options.get("--output-directory");
  if ((language !== "java" && language !== "python") || !repoRoot || !output) throw new Error("A supported language, tracked checkout and output directory are required.");
  console.log(await collectCheckout(language, repoRoot, output));
}
