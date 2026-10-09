import { readFile, writeFile, mkdir, copyFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import Ajv from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import standaloneCode from "ajv/dist/standalone/index.js";
import { compile } from "json-schema-to-typescript";
import { build } from "esbuild";
import type { Snapshot } from "./data.ts";

const directory = dirname(fileURLToPath(import.meta.url));
const root = dirname(directory);
const generated = join(directory, "generated");
const output = join(directory, "dist");
const seedPaths: string[] = [];
let indexUrl = "";
let publishingOnly = false;
let preview = false;
for (let index = 2; index < process.argv.length; index++) {
  const argument = process.argv[index];
  if (argument === "--publishing-only" && !publishingOnly) {
    publishingOnly = true;
    continue;
  }
  if (argument === "--preview" && !preview) {
    preview = true;
    continue;
  }
  if (!["--snapshot", "--index-url"].includes(argument) || !process.argv[index + 1]) {
    throw new Error("Usage: node --experimental-strip-types dashboard/build.ts [--snapshot <JSON path>]... [--index-url <HTTPS URL>] OR --preview --snapshot <JSON path>... OR --publishing-only");
  }
  const value = process.argv[++index];
  if (argument === "--snapshot") seedPaths.push(resolve(value));
  else {
    const url = new URL(value);
    if (url.protocol !== "https:" || url.username || url.password || url.search || url.hash) {
      throw new Error("Hosted reporting requires a public HTTPS index URL without credentials, query or fragment.");
    }
    indexUrl = url.href;
  }
}
if (publishingOnly && (seedPaths.length || indexUrl || preview)) {
  throw new Error("--publishing-only cannot be combined with dashboard snapshot or index options.");
}
if (preview && (!seedPaths.length || indexUrl)) {
  throw new Error("--preview requires snapshot inputs and cannot be combined with a hosted index URL.");
}
await mkdir(generated, { recursive: true });
for (const [file, name] of [["snapshot", "RepositoryCodeMetrics"], ["dotnet-snapshot", "CustomCodeMetrics"],
  ["report-index", "ReportIndex"], ["history-month", "HistoryMonth"]]) {
  const schema = JSON.parse(await readFile(join(root, "schemas", `${name}.schema.json`), "utf8"));
  await writeFile(join(generated, `${file}.d.ts`), await compile({ ...schema, title: name }, name, {
    additionalProperties: false,
    ignoreMinAndMaxItems: true,
    bannerComment: "/* Generated from the TypeSpec JSON Schema. Do not edit. */",
  }));
  const ajv = new Ajv({ allErrors: false, code: { source: true, esm: true } });
  addFormats(ajv);
  const validate = ajv.compile(schema);
  await build({
    stdin: { contents: standaloneCode(ajv, validate), resolveDir: root, loader: "js" },
    outfile: join(generated, `validate-${file}.mjs`),
    bundle: true,
    target: "es2022",
    format: "esm",
    platform: "neutral",
  });
  await writeFile(join(generated, `validate-${file}.d.mts`), `
import type { ${name} } from "./${file}.js";
declare const validate: {
  (value: unknown): value is ${name};
  errors?: readonly { instancePath: string; message?: string }[] | null;
};
export default validate;
`);
}
const typecheck = spawnSync(process.execPath, [
  join(root, "node_modules", "typescript", "bin", "tsc"),
  "--project", join(root, "tsconfig.json"),
], { stdio: "inherit" });
if (typecheck.error) throw typecheck.error;
if (typecheck.status !== 0) throw new Error("Custom code metrics TypeScript check failed.");
await build({
  entryPoints: [join(directory, "data.ts")],
  outfile: join(generated, "data.mjs"),
  bundle: true,
  target: "es2022",
  format: "esm",
  platform: "node",
});
await build({
  entryPoints: [join(directory, "report.ts")],
  outfile: join(generated, "report.mjs"),
  bundle: true,
  target: "es2022",
  format: "esm",
  platform: "node",
});
if (publishingOnly) {
  console.log(`Built publishing validators and helpers in ${generated}; no website output written.`);
} else {
  const { parseSnapshot, mergeSnapshots, withoutFileEvidence } = await import("./data.ts");
  const incoming: Snapshot[] = [];
  for (const path of seedPaths) {
    try {
      incoming.push(parseSnapshot(await readFile(path, "utf8")));
    } catch (error) {
      throw new Error(`${path}: ${error instanceof Error ? error.message : String(error)}`, { cause: error });
    }
  }
  let seeds = mergeSnapshots([], incoming);
  if (preview) {
    if (seeds.some((snapshot) => snapshot.repository.isDirty)) {
      throw new Error("Official observations require committed source.");
    }
    seeds = seeds.map(withoutFileEvidence);
  }
  await mkdir(output, { recursive: true });
  await build({
    entryPoints: [join(directory, "app.ts")],
    outfile: join(output, "app.js"),
    bundle: true,
    minify: true,
    target: "es2022",
    format: "iife",
    platform: "browser",
    legalComments: "eof",
  });
  await writeFile(join(output, "snapshots.js"), `globalThis.customCodeMetricsSeed = ${JSON.stringify(seeds)};\nglobalThis.customCodeMetricsIndexUrl = ${JSON.stringify(indexUrl)};\nglobalThis.customCodeMetricsPreview = ${preview};\n`);
  for (const name of ["index.html", "styles.css", "staticwebapp.config.json"]) {
    await copyFile(join(directory, name), join(output, name));
  }
  console.log(`Built ${output} (${seeds.length} seed snapshots; ${preview ? "static preview; no live feed" : indexUrl ? "hosted reporting configured" : "offline; no external assets"}).`);
}
