import { parseArgs } from "node:util";
import { Logger, printUsage, printVersion } from "./log.js";
import * as path from "node:path";
import { knownLanguages, languageAliases } from "./languageSettings.js";

export interface Options {
  debug: boolean;
  command: string;
  emitter: string | undefined;
  mainFile?: string;
  outputDir: string;
  noCleanup: boolean;
}

export async function getOptions(): Promise<Options> {
  const { values, positionals } = parseArgs({
    allowPositionals: true,
    options: {
      help: {
        type: "boolean",
        short: "h",
      },
      version: {
        type: "boolean",
        short: "v",
      },
      debug: {
        type: "boolean",
        short: "d",
      },
      emitter: {
        type: "string",
        short: "e",
      },
      mainFile: {
        type: "string",
        short: "m",
      },
      ["no-cleanup"]: {
        type: "boolean",
      },
    },
  });
  if (values.help) {
    printUsage();
    process.exit(0);
  }

  if (values.version) {
    await printVersion();
    process.exit(0);
  }

  if (values.emitter) {
    let emitter = values.emitter.toLowerCase();
    if (emitter in languageAliases) {
      emitter = languageAliases[emitter]!;
    }
    if (!(knownLanguages as readonly string[]).includes(emitter)) {
      Logger.error(`Unknown language ${values.emitter}`);
      Logger.error(`Valid languages are: ${knownLanguages.join(", ")}`);
      printUsage();
      process.exit(1);
    }
  }

  if (positionals.length === 0) {
    Logger.error("Command is required");
    printUsage();
    process.exit(1);
  }

  if (positionals[0] !== "sync" && positionals[0] !== "generate" && positionals[0] !== "update") {
    Logger.error(`Unknown command ${positionals[0]}`);
    printUsage();
    process.exit(1);
  }

  // By default, assume that the command is run from the output directory
  var outputDir = ".";
  if (positionals[1] !== undefined) {
    outputDir = path.resolve(path.normalize(positionals[1]));
  } else {
    outputDir = path.resolve(path.normalize(outputDir));
  }

  return {
    debug: values.debug ?? false,
    command: positionals[0],
    emitter: values.emitter,
    mainFile: values.mainFile,
    noCleanup: values["no-cleanup"] ?? false,
    outputDir: outputDir,
  };
}
