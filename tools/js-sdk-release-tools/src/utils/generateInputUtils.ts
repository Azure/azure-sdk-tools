import path from "path";
import { SDKType } from "../common/types";
import { loadTspConfig } from "../common/utils";
import { RunningEnvironment } from "./runningEnvironment";
import { exists } from "fs-extra";

export async function getSDKType(
    specFolder: string,
    readme: string | undefined,
    typespecProjectFolder: string | undefined
): Promise<SDKType> {
    if (readme) {
        // Update?
        if (readme.includes("resource-manager")) return SDKType.HighLevelClient;
        return SDKType.RestLevelClient;
    }

    if (!typespecProjectFolder) {
        const rootCause = `Failed to get SDK Type due to neither README or typespecProjectFolder is undefined.`;
        throw new Error(rootCause);
    }

    const tspFolderFromSpecRoot = path.join(specFolder, typespecProjectFolder);

    const tspConfigPath = path.join(tspFolderFromSpecRoot, "tspconfig.yaml");
    if (!(await exists(tspConfigPath))) {
        const rootCause = `Failed to get SDK Type due to tspconfig.yaml doesn't exist in ${tspFolderFromSpecRoot}.`;
        throw new Error(rootCause);
    }

    const tspConfig = await loadTspConfig(tspFolderFromSpecRoot);
    const isModularLibrary =
        tspConfig?.options?.["@azure-tools/typespec-ts"]?.["isModularLibrary"];

    // NOTE: respect customer's choice
    if (isModularLibrary === true) return SDKType.ModularClient;
    if (isModularLibrary === false) return SDKType.RestLevelClient;

    const isAzureSDK =
        tspConfig?.options?.["@azure-tools/typespec-ts"]?.["flavor"] ===
        "azure";
    // NOTE: unbranded sdk will generate modular client bt default
    if (!isAzureSDK) return SDKType.ModularClient;

    const isManagementPlane = new RegExp(/\.Management[\/\\]?$/).test(
        tspFolderFromSpecRoot
    );
    // NOTE: management plane will generate modular client by default
    return isManagementPlane ? SDKType.ModularClient : SDKType.RestLevelClient;
}

// TODO: generate interface for inputJson
export async function parseInputJson(inputJson: any) {
    // inputJson schema: https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/sdkautomation/GenerateInputSchema.json
    // todo: add interface for the schema
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] | string | undefined = inputJson['relatedReadmeMdFiles'] ? inputJson['relatedReadmeMdFiles'] : inputJson['relatedReadmeMdFile'];
    const typespecProjectFolder: string[] | string | undefined = inputJson['relatedTypeSpecProjectFolder'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];
    const autorestConfig: string | undefined = inputJson['autorestConfig'];
    const downloadUrlPrefix: string | undefined = inputJson.installInstructionInput?.downloadUrlPrefix;
    // TODO: consider remove it, since it's not defined in inputJson schema
    const skipGeneration: boolean | undefined = inputJson['skipGeneration'];

    if (!readmeFiles && !typespecProjectFolder) {
        throw new Error(`readme files and typespec project info are both undefined`);
    }

    if (typespecProjectFolder && typeof typespecProjectFolder !== 'string' && typespecProjectFolder.length !== 1) {
        throw new Error(`get ${typespecProjectFolder.length} typespec project`);
    }

    const isTypeSpecProject = !!typespecProjectFolder;

    const packages: any[] = [];
    const outputJson = {
        packages: packages,
        language: 'JavaScript',
    };
    const readmeMd = isTypeSpecProject ? undefined : typeof readmeFiles === 'string' ? readmeFiles : readmeFiles![0];
    const typespecProject = isTypeSpecProject ? typeof typespecProjectFolder === 'string' ? typespecProjectFolder : typespecProjectFolder![0] : undefined;
    const runningEnvironment = typeof readmeFiles === 'string' || typeof typespecProjectFolder === 'string' ? RunningEnvironment.SdkGeneration : RunningEnvironment.SwaggerSdkAutomation;
    
    const sdkType = await getSDKType(specFolder, readmeMd, typespecProject);
    return {
        sdkType,
        specFolder,
        gitCommitId,
        repoHttpsUrl,
        autorestConfig,
        downloadUrlPrefix,
        readmeMd,
        outputJson,
        skipGeneration,
        runningEnvironment,
        typespecProject
    };
}