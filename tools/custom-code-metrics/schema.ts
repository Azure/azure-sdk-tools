import { readFile, copyFile, mkdir, rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const [mode, copyPath, ...extra] = process.argv.slice(2);
const copyMode = ["check-copy", "sync-copy"].includes(mode);
if ((!copyMode && !["clean", "generate", "check"].includes(mode)) ||
  (copyMode ? !copyPath || extra.length > 0 : copyPath !== undefined)) {
  throw new Error("Usage: node --experimental-strip-types schema.ts clean|generate|check OR check-copy|sync-copy <snapshot schema destination>");
}
if (copyMode) {
  const source = new URL("./schemas/CustomCodeMetrics.schema.json", import.meta.url);
  const destination = resolve(copyPath);
  const canonical = await readFile(source);
  if (mode === "sync-copy") {
    await mkdir(dirname(destination), { recursive: true });
    await copyFile(source, destination);
    console.log(`Copied the canonical snapshot schema to ${destination}.`);
  } else if (!canonical.equals(await readFile(destination))) {
    throw new Error(`Snapshot schema copy is stale: ${destination}. Run node --experimental-strip-types schema.ts sync-copy <destination>.`);
  } else {
    console.log(`Snapshot schema copy matches: ${destination}.`);
  }
} else {
  for (const name of ["CustomCodeMetrics", "RepositoryCodeMetrics", "ReportIndex", "HistoryMonth"]) {
    const emitted = new URL(`./tsp-output/@typespec/json-schema/${name}.json`, import.meta.url);
    const destination = new URL(`./schemas/${name}.schema.json`, import.meta.url);
    if (mode === "clean") await rm(emitted, { force: true });
    else if (mode === "generate") await copyFile(emitted, destination);
    else if (!(await readFile(emitted)).equals(await readFile(destination))) {
      throw new Error(`${name} JSON Schema is stale; run npm run generate.`);
    }
  }
}
