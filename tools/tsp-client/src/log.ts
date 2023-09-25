import chalk from "chalk";
import { getPackageVersion } from "./npm.js";

const logSink = {
  log: console.log,
  info: console.info,
  warn: console.warn,
  error: console.error,
  debug: (..._args: unknown[]) => {
    /* no-op */
  },
};

export interface Logger {
  info(message: string): void;
  warn(message: string): void;
  error(message: string): void;
  debug(message: string): void;
  success(message: string): void;
  (message: string): void;
}

function createLogger(): Logger {
  const direct = (...values: unknown[]) => logSink.log(chalk.reset(...values));
  direct.info = (...values: unknown[]) => logSink.info(chalk.blueBright(...values));
  direct.warn = (...values: unknown[]) => logSink.warn(chalk.yellow(...values));
  direct.error = (...values: unknown[]) => logSink.error(chalk.red(...values));
  direct.debug = (...values: unknown[]) => logSink.debug(chalk.magenta(...values));
  direct.success = (...values: unknown[]) => logSink.info(chalk.green(...values));

  return direct;
}

export function enableDebug(): void {
  logSink.debug = console.debug;
}

export const Logger = createLogger();

const bannerText = `
888                                      888 d8b                   888    
888                                      888 Y8P                   888    
888                                      888                       888    
888888 .d8888b  88888b.          .d8888b 888 888  .d88b.  88888b.  888888 
888    88K      888 "88b        d88P"    888 888 d8P  Y8b 888 "88b 888    
888    "Y8888b. 888  888 888888 888      888 888 88888888 888  888 888    
Y88b.       X88 888 d88P        Y88b.    888 888 Y8b.     888  888 Y88b.  
 "Y888  88888P' 88888P"          "Y8888P 888 888  "Y8888  888  888  "Y888 
                888                                                       
                888                                                       
                888                                                       
`;
export function printBanner() {
  Logger.info(bannerText);
}

const usageText = `
Usage: tsp-client [options]

Generate from a tsp file using --mainFile or use tsp-location.yaml inside 
the outputDir.

Positionals:
  init        Initialize the SDK project folder from a tspconfig.yaml   [string]
  sync        Sync tsp files using tsp-location.yaml                    [string]
  generate    Generate from a tsp project                               [string]
  update      Sync and generate from a tsp project                      [string]

Options:
  -d, --debug      Enable debug logging                                [boolean]
  -e, --emitter    Which language emitter to use             [required] [string]
                  [choices: "csharp", "java", "javascript", "python", "openapi"]
  -m, --mainFile   The url of the main tsp file to generate from        [string]
      --noCleanup  Don't clean up the temp directory after generation  [boolean]
  -h, --help       Show help                                           [boolean]
  -v, --version    Show version number                                 [boolean]
  -o, --outputDir  The output directory for the emitter
`;
export function printUsage() {
  Logger(usageText);
}

export async function printVersion() {
  const version = await getPackageVersion();
  Logger(`tsp-client version: ${version}`);
}
