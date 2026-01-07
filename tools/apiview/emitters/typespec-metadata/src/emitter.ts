import { emitFile, type EmitContext, NoTarget, resolvePath } from "@typespec/compiler";
import { stringify as stringifyYaml } from "yaml";
import packageJson from "../package.json" with { type: "json" };
import { collectLanguagePackages, buildSpecMetadata } from "./collector.js";
import type { MetadataSnapshot } from "./metadata.js";
import { type MetadataEmitterOptions, normalizeOptions, type NormalizedMetadataEmitterOptions } from "./options.js";
import { reportDiagnostic } from "./lib.js";

const SNAPSHOT_VERSION = packageJson.version ?? "0.0.0";

export async function $onEmit(context: EmitContext<MetadataEmitterOptions>): Promise<void> {
  const options = normalizeOptions(context.options);
  const specMetadata = buildSpecMetadata(context.program);

  // Get the common tsp-output directory (parent of this emitter's output dir)
  const commonOutputDir = context.emitterOutputDir.split(/[\/\\]/).slice(0, -2).join('/');

  let languageResult: Awaited<ReturnType<typeof collectLanguagePackages>>;
  try {
    languageResult = await collectLanguagePackages(context.program, commonOutputDir);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    reportDiagnostic(context.program, {
      code: "failed-to-emit",
      target: NoTarget,
      format: { message },
    });
    return;
  }

  const snapshot: MetadataSnapshot = {
    emitterVersion: SNAPSHOT_VERSION,
    generatedAt: new Date().toISOString(),
    spec: specMetadata,
    languages: languageResult.languages,
    sourceConfigPath: languageResult.sourceConfigPath,
  };

  await writeSnapshot(context, options, snapshot);
}

async function writeSnapshot(
  context: EmitContext<MetadataEmitterOptions>,
  options: NormalizedMetadataEmitterOptions,
  snapshot: MetadataSnapshot,
): Promise<void> {
  const serialized = options.format === "json" 
    ? JSON.stringify(snapshot, null, 2) + "\n" 
    : stringifyYaml(snapshot, {
        lineWidth: 0,
      });
  const outputPath = resolvePath(context.emitterOutputDir, options.outputFile);
  await emitFile(context.program, {
    path: outputPath,
    content: serialized,
  });
}
