import {RunGenerateAndBuildTaskCliConfig} from "../cliSchema/runGenerateAndBuildTaskCliConfig";
import {getGenerateAndBuildOutput} from '@azure-tools/sdk-generation-lib';
import {requireJsonc} from '@azure-tools/sdk-generation-lib';
import * as fs from "fs";
import * as path from "path";
import {AzureBlobClient} from "../utils/AzureBlobClient";
import {getFileListInPackageFolder} from "../utils/git";

function getFiles(dir: string, files?: string[]) {
    files = files || [];
    for (const f of fs.readdirSync(dir)) {
        const name = path.join(dir, f);
        if (fs.statSync(name).isDirectory()) {
            getFiles(name, files);
        } else {
            files.push(name);
        }
    }
    return files;
}

export async function processGenerateAndBuildOutput(config: RunGenerateAndBuildTaskCliConfig): Promise<{ hasFailedResult: boolean }> {
    const res = {hasFailedResult: false};
    if (!fs.existsSync(config.generateAndBuildOutputJson)) return res;
    const generateAndBuildOutputJson = getGenerateAndBuildOutput(requireJsonc(config.generateAndBuildOutputJson));
    const allPackageFolders: string[] = [];
    for (const p of generateAndBuildOutputJson.packages) {
        const result = p.result;
        if (result === 'failed') {
            res.hasFailedResult = true;
            continue;
        }
        const packageName = p.packageName;
        const paths = p.path;
        const packageFolder = p.packageFolder;
        const changelog = p.changelog;
        const artifacts = p.artifacts;

        allPackageFolders.push(packageFolder);
        // upload generated codes in packageFolder
        const azureBlobClient = new AzureBlobClient(config.azureStorageBlobSasUrl, config.azureBlobContainerName);
        for (const filePath of getFileListInPackageFolder(packageFolder)) {
            if (fs.existsSync(path.join(packageFolder, filePath))) {
                await azureBlobClient.uploadLocal(path.join(packageFolder, filePath), `${config.language}/${config.sdkGenerationName}/${packageName}/${filePath}`);
            }
        }

        for (const artifact of artifacts) {
            const artifactName = path.basename(artifact);
            await azureBlobClient.uploadLocal(artifact, `${config.language}/${config.sdkGenerationName}/${artifactName}`);
        }

        // TODO: Create PR in release
    }

    console.log(`##vso[task.setVariable variable=PackageFolders]${allPackageFolders.join(';')}`);
    return res;
}
