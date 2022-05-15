import * as child_process from 'child_process';
import * as fs from 'fs';
import * as path from 'path';

import { AzureBlobClient } from '../utils/blob/AzureBlobClient';
import { GenerateAndBuildOutput } from '../types/taskInputAndOuputSchemaTypes/GenerateAndBuildOutput';
import { logger } from '../utils/logger';
import { SDK } from '../types/commonType';

function getFileListInPackageFolder(packageFolder: string) {
    child_process.execSync('git add .', {
        encoding: 'utf8',
        cwd: packageFolder,
    });
    const files = child_process.execSync('git ls-files', { encoding: 'utf8', cwd: packageFolder }).trim().split('\n');

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
            if (result === 'failed') {
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
            if (result === 'failed') {
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

export type ArtifactPipelineUploaderContext = {
    artifactDir: string;
    language: string;
};

export class ArtifactPipelineUploader {
    private artifactDir: string;
    private language: string;

    constructor(artifactPipelineUploaderContext: ArtifactPipelineUploaderContext) {
        this.validateContext(artifactPipelineUploaderContext);
        this.artifactDir = artifactPipelineUploaderContext.artifactDir;
        this.language = artifactPipelineUploaderContext.language;
    }

    private validateContext(artifactPipelineUploaderContext: ArtifactPipelineUploaderContext) {
        if (
            !artifactPipelineUploaderContext.artifactDir ||
            !fs.existsSync(artifactPipelineUploaderContext.artifactDir)
        ) {
            throw new Error(`Invalid artifactDir`);
        }
        if (!(<any>Object).values(SDK).includes(artifactPipelineUploaderContext.language)) {
            throw new Error(`Invalid language`);
        }
    }

    public async publishSourceCode(generateAndBuildOutputJson: GenerateAndBuildOutput) {
        for (const p of generateAndBuildOutputJson.packages) {
            const result = p.result;
            if (result === 'failed') {
                logger.warn(`Build ${p.packageName} failed, skipped it`);
                continue;
            }
            const packageName = p.packageName;
            const packageFolder = p.packageFolder;

            if (packageFolder && fs.existsSync(packageFolder)) {
                for (const filePath of getFileListInPackageFolder(packageFolder)) {
                    if (fs.existsSync(path.join(packageFolder, filePath))) {
                        const fileDir = path.dirname(filePath);
                        child_process.execSync(
                            `mkdir -p ${this.artifactDir}/${this.language}/${packageName}/${fileDir}`,
                            {
                                encoding: 'utf8',
                            }
                        );
                        child_process.execSync(
                            `cp ${path.join(packageFolder, filePath)} ${this.artifactDir}/${
                                this.language
                            }/${packageName}/${filePath}`,
                            {
                                encoding: 'utf8',
                            }
                        );
                    }
                }
            }
        }
    }

    public async publishArtifacts(generateAndBuildOutputJson: GenerateAndBuildOutput) {
        for (const p of generateAndBuildOutputJson.packages) {
            const result = p.result;
            if (result === 'failed') {
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
                child_process.execSync(`mkdir -p ${this.artifactDir}/${this.language}`, {
                    encoding: 'utf8',
                });
                child_process.execSync(`cp ${artifact} ${this.artifactDir}/${this.language}/${artifactName}`, {
                    encoding: 'utf8',
                });
            }
        }
    }
}
