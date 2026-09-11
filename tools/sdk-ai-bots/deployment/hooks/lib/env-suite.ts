/** Shared, typed access to the deployment environment contract. */

import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";
import { parse } from "yaml";

// hooks/lib/env-suite.ts → ../../infra/environments/environment-suite.yaml
export const DEFAULT_SUITE_PATH = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../infra/environments/environment-suite.yaml",
);
export const DEFAULT_PROJECT_PATH = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../azure.yaml",
);

export const ENVIRONMENT_NAMES = ["dev", "preview", "prod"] as const;
export type EnvironmentName = (typeof ENVIRONMENT_NAMES)[number];

export interface EnvironmentConfig {
  subscriptionId: string;
  tenantId: string;
  serverApplicationClientId: string;
  serverApplicationIdUri: string;
  resourceGroupPrefix: string;
  keyVaultName: string;
  appConfigName: string;
  containerRegistryName: string;
  frontendSiteName: string;
  agentServerSiteName: string;
  functionAppName: string;
  teamsAppId: string;
  teamsGroupId: string;
  teamsChannelIds: string[];
  location: string;
  aiLocation: string;
  cosmosDbLocation: string;
  chatbotEvolutionAgentEnabled: boolean;
  candidateEnvironment?: string;
  bicepOverrides?: Record<string, string>;
  manageAuthorizationResources: boolean;
  localDeployAllowed: boolean;
  [key: string]: unknown;
}

export interface ComponentConfig {
  healthPath: string;
  [key: string]: unknown;
}

export interface EnvironmentSuite {
  environments: Record<string, EnvironmentConfig>;
  components: Record<string, ComponentConfig>;
}

export type ServiceImageRepositories = Record<
  "agent" | "agent-server" | "frontend" | "function-app",
  string
>;

function isRecord(value: unknown): value is Record<string, unknown> {
  return !!value && typeof value === "object" && !Array.isArray(value);
}

export function loadEnvironmentSuite(
  suitePath: string = DEFAULT_SUITE_PATH,
): EnvironmentSuite {
  if (!existsSync(suitePath)) {
    throw new Error(`environment-suite.yaml not found at ${suitePath}`);
  }

  const parsed: unknown = parse(readFileSync(suitePath, "utf8"));
  if (!isRecord(parsed) || !isRecord(parsed.environments) || !isRecord(parsed.components)) {
    throw new Error(`${suitePath} must define object maps named environments and components.`);
  }

  return parsed as unknown as EnvironmentSuite;
}

export function loadServiceImageRepositories(
  environmentName: string,
  projectPath: string = DEFAULT_PROJECT_PATH,
): ServiceImageRepositories {
  if (!existsSync(projectPath)) {
    throw new Error(`azure.yaml not found at ${projectPath}`);
  }
  const parsed: unknown = parse(readFileSync(projectPath, "utf8"));
  if (!isRecord(parsed) || !isRecord(parsed.services)) {
    throw new Error(`${projectPath} must define a services map.`);
  }

  const repositories = {} as ServiceImageRepositories;
  for (const serviceName of ["agent", "agent-server", "frontend", "function-app"] as const) {
    const service = parsed.services[serviceName];
    const docker = isRecord(service) ? service.docker : undefined;
    const image = isRecord(docker) && typeof docker.image === "string"
      ? docker.image.replaceAll("${AZURE_ENV_NAME}", environmentName).trim()
      : "";
    if (!image || image.includes("${")) {
      throw new Error(
        `services.${serviceName}.docker.image must resolve from ${projectPath}.`,
      );
    }
    repositories[serviceName] = image;
  }
  return repositories;
}

export function getEnvironmentConfig(
  suite: EnvironmentSuite,
  environmentName: string,
): EnvironmentConfig {
  const environment = suite.environments[environmentName];
  if (!environment) {
    throw new Error(
      `Environment '${environmentName}' is not declared. Declared: ${Object.keys(suite.environments).join(", ")}.`,
    );
  }
  return environment;
}

export function getComponentConfig(
  suite: EnvironmentSuite,
  componentName: string,
): ComponentConfig {
  const component = suite.components[componentName];
  if (!component) {
    throw new Error(
      `Component '${componentName}' is not declared. Declared: ${Object.keys(suite.components).join(", ")}.`,
    );
  }
  return component;
}

function scalarToString(value: unknown): string | undefined {
  if (typeof value === "string") return value || undefined;
  if (typeof value === "boolean" || typeof value === "number") return String(value);
  return undefined;
}

/** Compatibility accessor retained for existing hooks. */
export function getEnvSuiteValue(
  environmentName: string,
  key: string,
  suitePath: string = DEFAULT_SUITE_PATH,
): string | undefined {
  try {
    const environment = getEnvironmentConfig(loadEnvironmentSuite(suitePath), environmentName);
    return scalarToString(environment[key]);
  } catch {
    return undefined;
  }
}

