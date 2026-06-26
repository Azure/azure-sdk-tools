import { randomUUID } from "node:crypto";

import { DefaultAzureCredential } from "@azure/identity";
import { ResourceManagementClient } from "@azure/arm-resources";

import { type CosmosContainerConfig, loadVariables, type Variables } from "./variables.js";

type ArmExpression = string;
type ArmResource = Record<string, unknown>;
type ArmTemplate = Record<string, unknown>;

const credential = new DefaultAzureCredential();

const roleDefinitionIds = {
    appConfigurationDataOwner: "5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b",
    appConfigurationDataReader: "516239f1-63e1-4d78-a4de-a74fb236a071",
    keyVaultSecretsOfficer: "b86a8fe4-44ce-4948-aee5-eccb2c155cd7",
    keyVaultSecretsUser: "4633458b-17de-408a-b874-0445c86b69e6",
} as const;

async function main(): Promise<void> {
    const variables = await loadVariables();
    const resourceClient = new ResourceManagementClient(credential, variables.subscriptionId);

    console.log(`Creating API Review Hub resources for ENVIRONMENT_NAME=${variables.environmentName}`);
    await checkCredential();
    await createOrReuseResourceGroup(resourceClient, variables);
    await deployResources(resourceClient, variables);
}

async function checkCredential(): Promise<void> {
    try {
        await credential.getToken("https://management.azure.com/.default");
    } catch (error) {
        throw new Error("Unable to get an Azure management token. Run `az login` and try again.", { cause: error });
    }
}

async function createOrReuseResourceGroup(client: ResourceManagementClient, variables: Variables): Promise<void> {
    try {
        await client.resourceGroups.get(variables.resourceGroupName);
        console.log(`Using existing resource group: ${variables.resourceGroupName}`);
    } catch (error) {
        if (!isNotFound(error)) {
            throw error;
        }

        console.log(`Creating resource group: ${variables.resourceGroupName}`);
        await client.resourceGroups.createOrUpdate(variables.resourceGroupName, {
            location: variables.location,
            tags: {
                DoNotDelete: "true",
                Environment: variables.environmentName,
                Service: "api-review-hub",
            },
        });
        console.log(`Created resource group: ${variables.resourceGroupName}`);
    }
}

async function deployResources(client: ResourceManagementClient, variables: Variables): Promise<void> {
    const deploymentName = `api-review-hub-${variables.environmentName}-${randomUUID()}`;

    console.log(`Starting ARM deployment: ${deploymentName}`);
    const poller = await client.deployments.beginCreateOrUpdate(variables.resourceGroupName, deploymentName, {
        properties: {
            mode: "Incremental",
            parameters: buildParameters(variables),
            template: buildTemplate(variables),
        },
    });

    await poller.pollUntilDone();
    console.log(`Completed ARM deployment: ${deploymentName}`);
}

function buildParameters(variables: Variables): Record<string, { value: unknown }> {
    return {
        appConfigurationName: { value: variables.appConfigurationName },
        appServicePlanName: { value: variables.appServicePlanName },
        applicationInsightsName: { value: variables.applicationInsightsName },
        assigneeObjectId: { value: variables.assigneeObjectId ?? "" },
        cosmosAccountName: { value: variables.cosmosAccountName },
        cosmosContainers: { value: variables.cosmosContainers },
        cosmosDatabaseName: { value: variables.cosmosDatabaseName },
        createProductionSlot: { value: variables.environmentName === "production" },
        environmentName: { value: variables.environmentName },
        keyVaultName: { value: variables.keyVaultName },
        location: { value: variables.location },
        productionSlotName: { value: variables.productionSlotName },
        tenantId: { value: variables.tenantId },
        webAppName: { value: variables.webAppName },
    };
}

