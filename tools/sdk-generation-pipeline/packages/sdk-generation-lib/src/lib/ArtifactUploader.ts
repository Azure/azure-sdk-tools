import * as childProcess from 'child_process';
import * as fs from 'fs';
import * as path from 'path';

import { SDK } from '../types/commonType';
import { GenerateAndBuildOutput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildOutput';
import { TaskResultStatus } from '../types/taskResult';
import { AzureBlobClient } from '../utils/blob/AzureBlobClient';
import { logger } from '../utils/logger';

function getFileListInPackageFolder(packageFolder: string) {
    const files = childProcess
        .execSync('git ls-files -cmo --exclude-standard', { encoding: 'utf8', cwd: packageFolder })
        .trim()
        .split('\n');

    return files;
}

export type ArtifactBlobUploaderContext = {
    azureStorageBlobSasUrl: string;
    azureBlobContainerName: string;
    language: string;
    pipelineBuildId: string;
};

export class ArtifactBlobUploader {
    private azureBlobClient: AzureBlobClient;
    private language: string;
    private pipelineBuildId: string;

    constructor(artifactBlobUploaderContext: ArtifactBlobUploaderContext) {
        this.validateContext(artifactBlobUploaderContext);
        this.azureBlobClient = new AzureBlobClient(
            artifactBlobUploaderContext.azureStorageBlobSasUrl,
            artifactBlobUploaderContext.azureBlobContainerName
        );
        this.language = artifactBlobUploaderContext.language;
        this.pipelineBuildId = artifactBlobUploaderContext.pipelineBuildId;
    }

    private validateContext(artifactBlobUploaderContext: ArtifactBlobUploaderContext) {
        if (!artifactBlobUploaderContext.azureStorageBlobSasUrl) {
            throw new Error(`Invalid azureStorageBlobSasUrl`);
        }
        if (!artifactBlobUploaderContext.azureBlobContainerName) {
            throw new Error(`Invalid azureBlobContainerName`);
        }
        if (!(<any>Object).values(SDK).includes(artifactBlobUploaderContext.language)) {
            throw new Error(`Invalid language`);
        }
        if (!artifactBlobUploaderContext.pipelineBuildId) {
            throw new Error(`Invalid pipelineBuildId`);
        }
    }

    public async uploadSourceCode(generateAndBuildOutputJson: GenerateAndBuildOutput) {
        for (const p of generateAndBuildOutputJson.packages) {
            const result = p.result;
            if (result === TaskResultStatus.Failure) {
                logger.warn(`Build ${p.packageName} failed, skipped it`);
                continue;
            }
            const packageName = p.packageName;
            const packageFolder = p.packageFolder;

            if (packageFolder && fs.existsSync(packageFolder)) {
                for (const filePath of getFileListInPackageFolder(packageFolder)) {
                    if (fs.existsSync(path.join(packageFolder, filePath))) {
                        await this.azureBlobClient.publishBlob(
                            path.join(packageFolder, filePath),
                            `${this.pipelineBuildId}/${this.language}/${packageName}/${filePath}`
                        );
                    }
                }
            }
        }
    }

    public async uploadArtifacts(generateAndBuildOutputJson: GenerateAndBuildOutput) {
        for (const p of generateAndBuildOutputJson.packages) {
            const result = p.result;
            if (result === TaskResultStatus.Failure) {
                logger.warn(`Build ${p.packageName} failed, skipped it`);
                continue;
            }
            const artifacts = p.artifacts;
            if (!artifacts) {
                // artifacts is optional
                continue;
            }

            for (const artifact of artifacts) {
                const artifactName = path.basename(artifact);
                await this.azureBlobClient.publishBlob(
                    artifact,
                    `${this.pipelineBuildId}/${this.language}/${artifactName}`
                );
            }
        }
    }
}
