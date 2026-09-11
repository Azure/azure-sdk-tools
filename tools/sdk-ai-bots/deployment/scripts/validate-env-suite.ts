import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { parseArgs } from "node:util";
import { parse } from "yaml";

import {
  DEFAULT_SUITE_PATH,
  ENVIRONMENT_NAMES,
  getEnvironmentConfig,
  loadEnvironmentSuite,
  type EnvironmentName,
  type EnvironmentSuite,
} from "../hooks/lib/env-suite.js";
import {
  isDirectExecution,
  parseEnvironmentName,
  runCli,
} from "./lib/cli.js";

const REQUIRED_KEYS = [
  "subscriptionId",
  "tenantId",
  "serverApplicationClientId",
  "serverApplicationIdUri",
  "resourceGroupPrefix",
  "location",
  "keyVaultName",
  "appConfigName",
  "containerRegistryName",
  "teamsGroupId",
  "localDeployAllowed",
  "chatbotEvolutionAgentEnabled",
] as const;

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const APPLICATION_ID_URI_PATTERN = /^(api|https):\/\/[^/].*[^/]$/;
const DEPLOYABLE_COMPONENTS = ["frontend", "function-app", "agent-server"] as const;

interface ChannelConfig {
  default?: { tenant?: string };
  channels?: Array<{ id?: string; tenant?: string }>;
}

interface TenantConfig {
  tenants?: Array<{ tenant?: string; channel_link?: string }>;
}

export interface ValidateEnvironmentSuiteOptions {
  suite: EnvironmentSuite;
  environments: readonly EnvironmentName[];
  configRoot: string;
}

function isMissing(value: unknown): boolean {
  return value === undefined || value === null || value === "";
}

function hasPlaceholder(value: unknown): boolean {
  return typeof value === "string" && value.startsWith("REPLACE_WITH_");
}

function loadYaml<T>(filePath: string): T {
  return parse(readFileSync(filePath, "utf8")) as T;
}

