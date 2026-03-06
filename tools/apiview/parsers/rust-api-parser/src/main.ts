import * as fs from "fs";
import { processItem } from "./process-items/processItem";
import { CodeFile, TokenKind } from "./models/apiview-models";
import { Crate, FORMAT_VERSION } from "../rustdoc-types/output/rustdoc-types";
import { externalReferencesLines } from "./process-items/utils/externalReexports";
import { sortExternalItems } from "./process-items/utils/sorting";
import { setNavigationIds, ensureUniqueLineIds } from "./utils/lineIdUtils";

let apiJson: Crate;
export const processedItems = new Set<number>();
export let PACKAGE_NAME: string;
export function getAPIJson(): Crate {
  return apiJson;
}

/**
 * Processes the root item of the crate and adds its review lines to the code file
 * @param codeFile The code file to add review lines to
 */
function processRootItem(codeFile: CodeFile): void {
  const reviewLines = processItem(apiJson.index[apiJson.root], undefined, "");
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
 * Processes external references and adds them to the code file
 * @param codeFile The code file to add review lines to
 */
function processExternalReferences(codeFile: CodeFile): void {
  if (externalReferencesLines.length > 0) {
    addSectionHeader(codeFile, "External references");

    // Sort the external items by kind (using itemKindOrder) and then by name
    sortExternalItems(externalReferencesLines);

    // Process external item re-exports that aren't already included in modules
    for (let i = 0; i < externalReferencesLines.length; i++) {
      codeFile.ReviewLines.push(externalReferencesLines[i]);
    }
  }
}

/**
 * Builds the code file by processing items and paths from the rustdoc output
 * @returns The built code file
 */
function buildCodeFile(): CodeFile {
  const codeFile: CodeFile = {
    PackageName: apiJson.index[apiJson.root].name || "unknown_root_package_name",
    PackageVersion: apiJson["crate_version"] || "unknown_crate_version",
    // This should not be changed unless you're changing the base rustdoc-types (see its README for details)
    // or only processing code in this package. JSON format v37 is our current baseline because of how APIView migration works.
    ParserVersion: "1.1.1",
    Language: "Rust",
    ReviewLines: [],
  };
  PACKAGE_NAME = codeFile.PackageName;

  processRootItem(codeFile);
  processExternalReferences(codeFile);

  // Apply the new navigation system
  setNavigationIds(codeFile.ReviewLines);

  // Validate uniqueness
  const validation = ensureUniqueLineIds(codeFile.ReviewLines);
  if (!validation.isValid) {
    console.error(`❌ Found ${validation.duplicateCount} duplicate line IDs: ${validation.duplicates.join(', ')}`);
  }

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

  if (apiJson.format_version === 45) {
    // `Path::name` in v37 changed to `Path::path` in v45 and we'll handle that with a separate utility.
    // No reason to warn since that we'll handle.
  } else if (apiJson.format_version !== FORMAT_VERSION) {
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
