import { execSync } from "child_process";
import { isManagedApiConnection } from "./lib/managed-api-connection.js";
import { enforceProvisionGuard } from "./lib/provision-guard.js";
import { hasWorkflowActions } from "./lib/workflow-definition.js";

const subscriptionId = process.env.AZURE_SUBSCRIPTION_ID ?? "";
const resourceGroup = process.env.AZURE_RESOURCE_GROUP ?? "";

function log(message: string): void {
  console.log(`[logic-app:preprovision] ${message}`);
}

function errorText(error: unknown): string {
  if (error instanceof Error && "stderr" in error) {
    return String((error as Error & { stderr?: unknown }).stderr ?? error.message);
  }
  return error instanceof Error ? error.message : String(error);
}

function isMissingResource(error: unknown): boolean {
  return /ResourceGroupNotFound|ResourceNotFound|could not be found|was not found/i.test(errorText(error));
}

function setEnvironmentValue(name: string, value: boolean): void {
  const text = String(value);
  execSync(`azd env set ${name} ${text}`, { stdio: "inherit" });
  process.env[name] = text;
}

function preserveTeamsConnection(): void {
  let connectionName = process.env.TEAMS_CONNECTION_NAME?.trim();
  if (!resourceGroup || !subscriptionId) {
    setEnvironmentValue("LOGIC_APP_WORKFLOW_ENABLED", false);
    log("Teams connection is not known yet; the first provision will create it.");
    return;
  }

  if (!connectionName) {
    try {
      const raw = execSync(
        `az resource list --resource-group "${resourceGroup}" ` +
          `--subscription "${subscriptionId}" --resource-type Microsoft.Web/connections ` +
          `--query "[].name" -o json`,
        { encoding: "utf8" },
      );
      const candidateNames = JSON.parse(raw) as string[];
      const names = candidateNames.filter((candidateName) => {
        try {
          const apiId = execSync(
            `az resource show --resource-type Microsoft.Web/connections ` +
              `--subscription "${subscriptionId}" -g "${resourceGroup}" ` +
              `-n "${candidateName}" --query "properties.api.id" -o tsv`,
            { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
          ).trim();
          return isManagedApiConnection(apiId, "teams");
        } catch (error) {
          if (!isMissingResource(error)) throw error;
          return false;
        }
      });
      if (names.length > 1) {
        throw new Error(`Multiple Teams API connections found: ${names.join(", ")}`);
      }
      connectionName = names[0];
      if (connectionName) {
        execSync(`azd env set TEAMS_CONNECTION_NAME "${connectionName}"`, { stdio: "inherit" });
        process.env.TEAMS_CONNECTION_NAME = connectionName;
        log(`Discovered existing Teams API connection '${connectionName}'.`);
      }
    } catch (error) {
      if (error instanceof Error && error.message.startsWith("Multiple Teams")) throw error;
      if (!isMissingResource(error)) throw error;
    }
  }

  if (!connectionName) {
    setEnvironmentValue("CREATE_TEAMS_CONNECTION", true);
    setEnvironmentValue("LOGIC_APP_WORKFLOW_ENABLED", false);
    log("No Teams API connection exists; the first provision will create it.");
    return;
  }

  let status = "";
  try {
    status = execSync(
      `az resource show --resource-type Microsoft.Web/connections ` +
        `--subscription "${subscriptionId}" -g "${resourceGroup}" -n "${connectionName}" ` +
        `--query "properties.statuses[0].status" -o tsv`,
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
    ).trim();
  } catch (error) {
    if (!isMissingResource(error)) throw error;
  }

  const createConnection = status !== "Connected";
  setEnvironmentValue("CREATE_TEAMS_CONNECTION", createConnection);
  setEnvironmentValue("LOGIC_APP_WORKFLOW_ENABLED", !createConnection);
  log(
    status === "Connected"
      ? `Preserving connected Teams OAuth connection '${connectionName}'.`
      : `Teams connection status is '${status || "not-found"}'; Bicep will create or repair it.`,
  );
}

function preserveWorkflowDefinition(): void {
  if (!resourceGroup || !subscriptionId) {
    process.env.INCLUDE_LOGIC_APP_WORKFLOW_DEFINITION = "false";
    log("Resource group is not known yet; the first provision will deploy the workflow shell.");
    return;
  }

  const configuredName = process.env.LOGIC_APP_WORKFLOW_NAME?.trim();
  let workflowNames: string[] = [];
  try {
    const query = configuredName
      ? `name=='${configuredName}'`
      : "starts_with(name, 'azuresdkqabot-logicapp-')";
    const raw = execSync(
      `az resource list --resource-group "${resourceGroup}" ` +
        `--subscription "${subscriptionId}" --resource-type Microsoft.Logic/workflows ` +
        `--query "[?${query}].name" -o json`,
      { encoding: "utf8" },
    );
    workflowNames = JSON.parse(raw);
  } catch (error) {
    if (!isMissingResource(error)) throw error;
  }

  const includeDefinition = workflowNames.some((workflowName) => {
    try {
      const raw = execSync(
        `az resource show --resource-type Microsoft.Logic/workflows ` +
          `--subscription "${subscriptionId}" -g "${resourceGroup}" ` +
          `-n "${workflowName}" --api-version 2019-05-01 ` +
          `--query "properties.definition.actions" -o json`,
        { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
      );
      return hasWorkflowActions(JSON.parse(raw || "null"));
    } catch (error) {
      if (!isMissingResource(error)) throw error;
      return false;
    }
  });
  setEnvironmentValue("INCLUDE_LOGIC_APP_WORKFLOW_DEFINITION", includeDefinition);
  log(
    includeDefinition
      ? `Preserving the deployed workflow definition for ${workflowNames.join(", ")}.`
      : workflowNames.length > 0
        ? `Existing workflow ${workflowNames.join(", ")} is an empty shell; preserving the shell until Function App postdeploy.`
        : "No workflow exists yet; Bicep will deploy the workflow shell.",
  );
}

try {
  enforceProvisionGuard();
  preserveTeamsConnection();
  preserveWorkflowDefinition();
} catch (error) {
  console.error(`[logic-app:preprovision] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
}