export function collectEnvironmentSuiteErrors(
  options: ValidateEnvironmentSuiteOptions,
): string[] {
  const errors: string[] = [];

  for (const environmentName of options.environments) {
    const environment = getEnvironmentConfig(options.suite, environmentName);
    for (const key of REQUIRED_KEYS) {
      const value = environment[key];
      if (isMissing(value)) {
        errors.push(`[${environmentName}] missing key '${key}'`);
      } else if (hasPlaceholder(value)) {
        errors.push(`[${environmentName}] '${key}' still contains placeholder '${value}'`);
      }
    }

    if (!GUID_PATTERN.test(environment.subscriptionId ?? "")) {
      errors.push(
        `[${environmentName}] subscriptionId '${environment.subscriptionId}' is not GUID-shaped`,
      );
    }
    if (!GUID_PATTERN.test(environment.serverApplicationClientId ?? "")) {
      errors.push(
        `[${environmentName}] serverApplicationClientId '${environment.serverApplicationClientId}' is not GUID-shaped`,
      );
    }
    if (!APPLICATION_ID_URI_PATTERN.test(environment.serverApplicationIdUri ?? "")) {
      errors.push(
        `[${environmentName}] serverApplicationIdUri '${environment.serverApplicationIdUri}' must be an api:// or https:// URI without a trailing slash`,
      );
    }
    if (typeof environment.chatbotEvolutionAgentEnabled !== "boolean") {
      errors.push(
        `[${environmentName}] chatbotEvolutionAgentEnabled must be true or false`,
      );
    }
    if (typeof environment.localDeployAllowed !== "boolean") {
      errors.push(`[${environmentName}] localDeployAllowed must be true or false`);
    }

    const channelIds = environment.teamsChannelIds ?? [];
    if (
      channelIds.length === 0 ||
      channelIds.some((id) => !id || hasPlaceholder(id))
    ) {
      errors.push(
        `[${environmentName}] teamsChannelIds is empty or contains a placeholder`,
      );
    }

    if (environment.chatbotEvolutionAgentEnabled && !environment.candidateEnvironment) {
      errors.push(
        `[${environmentName}] chatbot evolution is enabled but candidateEnvironment is missing`,
      );
    } else if (
      environment.candidateEnvironment &&
      !options.suite.environments[environment.candidateEnvironment]
    ) {
      errors.push(
        `[${environmentName}] candidateEnvironment '${environment.candidateEnvironment}' is not declared`,
      );
    }

    const channelConfigPath = resolve(options.configRoot, environmentName, "channel.yaml");
    const tenantConfigPath = resolve(options.configRoot, environmentName, "tenant.yaml");
    if (!existsSync(channelConfigPath)) {
      errors.push(`[${environmentName}] config/${environmentName}/channel.yaml is missing`);
      continue;
    }
    if (!existsSync(tenantConfigPath)) {
      errors.push(`[${environmentName}] config/${environmentName}/tenant.yaml is missing`);
      continue;
    }

    const channelConfig = loadYaml<ChannelConfig>(channelConfigPath);
    const tenantConfig = loadYaml<TenantConfig>(tenantConfigPath);
    const routeIds = (channelConfig.channels ?? [])
      .map((channel) => channel.id)
      .filter((id): id is string => !!id);
    for (const channelId of channelIds) {
      if (!routeIds.includes(channelId)) {
        errors.push(
          `[${environmentName}] monitored Teams channel '${channelId}' has no route in config/${environmentName}/channel.yaml`,
        );
      }
    }
    for (const routeId of routeIds) {
      if (!channelIds.includes(routeId)) {
        errors.push(
          `[${environmentName}] config/${environmentName}/channel.yaml contains unmonitored Teams channel '${routeId}'`,
        );
      }
    }

    const routeTenants = new Set([
      channelConfig.default?.tenant,
      ...(channelConfig.channels ?? []).map((channel) => channel.tenant),
    ].filter((tenant): tenant is string => !!tenant));
    const definedTenants = new Set(
      (tenantConfig.tenants ?? [])
        .map((tenant) => tenant.tenant)
        .filter((tenant): tenant is string => !!tenant),
    );
    for (const routeTenant of routeTenants) {
      if (!definedTenants.has(routeTenant)) {
        errors.push(
          `[${environmentName}] route tenant '${routeTenant}' is not defined in config/${environmentName}/tenant.yaml`,
        );
      }
    }

    for (const tenant of tenantConfig.tenants ?? []) {
      if (!tenant.channel_link) continue;
      let groupId = "";
      try {
        groupId = new URL(tenant.channel_link).searchParams.get("groupId") ?? "";
      } catch {
        errors.push(
          `[${environmentName}] tenant channel link is not a valid URL: '${tenant.channel_link}'`,
        );
        continue;
      }
      if (groupId !== environment.teamsGroupId) {
        errors.push(
          `[${environmentName}] tenant channel link targets a different Teams group: '${tenant.channel_link}'`,
        );
      }
    }
  }

  for (const componentName of DEPLOYABLE_COMPONENTS) {
    const component = options.suite.components[componentName];
    if (!component) {
      errors.push(`[components] missing '${componentName}'`);
      continue;
    }
    for (const key of ["healthPath"] as const) {
      if (!component[key] || hasPlaceholder(component[key])) {
        errors.push(`[components.${componentName}] missing or invalid '${key}'`);
      }
    }
  }

  return errors;
}

export function validateEnvironmentSuite(
  suitePath: string,
  environments: readonly EnvironmentName[],
): string[] {
  return collectEnvironmentSuiteErrors({
    suite: loadEnvironmentSuite(suitePath),
    environments,
    configRoot: resolve(dirname(suitePath), "../..", "config"),
  });
}

function printUsage(): void {
  console.log(
    "Usage: npm run validate-env-suite -- [--environment <dev|preview|prod>]... [--suite <path>]",
  );
}

function main(): void {
  const { values } = parseArgs({
    options: {
      environment: { type: "string", short: "e", multiple: true },
      suite: { type: "string" },
      help: { type: "boolean", short: "h" },
    },
    strict: true,
  });
  if (values.help) {
    printUsage();
    return;
  }

  const environments = values.environment?.map(parseEnvironmentName) ?? [...ENVIRONMENT_NAMES];
  const suitePath = resolve(values.suite ?? DEFAULT_SUITE_PATH);
  const errors = validateEnvironmentSuite(suitePath, environments);
  if (errors.length > 0) {
    throw new Error(
      `environment-suite.yaml validation FAILED:\n${errors.map((error) => `  - ${error}`).join("\n")}`,
    );
  }
  console.log("environment-suite.yaml validation passed.");
}

if (isDirectExecution(import.meta.url)) runCli(main);