function buildTemplate(variables: Variables): ArmTemplate {
    const resources: ArmResource[] = [
        appServicePlanResource(),
        applicationInsightsResource(),
        keyVaultResource(),
        appConfigurationResource(),
        cosmosAccountResource(),
        cosmosDatabaseResource(),
        ...variables.cosmosContainers.map((container) => cosmosContainerResource(container)),
        webAppResource(),
        productionSlotResource(),
        cosmosDataContributorAssignmentForWebApp(),
        appConfigurationDataReaderAssignmentForWebApp(),
        keyVaultSecretsUserAssignmentForWebApp(),
        cosmosDataContributorAssignmentForAssignee(),
        appConfigurationDataOwnerAssignmentForAssignee(),
        keyVaultSecretsOfficerAssignmentForAssignee(),
    ];

    return {
        $schema: "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
        contentVersion: "1.0.0.0",
        parameters: {
            appConfigurationName: { type: "string" },
            appServicePlanName: { type: "string" },
            applicationInsightsName: { type: "string" },
            assigneeObjectId: { type: "string", defaultValue: "" },
            cosmosAccountName: { type: "string" },
            cosmosContainers: { type: "array" },
            cosmosDatabaseName: { type: "string" },
            createProductionSlot: { type: "bool" },
            environmentName: { type: "string" },
            keyVaultName: { type: "string" },
            location: { type: "string" },
            productionSlotName: { type: "string" },
            tenantId: { type: "string" },
            webAppName: { type: "string" },
        },
        variables: {
            appConfigurationResourceId: "[resourceId('Microsoft.AppConfiguration/configurationStores', parameters('appConfigurationName'))]",
            appServicePlanResourceId: "[resourceId('Microsoft.Web/serverfarms', parameters('appServicePlanName'))]",
            applicationInsightsResourceId: "[resourceId('Microsoft.Insights/components', parameters('applicationInsightsName'))]",
            cosmosAccountResourceId: "[resourceId('Microsoft.DocumentDB/databaseAccounts', parameters('cosmosAccountName'))]",
            cosmosDataContributorRoleDefinitionId:
                "[resourceId('Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions', parameters('cosmosAccountName'), '00000000-0000-0000-0000-000000000002')]",
            keyVaultResourceId: "[resourceId('Microsoft.KeyVault/vaults', parameters('keyVaultName'))]",
            webAppResourceId: "[resourceId('Microsoft.Web/sites', parameters('webAppName'))]",
        },
        resources,
        outputs: {
            appConfigurationEndpoint: {
                type: "string",
                value: "[format('https://{0}.azconfig.io', parameters('appConfigurationName'))]",
            },
            cosmosEndpoint: {
                type: "string",
                value: "[format('https://{0}.documents.azure.com:443/', parameters('cosmosAccountName'))]",
            },
            keyVaultUri: {
                type: "string",
                value: "[format('https://{0}.vault.azure.net/', parameters('keyVaultName'))]",
            },
            webAppEndpoint: {
                type: "string",
                value: "[format('https://{0}.azurewebsites.net/', parameters('webAppName'))]",
            },
        },
    };
}

function appServicePlanResource(): ArmResource {
    return {
        type: "Microsoft.Web/serverfarms",
        apiVersion: "2023-12-01",
        name: "[parameters('appServicePlanName')]",
        location: "[parameters('location')]",
        kind: "linux",
        sku: {
            capacity: 1,
            family: "Pv3",
            name: "P1v3",
            size: "P1v3",
            tier: "PremiumV3",
        },
        properties: {
            reserved: true,
        },
    };
}

function applicationInsightsResource(): ArmResource {
    return {
        type: "Microsoft.Insights/components",
        apiVersion: "2020-02-02",
        name: "[parameters('applicationInsightsName')]",
        location: "[parameters('location')]",
        kind: "web",
        properties: {
            Application_Type: "web",
            Flow_Type: "Bluefield",
            Request_Source: "rest",
        },
    };
}

function keyVaultResource(): ArmResource {
    return {
        type: "Microsoft.KeyVault/vaults",
        apiVersion: "2023-07-01",
        name: "[parameters('keyVaultName')]",
        location: "[parameters('location')]",
        properties: {
            enablePurgeProtection: true,
            enableRbacAuthorization: true,
            enableSoftDelete: true,
            sku: {
                family: "A",
                name: "standard",
            },
            tenantId: "[parameters('tenantId')]",
        },
    };
}

function appConfigurationResource(): ArmResource {
    return {
        type: "Microsoft.AppConfiguration/configurationStores",
        apiVersion: "2024-06-01",
        name: "[parameters('appConfigurationName')]",
        location: "[parameters('location')]",
        sku: {
            name: "standard",
        },
        properties: {
            dataPlaneProxy: {
                authenticationMode: "Pass-through",
            },
            disableLocalAuth: true,
        },
    };
}

function cosmosAccountResource(): ArmResource {
    return {
        type: "Microsoft.DocumentDB/databaseAccounts",
        apiVersion: "2024-05-15",
        name: "[parameters('cosmosAccountName')]",
        location: "[parameters('location')]",
        kind: "GlobalDocumentDB",
        properties: {
            capabilities: [{ name: "EnableServerless" }],
            consistencyPolicy: {
                defaultConsistencyLevel: "BoundedStaleness",
                maxIntervalInSeconds: 300,
                maxStalenessPrefix: 100000,
            },
            databaseAccountOfferType: "Standard",
            disableLocalAuth: true,
            locations: [
                {
                    failoverPriority: 0,
                    isZoneRedundant: false,
                    locationName: "[parameters('location')]",
                },
            ],
        },
    };
}

