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
Usage: tsp-client <command> [options]

Use one of the supported commands to get started generating clients from a TypeSpec project.
This tool will default to using your current working directory to generate clients in and will
use it to look for relevant configuration files. To specify a different directory, use
the -o or --output-dir option.

Commands:
  init        Initialize the SDK project folder from a tspconfig.yaml   [string]
  sync        Sync TypeSpec project specified in tsp-location.yaml      [string]
  generate    Generate from a TypeSpec project                          [string]
  update      Sync and generate from a TypeSpec project                 [string]
  convert     Convert a swagger specification to TypeSpec               [string]

Options:
  -c, --tsp-config          The tspconfig.yaml file to use                      [string]
  --commit                  Commit to be used for project init or update        [string]
  -d, --debug               Enable debug logging                                [boolean]
  --emitter-options         The options to pass to the emitter                  [string]
  -h, --help                Show help                                           [boolean]
  --local-spec-repo         Path to local repository with the TypeSpec project  [string]
  --save-inputs             Don't clean up the temp directory after generation  [boolean]
  --skip-sync-and-generate  Skip sync and generate during project init          [boolean]
  --swagger-readme          Path or url to swagger readme file                  [string]
  -o, --output-dir          Specify an alternate output directory for the 
                            generated files. Default is your current directory  [string]
  --repo                    Repository where the project is defined for init 
                            or update                                           [string]
  -v, --version             Show version number                                 [boolean]
`;
export function printUsage() {
  Logger(usageText);
}

export async function printVersion() {
  const version = await getPackageVersion();
  Logger(`tsp-client version: ${version}`);
}
