import { app, InvocationContext, Timer } from "@azure/functions";
import { DefaultAzureCredential, ManagedIdentityCredential } from "@azure/identity";
import { SecretClient } from "@azure/keyvault-secrets";

const ADO_RESOURCE_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default";
const SECRET_NAME = "ado-token";

/**
 * Timer-triggered function that refreshes the Azure DevOps bearer token
 * in Key Vault.
 *
 * The Foundry-hosted QA Bot Agent cannot mint an ADO token directly
 * (ADO doesn't accept its identity as an org member). This function
 * runs under a UAMI that IS an org member, mints a token, and writes
 * it to Key Vault for the agent to consume.
 *
 * Environment variables:
 *   - KEY_VAULT_URL  : e.g. https://xxxx-kv.vault.azure.net
 *   - AZURE_CLIENT_ID: client ID of the user-assigned managed identity
 */
async function refreshAdoToken(_timer: Timer, context: InvocationContext): Promise<void> {
    const kvUrl = process.env.KEY_VAULT_URL;
    if (!kvUrl) {
        context.error("KEY_VAULT_URL is not configured");
        throw new Error("KEY_VAULT_URL environment variable is required");
    }

    const clientId = process.env.AZURE_CLIENT_ID;
    const credential = clientId
        ? new ManagedIdentityCredential({ clientId })
        : new DefaultAzureCredential();

    context.log("Requesting ADO token via managed identity...");
    const tokenResponse = await credential.getToken(ADO_RESOURCE_SCOPE);
    if (!tokenResponse?.token) {
        context.error("Failed to obtain ADO token — empty response");
        throw new Error("ADO token acquisition returned no token");
    }
    context.log("ADO token acquired, expires at %s", new Date(tokenResponse.expiresOnTimestamp).toISOString());

    const secretClient = new SecretClient(kvUrl, credential);
    await secretClient.setSecret(SECRET_NAME, tokenResponse.token);
    context.log("Secret '%s' updated in Key Vault", SECRET_NAME);
}

// Run every 40 minutes so the token (~1 hour validity) stays fresh.
app.timer("adoTokenRefresh", {
    schedule: "0 */40 * * * *",
    handler: refreshAdoToken,
});
