import { ApiModel } from "@microsoft/api-extractor-model";
import { readFile, writeFile } from "node:fs/promises";
import { generateApiView } from "./generate";
import { CrossLanguageMetadata } from "./models";
import yargs from "yargs";
import { hideBin } from "yargs/helpers";

function getPackageVersion(fileName: string) {
  const match = fileName.match(/.*_(?<version>.*)\.api\.json/);
  return match?.length > 0 ? match.groups["version"] : undefined;
}

async function loadApiJson(fileName: string) {
  const apiModel = new ApiModel();
  const packageVersionString = getPackageVersion(fileName);

  apiModel.loadPackage(fileName);

  const apiJson = JSON.parse(await readFile(fileName, { encoding: "utf-8" }));
  const dependencies = apiJson.metadata.dependencies;

  return {
    Name: apiModel.packages[0].name + (packageVersionString ? `(${packageVersionString})` : ""),
    PackageName: apiModel.packages[0].name,
    PackageVersion: packageVersionString ?? "",
    dependencies,
    apiModel,
  };
}

async function loadMetadata(fileName: string): Promise<Record<string, string> | undefined> {
  try {
    const metadataContent = await readFile(fileName, { encoding: "utf-8" });
    const metadata: CrossLanguageMetadata = JSON.parse(metadataContent);
    return metadata.crossLanguageDefinitions?.CrossLanguageDefinitionId;
  } catch (error) {
    console.warn(`Warning: Could not load metadata file ${fileName}:`, String(error));
    return undefined;
  }
}

async function main() {
  const argv = await yargs(hideBin(process.argv))
    .usage("ts-genapi <input> [options]")
    .command("$0 <input>", "Generate APIView token file from API extractor output", (yargs) => {
      return yargs
        .positional("input", {
          describe: "Path to api-extractor JSON output file",
          type: "string",
          demandOption: true,
        })
        .option("output", {
          alias: "o",
          describe: "Path to output APIView file",
          type: "string",
          demandOption: true,
        })
        .option("metadata-file", {
          alias: "m",
          describe: "Path to metadata.json file for cross-language definitions",
          type: "string",
        });
    })
    .help()
    .alias("help", "h")
    .version(false)
    .strict()
    .parseAsync();

  const input = argv.input as string;
  const output = argv.output as string;
  const metadataFile = argv["metadata-file"] as string | undefined;

  const { Name, PackageName, PackageVersion, dependencies, apiModel } = await loadApiJson(input);

  // Load cross-language metadata if provided
  const crossLanguageDefinitionIds = metadataFile ? await loadMetadata(metadataFile) : undefined;

  const result = JSON.stringify(
    generateApiView({
      meta: {
        Name,
        PackageName,
        PackageVersion,
        ParserVersion: "2.0.7",
        Language: "JavaScript",
      },
      dependencies,
      apiModel,
      crossLanguageDefinitionIds,
    }),
  );

  await writeFile(output, result);
}

main().catch(console.error);
