import { ApiModel } from "@microsoft/api-extractor-model";
import { readFile, writeFile } from "node:fs/promises";
import { generateApiview } from "./generate";

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
  if (process.argv.length < 4) {
    console.log("Please run this tool with proper input");
    console.log("ts-genapi <Path to api-extractor JSON output> <Path to apiviewFile>");
    process.exit(1);
  }
  const { Name, PackageName, PackageVersion, dependencies, apiModel } = await loadApiJson(
    process.argv[2],
  );

  const result = JSON.stringify(
    generateApiview({
      meta: {
        Name,
        PackageName,
        PackageVersion,
        ParserVersion: "2.0.0",
        Language: "JavaScript",
      },
      dependencies,
      apiModel,
    }),
  );

  await writeFile(process.argv[3], result);
}

main().catch(console.error);
