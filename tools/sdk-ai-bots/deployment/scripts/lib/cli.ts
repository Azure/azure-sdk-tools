import { spawnSync } from "node:child_process";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

import {
  ENVIRONMENT_NAMES,
  type EnvironmentName,
} from "../../hooks/lib/env-suite.js";

export interface RunCommandOptions {
  cwd?: string;
  env?: NodeJS.ProcessEnv;
  capture?: boolean;
}

export type CommandRunner = (
  command: string,
  args: readonly string[],
  options?: RunCommandOptions,
) => string;

export const runCommand: CommandRunner = (command, args, options = {}) => {
  const result = spawnSync(command, args, {
    cwd: options.cwd,
    env: { ...process.env, ...options.env },
    encoding: "utf8",
    maxBuffer: 100 * 1024 * 1024,
    stdio: options.capture ? ["ignore", "pipe", "pipe"] : "inherit",
  });

  if (result.error) throw result.error;
  if (result.status !== 0) {
    const detail = result.stderr?.trim();
    throw new Error(
      `${command} ${args.join(" ")} failed with exit code ${result.status}` +
        (detail ? `: ${detail}` : "."),
    );
  }
  return result.stdout?.trim() ?? "";
};

export function parseAzdEnvironmentNames(raw: string): string[] {
  const parsed = JSON.parse(raw) as unknown;
  if (!Array.isArray(parsed)) {
    throw new Error("azd env list returned an unexpected JSON shape.");
  }
  return parsed
    .map((item) => {
      if (!item || typeof item !== "object") return undefined;
      const record = item as Record<string, unknown>;
      const name = record.Name ?? record.name;
      return typeof name === "string" ? name : undefined;
    })
    .filter((name): name is string => !!name);
}

export function parseEnvironmentName(value: string): EnvironmentName {
  if (!ENVIRONMENT_NAMES.includes(value as EnvironmentName)) {
    throw new Error(
      `Invalid environment '${value}'. Expected one of: ${ENVIRONMENT_NAMES.join(", ")}.`,
    );
  }
  return value as EnvironmentName;
}

export function parsePositiveInteger(value: string, option: string): number {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new Error(`${option} must be a positive integer.`);
  }
  return parsed;
}

export function isDirectExecution(moduleUrl: string): boolean {
  return !!process.argv[1] && resolve(process.argv[1]) === fileURLToPath(moduleUrl);
}

export function runCli(main: () => void | Promise<void>): void {
  Promise.resolve()
    .then(main)
    .catch((error: unknown) => {
      console.error(error instanceof Error ? error.message : String(error));
      process.exitCode = 1;
    });
}