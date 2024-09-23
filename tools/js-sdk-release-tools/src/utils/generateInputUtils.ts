import path from "path";
import { SDKType } from "../common/types";
import { loadTspConfig } from "../common/utils";
import { RunningEnvironment } from "./runningEnvironment";
import { exists } from "fs-extra";

async function isManagementPlaneModularClient(specFolder: string, typespecProjectFolder: string[] | string | undefined) {
    if (!typespecProjectFolder) {
        return false;
    }
    
    if (Array.isArray(typespecProjectFolder) && (typespecProjectFolder as string[]).length !== 1) {
        throw new Error(`Unexpected typespecProjectFolder length: ${(typespecProjectFolder as string[]).length} (expect 1)`);
    }

    const resolvedRelativeTspFolder = Array.isArray(typespecProjectFolder) ? typespecProjectFolder[0] : typespecProjectFolder as string;
    const tspFolderFromSpecRoot = path.join(specFolder, resolvedRelativeTspFolder);
    const tspConfigPath = path.join(tspFolderFromSpecRoot, 'tspconfig.yaml');
    if (!(await exists(tspConfigPath))) {
        return false;
    }

    const tspConfig = await loadTspConfig(tspFolderFromSpecRoot);
    if (tspConfig?.options?.['@azure-tools/typespec-ts']?.['isModularLibrary'] !== true) {
        return false;
    }
    return true;
}

// TODO: consider add stricter rules for RLC in when update SDK automation for RLC
function getSDKType(isMgmtWithHLC: boolean, isMgmtWithModular: boolean) {    
    if (isMgmtWithHLC) {
        return SDKType.HighLevelClient;
    }
    if (isMgmtWithModular) {
        return SDKType.ModularClient;
    }
    return SDKType.RestLevelClient;
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
    
    const isMgmtWithHLC = isTypeSpecProject ? false : readmeMd!.includes('resource-manager');
    const isMgmtWithModular = await isManagementPlaneModularClient(specFolder, typespecProjectFolder);
    const sdkType = getSDKType(isMgmtWithHLC, isMgmtWithModular);
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