/** Compatibility array accessor retained for existing hooks. */
export function getEnvSuiteValues(
  environmentName: string,
  key: string,
  suitePath: string = DEFAULT_SUITE_PATH,
): string[] {
  try {
    const environment = getEnvironmentConfig(loadEnvironmentSuite(suitePath), environmentName);
    const value = environment[key];
    return Array.isArray(value)
      ? value.map(scalarToString).filter((item): item is string => !!item)
      : [];
  } catch {
    return [];
  }
}

const OVERRIDE_ALIAS_KEYS = new Set([
  "MANAGED_IDENTITY_NAME",
  "ACTION_GROUP_NAME",
  "KEY_VAULT_NAME",
  "APP_CONFIG_NAME",
  "SEARCH_SERVICE_NAME",
  "CONTAINER_REGISTRY_NAME",
  "STORAGE_ACCOUNT_NAME",
  "COSMOS_DB_ACCOUNT_NAME",
  "AI_RESOURCE_NAME",
  "AI_PROJECT_NAME",
  "AGENT_SERVER_SITE_NAME",
  "FUNCTION_APP_NAME",
  "INTEGRATION_ACCOUNT_NAME",
  "TEAMS_CONNECTION_NAME",
  "DOCUMENT_DB_CONNECTION_NAME",
  "LOGIC_APP_WORKFLOW_NAME",
  "LOGIC_APP_ALERT_NAME",
]);

export function buildAzdEnvironmentValues(
  suite: EnvironmentSuite,
  environmentName: string,
  imageRepositories: ServiceImageRepositories = loadServiceImageRepositories(environmentName),
): Record<string, string> {
  const environment = getEnvironmentConfig(suite, environmentName);
  if (!environment.location) {
    throw new Error(`Environment '${environmentName}' must define a location.`);
  }

  const values: Record<string, string> = {
    AZD_IMAGE_TAG: environmentName,
    AZURE_SUBSCRIPTION_ID: environment.subscriptionId,
    AZURE_TENANT_ID: environment.tenantId,
    SERVER_APPLICATION_CLIENT_ID: environment.serverApplicationClientId,
    SERVER_APPLICATION_ID_URI: environment.serverApplicationIdUri,
    AZURE_RESOURCE_GROUP: environment.resourceGroupPrefix,
    AZURE_LOCATION: environment.location,
    AZURE_AI_LOCATION: environment.aiLocation,
    COSMOS_DB_LOCATION: environment.cosmosDbLocation,
    CHATBOT_EVOLUTION_AGENT_ENABLED: String(environment.chatbotEvolutionAgentEnabled),
    MANAGE_AUTHORIZATION_RESOURCES: String(environment.manageAuthorizationResources),
    FRONTEND_SITE_NAME: environment.frontendSiteName,
    AGENT_SERVER_SITE_NAME: environment.agentServerSiteName,
    AGENT_SERVER_SITE_NAME_OVERRIDE: environment.agentServerSiteName,
    FUNCTION_APP_NAME: environment.functionAppName,
    FUNCTION_APP_NAME_OVERRIDE: environment.functionAppName,
    CONTAINER_REGISTRY_NAME: environment.containerRegistryName,
    CONTAINER_REGISTRY_NAME_OVERRIDE: environment.containerRegistryName,
    KEY_VAULT_NAME: environment.keyVaultName,
    KEY_VAULT_NAME_OVERRIDE: environment.keyVaultName,
    APP_CONFIG_NAME: environment.appConfigName,
    APP_CONFIG_NAME_OVERRIDE: environment.appConfigName,
    AZURE_APPCONFIG_ENDPOINT: `https://${environment.appConfigName}.azconfig.io`,
    AGENT_IMAGE_REPOSITORY: imageRepositories.agent,
    FRONTEND_IMAGE_REPOSITORY: `${imageRepositories.frontend}:${environmentName}`,
    AGENT_SERVER_IMAGE_REPOSITORY: `${imageRepositories["agent-server"]}:${environmentName}`,
    FUNCTION_IMAGE_REPOSITORY: `${imageRepositories["function-app"]}:${environmentName}`,
    TEAMS_GROUP_ID: environment.teamsGroupId,
    TEAMS_CHANNEL_IDS: environment.teamsChannelIds.join(","),
  };

  if (environment.candidateEnvironment) {
    const candidate = getEnvironmentConfig(suite, environment.candidateEnvironment);
    values.CANDIDATE_APPCONFIG_ENDPOINT = `https://${candidate.appConfigName}.azconfig.io`;
  }

  for (const [key, value] of Object.entries(environment.bicepOverrides ?? {})) {
    values[key] = String(value);
    if (OVERRIDE_ALIAS_KEYS.has(key)) values[`${key}_OVERRIDE`] = String(value);
  }

  return values;
}
