import { ApiModel } from "@microsoft/api-extractor-model";
import { readFile, writeFile } from "node:fs/promises";
import { generateApiView } from "./generate";
import commandLineArgs from "command-line-args";

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

async function main() {
  const optionDefinitions = [
    { name: "input", type: String },
    { name: "output", type: String },
    { name: "metadata-file", type: String },
    { name: "help", type: Boolean, alias: "h" },
  ];

  const options = commandLineArgs(optionDefinitions);

  if (options.help) {
    console.log("Usage:");
    console.log(
      "  ts-genapi --input <path-to-api-extractor-json> --output <path-to-output-json> [--metadata-file <path-to-metadata>]",
    );
    console.log("");
    console.log("Options:");
    console.log("  --input          Path to api-extractor JSON output");
    console.log("  --output         Path to output JSON file");
    console.log("  --metadata-file  Path to metadata file (optional)");
    console.log("  --help, -h       Show this help message");
    process.exit(0);
  }

  if (!options.input || !options.output) {
    console.error("Error: Both --input and --output are required");
    console.log("");
    console.log("Usage:");
    console.log(
      "  ts-genapi --input <path-to-api-extractor-json> --output <path-to-output-json> [--metadata-file <path-to-metadata>]",
    );
    console.log("");
    console.log("For more information, run: ts-genapi --help");
    process.exit(1);
  }

  const { Name, PackageName, PackageVersion, dependencies, apiModel } = await loadApiJson(
    options.input,
  );

  const result = JSON.stringify(
    generateApiView({
      meta: {
        Name,
        PackageName,
        PackageVersion,
        ParserVersion: "2.0.6",
        Language: "JavaScript",
      },
      dependencies,
      apiModel,
    }),
  );

  await writeFile(options.output, result);
}

main().catch(console.error);
