import { ApiModel } from "@microsoft/api-extractor-model";
import { readFile, writeFile } from "node:fs/promises";
import { generateApiView } from "./generate";
import { CrossLanguageMetadata } from "./models";
import { version as parserVersion } from "../package.json";

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
  if (process.argv.length < 4) {
    console.log("Please run this tool with proper input");
    console.log(
      "ts-genapi <Path to api-extractor JSON output> <Path to apiviewFile> [Path to metadata.json]",
    );
    process.exit(1);
  }
  const { Name, PackageName, PackageVersion, dependencies, apiModel } = await loadApiJson(
    process.argv[2],
  );

  // Load cross-language metadata if provided
  const crossLanguageDefinitionIds = process.argv[4]
    ? await loadMetadata(process.argv[4])
    : undefined;

  const result = JSON.stringify(
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
      crossLanguageDefinitionIds,
    }),
  );

  await writeFile(process.argv[3], result);
}

main().catch(console.error);
