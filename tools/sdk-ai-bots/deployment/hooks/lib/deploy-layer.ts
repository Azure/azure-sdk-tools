/**
 * deployLayer — executes a single infra layer with its pre/post callbacks.
 *
 * Ported verbatim from ../../../azd-experiments/hooks/lib/deploy-layer.ts.
 *
 * Wraps `az deployment group create` and surfaces any CLI errors as thrown
 * exceptions so the caller (postprovision.ts) can abort the pipeline.
 */

import { execSync } from "child_process";
import { existsSync } from "fs";
import { resolve } from "path";
import type { Layer, LayerContext } from "./layers.js";

function log(layer: string, message: string): void {
  console.log(`[${layer}] ${message}`);
}

export async function deployLayer(layer: Layer, ctx: LayerContext): Promise<void> {
  const bicepPath = resolve(process.cwd(), layer.bicepFile);

  if (!existsSync(bicepPath)) {
    throw new Error(`Bicep file not found for layer '${layer.name}': ${bicepPath}`);
  }

  log(layer.name, "pre-deploy");
  await layer.pre?.(ctx);

  log(layer.name, `deploying → ${bicepPath}`);

  const paramArgs: string[] = [];
  if (layer.params) {
    const values = layer.params();
    for (const [k, v] of Object.entries(values)) {
      // az CLI accepts key=value; escape embedded single quotes for the shell.
      const escaped = v.replace(/'/g, `'\\''`);
      paramArgs.push(`${k}='${escaped}'`);
    }
  }

  const parts = [
    "az deployment group create",
    `--resource-group "${ctx.resourceGroup}"`,
    `--template-file "${bicepPath}"`,
    `--name "${layer.name}"`,
    "--no-prompt",
  ];
  if (paramArgs.length > 0) {
    parts.push(`--parameters ${paramArgs.join(" ")}`);
  }
  // Join on single spaces (no backslash-newline continuations) so the command
  // parses correctly under both bash and Windows cmd.exe — execSync picks the
  // default shell per platform and cmd.exe treats trailing `\` as literal.
  const cmd = parts.join(" ");

  execSync(cmd, { stdio: "inherit" });
  log(layer.name, "deployment succeeded");

  log(layer.name, "post-deploy");
  await layer.post?.(ctx);
}

export async function runLayerPipeline(
  layers: Layer[],
  ctx: LayerContext
): Promise<void> {
  const targetLayer = process.env.DEPLOY_LAYER?.trim();

  // Without DEPLOY_LAYER, main.bicep has already applied every module in the
  // same `azd provision` invocation — re-running the whole pipeline here is
  // redundant (and hits parameter drift since main.bicep passes locations /
  // names that this pipeline doesn't). The pipeline exists for targeted
  // per-layer redeploys via `DEPLOY_LAYER=<name> azd provision`.
  if (!targetLayer) {
    console.log(
      "[pipeline] DEPLOY_LAYER not set — skipping (main.bicep already applied all modules)."
    );
    return;
  }

  const toRun = layers.filter((l) => l.name === targetLayer);

  if (toRun.length === 0) {
    throw new Error(
      `DEPLOY_LAYER='${targetLayer}' does not match any layer. ` +
        `Valid names: ${layers.map((l) => l.name).join(", ")}`
    );
  }

  console.log(`[pipeline] Partial deployment — running layer: ${targetLayer}`);

  for (const layer of toRun) {
    await deployLayer(layer, ctx);
  }

  console.log("[pipeline] All layers complete.");
}