function cosmosDatabaseResource(): ArmResource {
    return {
        type: "Microsoft.DocumentDB/databaseAccounts/sqlDatabases",
        apiVersion: "2024-05-15",
        name: "[format('{0}/{1}', parameters('cosmosAccountName'), parameters('cosmosDatabaseName'))]",
        dependsOn: ["[variables('cosmosAccountResourceId')]"],
        properties: {
            resource: {
                id: "[parameters('cosmosDatabaseName')]",
            },
        },
    };
}

function cosmosContainerResource(container: CosmosContainerConfig): ArmResource {
    return {
        type: "Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers",
        apiVersion: "2024-05-15",
        name: `[format('{0}/{1}/${container.name}', parameters('cosmosAccountName'), parameters('cosmosDatabaseName'))]`,
        dependsOn: [
            "[resourceId('Microsoft.DocumentDB/databaseAccounts/sqlDatabases', parameters('cosmosAccountName'), parameters('cosmosDatabaseName'))]",
        ],
        properties: {
            resource: {
                id: container.name,
                partitionKey: {
                    kind: "Hash",
                    paths: [container.partitionKeyPath],
                },
            },
        },
    };
}

function webAppResource(): ArmResource {
    return {
        type: "Microsoft.Web/sites",
        apiVersion: "2023-12-01",
        name: "[parameters('webAppName')]",
        location: "[parameters('location')]",
        kind: "app,linux",
        identity: {
            type: "SystemAssigned",
        },
        dependsOn: ["[variables('appServicePlanResourceId')]", "[variables('applicationInsightsResourceId')]"],
        properties: {
            httpsOnly: true,
            reserved: true,
            serverFarmId: "[variables('appServicePlanResourceId')]",
            siteConfig: siteConfig("[parameters('environmentName')]"),
        },
    };
}

function productionSlotResource(): ArmResource {
    return {
        type: "Microsoft.Web/sites/slots",
        apiVersion: "2023-12-01",
        name: "[format('{0}/{1}', parameters('webAppName'), parameters('productionSlotName'))]",
        location: "[parameters('location')]",
        kind: "app,linux",
        condition: "[parameters('createProductionSlot')]",
        identity: {
            type: "SystemAssigned",
        },
        dependsOn: ["[variables('webAppResourceId')]"],
        properties: {
            httpsOnly: true,
            reserved: true,
            serverFarmId: "[variables('appServicePlanResourceId')]",
            siteConfig: siteConfig("production"),
        },
    };
}

function siteConfig(environmentName: string): Record<string, unknown> {
    return {
        alwaysOn: true,
        appSettings: [
            {
                name: "APPLICATIONINSIGHTS_CONNECTION_STRING",
                value: "[reference(variables('applicationInsightsResourceId'), '2020-02-02').ConnectionString]",
            },
            { name: "AZURE_APP_CONFIG_ENDPOINT", value: "[format('https://{0}.azconfig.io', parameters('appConfigurationName'))]" },
            { name: "ENVIRONMENT_NAME", value: environmentName },
            { name: "SCM_DO_BUILD_DURING_DEPLOYMENT", value: "true" },
            { name: "WEBSITE_ENABLE_SYNC_UPDATE_SITE", value: "true" },
            { name: "WEBSITE_NODE_DEFAULT_VERSION", value: "~24" },
        ],
        http20Enabled: true,
        linuxFxVersion: "NODE|24-lts",
        minimumElasticInstanceCount: 1,
    };
}

function cosmosDataContributorAssignmentForWebApp(): ArmResource {
    return cosmosSqlRoleAssignmentResource({
        nameSeed: "webapp-cosmos-data-contributor",
        principalId: "[reference(variables('webAppResourceId'), '2023-12-01', 'Full').identity.principalId]",
        principalNameSeed: "[variables('webAppResourceId')]",
        dependsOn: ["[variables('cosmosAccountResourceId')]", "[variables('webAppResourceId')]"],
    });
}

function cosmosDataContributorAssignmentForAssignee(): ArmResource {
    return cosmosSqlRoleAssignmentResource({
        nameSeed: "assignee-cosmos-data-contributor",
        principalId: "[parameters('assigneeObjectId')]",
        condition: "[not(empty(parameters('assigneeObjectId')))]",
        dependsOn: ["[variables('cosmosAccountResourceId')]"],
    });
}

function cosmosSqlRoleAssignmentResource(options: {
    readonly nameSeed: string;
    readonly principalId: ArmExpression;
    readonly principalNameSeed?: ArmExpression;
    readonly dependsOn: readonly ArmExpression[];
    readonly condition?: ArmExpression;
}): ArmResource {
    const principalNameSeedExpression = armExpressionArgument(options.principalNameSeed ?? options.principalId);

    return removeUndefinedProperties({
        type: "Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments",
        apiVersion: "2024-05-15",
        name: `[format('{0}/{1}', parameters('cosmosAccountName'), guid(variables('cosmosAccountResourceId'), ${principalNameSeedExpression}, '${options.nameSeed}'))]`,
        condition: options.condition,
        dependsOn: options.dependsOn,
        properties: {
            principalId: options.principalId,
            roleDefinitionId: "[variables('cosmosDataContributorRoleDefinitionId')]",
            scope: "[variables('cosmosAccountResourceId')]",
        },
    });
}

