import * as fs from "fs";
import { processItem } from "./process-items/processItem";
import { CodeFile, ReviewLine, TokenKind } from "./models/apiview-models";
import { Crate, FORMAT_VERSION } from "../rustdoc-types/output/rustdoc-types";
import { reexportLines } from "./process-items/processUse";
import { sortExternalItems } from "./process-items/utils/sorting";

let apiJson: Crate;
export let PACKAGE_NAME: string;
export function getAPIJson(): Crate {
  return apiJson;
}

/**
 * Processes the root item of the crate and adds its review lines to the code file
 * @param codeFile The code file to add review lines to
 */
function processRootItem(codeFile: CodeFile): void {
  const reviewLines = processItem(apiJson.index[apiJson.root]);
  if (reviewLines) {
    codeFile.ReviewLines.push(...reviewLines);
  }
}

/**
 * Adds a section header to the code file
 * @param codeFile The code file to add the header to
 * @param headerText The header text
 */
function addSectionHeader(codeFile: CodeFile, headerText: string): void {
  codeFile.ReviewLines.push({
    LineId: `header-${headerText}`,
    Tokens: [
      {
        Kind: TokenKind.Punctuation,
        Value: `/* ${headerText} */`,
        NavigateToId: `header-${headerText}`,
        NavigationDisplayName: `/* ${headerText} */`,
        RenderClasses: ["namespace"],
      },
    ],
  });
}

/**
 * Checks if an item is already included in any of the external modules
 * @param reexportItem The item to check
 * @returns Whether the item is already included
 */
function isItemAlreadyIncludedInModules(reexportItem: ReviewLine): boolean {
  for (let j = 0; j < reexportLines.external.modules.length; j++) {
    const moduleReexport = reexportLines.external.modules[j];

    if (moduleReexport.Children && moduleReexport.Children.length > 0) {
      for (let k = 0; k < moduleReexport.Children.length; k++) {
        if (moduleReexport.Children[k].LineId === reexportItem.LineId) {
          return true;
        }
      }
    }
  }

  return false;
}

/**
 * Processes external item reexports and adds them to the code file
 * @param codeFile The code file to add review lines to
 */
function processExternalItemReexports(codeFile: CodeFile): void {
  if (reexportLines.external.items.length > 0) {
    addSectionHeader(codeFile, "External items");

    // Sort the external items by kind (using itemKindOrder) and then by name
    sortExternalItems(reexportLines.external.items);

    // Process external item re-exports that aren't already included in modules
    for (let i = 0; i < reexportLines.external.items.length; i++) {
      const reexportItem = reexportLines.external.items[i];

      // Only add the item if it's not already included in external module reexports
      if (!isItemAlreadyIncludedInModules(reexportItem)) {
        codeFile.ReviewLines.push(reexportItem);
      }
    }
  }
}

/**
 * Builds the code file by processing items and reexports
 * @returns The built code file
 */
function buildCodeFile(): CodeFile {
  const codeFile: CodeFile = {
    PackageName: apiJson.index[apiJson.root].name || "unknown",
    PackageVersion: apiJson["crate_version"] || "unknown",
    ParserVersion: "1.1.0",
    Language: "Rust",
    ReviewLines: [],
  };
  PACKAGE_NAME = codeFile.PackageName;

  processRootItem(codeFile);
  processExternalItemReexports(codeFile);
  return codeFile;
}

/**
 * Reads and parses the API JSON from the input file
 * @param inputFilePath Path to the input file
 * @returns Whether there is a format mismatch
 */
function readApiJson(inputFilePath: string): void {
  const data = fs.readFileSync(inputFilePath, "utf8");
  apiJson = JSON.parse(data);

  if (apiJson.format_version !== FORMAT_VERSION) {
    console.warn(
      `Warning: Different format version detected: ${apiJson.format_version}, parser supports ${FORMAT_VERSION}. This may cause errors or unexpected results.`,
    );
  }
}

/**
 * Main function that orchestrates the API parsing process
 */
function main() {
  // Read the JSON file
  const args = process.argv.slice(2);
  if (args.length < 2) {
    throw new Error("Please provide input and output file paths as arguments");
  }
  const inputFilePath = args[0];
  const outputFilePath = args[1];

  readApiJson(inputFilePath);

  const codeFile = buildCodeFile();
  fs.writeFileSync(outputFilePath, JSON.stringify(codeFile, null, 2));
  console.log(`The exported API surface has been successfully saved to '${outputFilePath}'`);
}

main();
