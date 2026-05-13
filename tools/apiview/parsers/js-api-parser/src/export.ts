import { ApiModel } from "@microsoft/api-extractor-model";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { generateApiView } from "./generate.js";
import { CrossLanguageMetadata } from "./models.js";
import { generateApiViewFromDts } from "./dts/index.js";
import { version as parserVersion } from "../package.json";

async function loadApiJson(fileName: string) {
  const apiModel = new ApiModel();
  apiModel.loadPackage(fileName);

  const apiJson = JSON.parse(await readFile(fileName, { encoding: "utf-8" }));
  const dependencies = apiJson.metadata.dependencies;
  const packageVersionString = apiJson.metadata.version;

  return {
    Name: apiModel.packages[0].name + (packageVersionString ? `(${packageVersionString})` : ""),
    PackageName: apiModel.packages[0].name,
    PackageVersion: packageVersionString ?? "",
    dependencies,
    apiModel,
  };
}

async function loadMetadata(fileName: string): Promise<CrossLanguageMetadata | undefined> {
  try {
    const metadataContent = await readFile(fileName, { encoding: "utf-8" });
    return JSON.parse(metadataContent) as CrossLanguageMetadata;
  } catch (error) {
    console.warn(`Warning: Could not load metadata file ${fileName}:`, String(error));
    return undefined;
  }
}

/**
 * Parses named CLI flags of the form --key value from argv,
 * returning a map and the remaining positional arguments.
 */
function parseArgs(argv: string[]): {
  flags: Map<string, string>;
  positional: string[];
} {
  const flags = new Map<string, string>();
  const positional: string[] = [];
  for (let i = 0; i < argv.length; i++) {
    if (argv[i].startsWith("--") && i + 1 < argv.length) {
      flags.set(argv[i].slice(2), argv[i + 1]);
      i++;
    } else {
      positional.push(argv[i]);
    }
  }
  return { flags, positional };
}

async function main() {
  const { flags, positional } = parseArgs(process.argv.slice(2));

  if (positional.length < 2) {
    console.log("Please run this tool with proper input");
    console.log(
      "ts-genapi <Path to input file (.api.json or .d.ts)> <Path to apiviewFile> [Path to metadata.json]",
    );
    console.log("");
    console.log("When input is a .d.ts file, adjacent package.json is used for package metadata.");
    console.log("Optional flags for .d.ts input:");
    console.log("  --package-name <name>       Override package name");
    console.log("  --package-version <version> Override package version");
    process.exit(1);
  }

  const inputFile = positional[0];
  const outputFile = positional[1];
  const metadataFile = positional[2];

  // Load cross-language metadata if provided
  const loadedMetadata = metadataFile ? await loadMetadata(metadataFile) : undefined;
  const crossLanguagePackageId =
    loadedMetadata?.crossLanguageDefinitions?.CrossLanguagePackageId;
  const crossLanguageDefinitionIds =
    loadedMetadata?.crossLanguageDefinitions?.CrossLanguageDefinitionId;

  let result: string;

  if (path.extname(inputFile) === ".ts" && inputFile.endsWith(".d.ts")) {
    // .d.ts path
    const codeFile = await generateApiViewFromDts({
      dtsFilePath: inputFile,
      packageName: flags.get("package-name"),
      packageVersion: flags.get("package-version"),
      parserVersion,
      crossLanguagePackageId,
      crossLanguageDefinitionIds,
    });
    result = JSON.stringify(codeFile);
  } else {
    const rawContent = await readFile(inputFile, { encoding: "utf-8" });
    const rawJson = JSON.parse(rawContent);

    if (rawJson.ReviewLines !== undefined) {
      // Already a CodeFile JSON (e.g. generated from a .d.ts file) — pass through unchanged.
      result = rawContent;
    } else {
      // api-extractor .api.json path (existing behaviour)
      const { Name, PackageName, PackageVersion, dependencies, apiModel } =
        await loadApiJson(inputFile);

      result = JSON.stringify(
        generateApiView({
          meta: {
            Name,
            PackageName,
            PackageVersion,
            ParserVersion: parserVersion,
            Language: "JavaScript",
          },
          dependencies,
          apiModel,
          crossLanguagePackageId,
          crossLanguageDefinitionIds,
        }),
      );
    }
  }

  await writeFile(outputFile, result);
}

main().catch(console.error);