function appConfigurationDataReaderAssignmentForWebApp(): ArmResource {
    return azureRoleAssignmentResource({
        nameSeed: "webapp-appconfig-data-reader",
        principalId: "[reference(variables('webAppResourceId'), '2023-12-01', 'Full').identity.principalId]",
        principalNameSeed: "[variables('webAppResourceId')]",
        principalType: "ServicePrincipal",
        roleDefinitionId: roleDefinitionIds.appConfigurationDataReader,
        scope: "[format('Microsoft.AppConfiguration/configurationStores/{0}', parameters('appConfigurationName'))]",
        dependsOn: ["[variables('appConfigurationResourceId')]", "[variables('webAppResourceId')]"],
    });
}

function appConfigurationDataOwnerAssignmentForAssignee(): ArmResource {
    return azureRoleAssignmentResource({
        nameSeed: "assignee-appconfig-data-owner",
        principalId: "[parameters('assigneeObjectId')]",
        principalType: "User",
        roleDefinitionId: roleDefinitionIds.appConfigurationDataOwner,
        scope: "[format('Microsoft.AppConfiguration/configurationStores/{0}', parameters('appConfigurationName'))]",
        condition: "[not(empty(parameters('assigneeObjectId')))]",
        dependsOn: ["[variables('appConfigurationResourceId')]"],
    });
}

function keyVaultSecretsUserAssignmentForWebApp(): ArmResource {
    return azureRoleAssignmentResource({
        nameSeed: "webapp-keyvault-secrets-user",
        principalId: "[reference(variables('webAppResourceId'), '2023-12-01', 'Full').identity.principalId]",
        principalNameSeed: "[variables('webAppResourceId')]",
        principalType: "ServicePrincipal",
        roleDefinitionId: roleDefinitionIds.keyVaultSecretsUser,
        scope: "[format('Microsoft.KeyVault/vaults/{0}', parameters('keyVaultName'))]",
        dependsOn: ["[variables('keyVaultResourceId')]", "[variables('webAppResourceId')]"],
    });
}

function keyVaultSecretsOfficerAssignmentForAssignee(): ArmResource {
    return azureRoleAssignmentResource({
        nameSeed: "assignee-keyvault-secrets-officer",
        principalId: "[parameters('assigneeObjectId')]",
        principalType: "User",
        roleDefinitionId: roleDefinitionIds.keyVaultSecretsOfficer,
        scope: "[format('Microsoft.KeyVault/vaults/{0}', parameters('keyVaultName'))]",
        condition: "[not(empty(parameters('assigneeObjectId')))]",
        dependsOn: ["[variables('keyVaultResourceId')]"],
    });
}

function azureRoleAssignmentResource(options: {
    readonly nameSeed: string;
    readonly principalId: ArmExpression;
    readonly principalNameSeed?: ArmExpression;
    readonly principalType: "ServicePrincipal" | "User";
    readonly roleDefinitionId: string;
    readonly scope: ArmExpression;
    readonly dependsOn: readonly ArmExpression[];
    readonly condition?: ArmExpression;
}): ArmResource {
    const principalNameSeedExpression = armExpressionArgument(options.principalNameSeed ?? options.principalId);
    const scopeExpression = armExpressionArgument(options.scope);

    return removeUndefinedProperties({
        type: "Microsoft.Authorization/roleAssignments",
        apiVersion: "2022-04-01",
        name: `[guid(${scopeExpression}, ${principalNameSeedExpression}, '${options.nameSeed}')]`,
        scope: options.scope,
        condition: options.condition,
        dependsOn: options.dependsOn,
        properties: {
            principalId: options.principalId,
            principalType: options.principalType,
            roleDefinitionId: `[subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '${options.roleDefinitionId}')]`,
        },
    });
}

function armExpressionArgument(expression: ArmExpression): string {
    return expression.startsWith("[") && expression.endsWith("]") ? expression.slice(1, -1) : expression;
}

function removeUndefinedProperties(resource: ArmResource): ArmResource {
    return Object.fromEntries(Object.entries(resource).filter(([, value]) => value !== undefined));
}

function isNotFound(error: unknown): boolean {
    return typeof error === "object" && error !== null && "statusCode" in error && error.statusCode === 404;
}

main().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Failed to create API Review Hub resources: ${message}`);

    if (message.includes("Microsoft.Authorization/roleAssignments/write")) {
        console.error(
            "The deployment creates Azure RBAC role assignments. Re-run with an identity that has Owner or User Access Administrator at the target subscription or resource group scope.",
        );
    }

    process.exitCode = 1;
});