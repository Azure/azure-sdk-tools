import { parseArgs } from "node:util";
import { Logger, printUsage, printVersion } from "./log.js";
import * as path from "node:path";
import { knownLanguages, languageAliases } from "./languageSettings.js";

export interface Options {
  debug: boolean;
  emitter: string;
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

  if (!values.emitter) {
    Logger.error("Option --emitter/-e is required");
    printUsage();
    process.exit(1);
  }

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

  if (positionals.length === 0 || !positionals[0]) {
    Logger.error("Output directory is required");
    printUsage();
    process.exit(1);
  }
  const outputDir = path.resolve(path.normalize(positionals[0]));

  return {
    debug: values.debug ?? false,
    emitter: values.emitter,
    mainFile: values.mainFile,
    noCleanup: values["no-cleanup"] ?? false,
    outputDir: outputDir,
  };
}
