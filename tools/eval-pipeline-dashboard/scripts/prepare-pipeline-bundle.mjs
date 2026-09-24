import { parseArgs } from "node:util";
import { dirname, resolve } from "node:path";
import { loadShardInput, pipelineManifest, preparePipelineBundle } from "../lib/pipeline-bundle.js";

const { values } = parseArgs({ options: { input: { type: "string" }, output: { type: "string" } } });
if (!values.input || !values.output) throw new Error("Usage: prepare-pipeline-bundle.mjs --input <shard-index.json> --output <dashboard-bundle.zip>");
const path = resolve(values.input);
console.log(JSON.stringify(await preparePipelineBundle({ shardInput: await loadShardInput(path), resultsRoot: dirname(path),
  manifest: pipelineManifest(process.env), outputPath: resolve(values.output) }), null, 2));