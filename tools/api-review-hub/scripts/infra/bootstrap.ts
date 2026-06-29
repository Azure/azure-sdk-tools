import { CosmosClient } from "@azure/cosmos";
import { DefaultAzureCredential } from "@azure/identity";

import type { RepositoryRegistration } from "../../src/models/models.js";
import { loadVariables } from "./variables.js";

const credential = new DefaultAzureCredential();
const repositoryRegistrationsContainerName = "repositoryRegistrations";
const initialRepositoryRegistration: Omit<RepositoryRegistration, "lastUpdated" | "rotationDate"> = {
    repositoryFullName: "tjprescott/azure-sdk-for-python",
    githubRepositoryId: 1281659283,
    githubWebhookId: 646775069,
    webhookSecretKey: "github-webhook-1281659283",
    lastWebhookSecretKey: "github-webhook-1281659283-prev",
    status: "active",
};

async function main(): Promise<void> {
    const variables = await loadVariables();
    const cosmosClient = new CosmosClient({ endpoint: variables.cosmosEndpoint, aadCredentials: credential });
    const container = cosmosClient.database(variables.cosmosDatabaseName).container(repositoryRegistrationsContainerName);
    const registration = buildRepositoryRegistration();

    await container.items.upsert(registration);

    console.log(
        `Bootstrapped repository registration for ${registration.repositoryFullName} ` +
            `(repositoryId=${registration.githubRepositoryId}, webhookId=${registration.githubWebhookId})`,
    );
}

function buildRepositoryRegistration(): RepositoryRegistration & { readonly id: string } {
    const now = new Date();
    const rotationDate = new Date(now);
    rotationDate.setUTCFullYear(rotationDate.getUTCFullYear() + 1);

    return {
        id: String(initialRepositoryRegistration.githubRepositoryId),
        ...initialRepositoryRegistration,
        rotationDate: rotationDate.toISOString(),
        lastUpdated: now.toISOString(),
    };
}

main().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Failed to bootstrap API Review Hub infrastructure data: ${message}`);
    process.exitCode = 1;
});