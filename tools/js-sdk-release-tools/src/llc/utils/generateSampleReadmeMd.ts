import * as fs from "fs";
import * as yaml from "js-yaml";
import * as path from "path";
import {
    changeRequiredReadmePath,
    getConfigFromReadmeMd,
    getInputFromCommand,
    getInputFromCommandWithDefaultValue,
    getLatestCodegen
} from "./utils";
import { logger } from "../../utils/logger";

async function writeReadmeMd(packageName: string, packagePath: string, options: any) {
    const sampleReadme = `# Azure Sample Readme for RLC

> see https://aka.ms/autorest

## Configuration

\`\`\`yaml
package-name: "${packageName}"
title: ${options.title}
description: ${options.description}
generate-metadata: true
license-header: MICROSOFT_MIT_NO_VERSION
output-folder: ../
source-code-folder-path: ./src
input-file: ${options.inputFile}
package-version: ${options.packageVersion}
rest-level-client: true
add-credentials: true
credential-scopes: "${options.credentialScopes}"
use-extension:
  "@autorest/typescript": "${await getLatestCodegen(packagePath)}"
\`\`\`
`;
    if (!fs.existsSync(path.join(packagePath, 'swagger'))) {
        fs.mkdirSync(path.join(packagePath, 'swagger'));
    }
    fs.writeFileSync(path.join(packagePath, 'swagger', 'README.md'), sampleReadme, {encoding: 'utf-8'});
    logger.log('');
    logger.logGreen('-------------------------------------------------------------');
    logger.log('');
}

export async function generateSampleReadmeMd(packageName: string, packagePath: string, options: any) {
    const title = options.title ? options.title : await getInputFromCommand('title');
    const description = options.description ? options.description : await getInputFromCommand('description');
    let inputFile = options['input-file'] ? options['input-file'] : await getInputFromCommand('input-file');
    if (inputFile.includes(';')) {
        const inputFileArray = inputFile.split(';');
        inputFile = '';
        for (const i of inputFileArray) {
            inputFile = inputFile + '\n  -' + i;
        }
    }
    const packageVersion = options['package-version'] ? options['package-version'] : await getInputFromCommand('package-version');
    const credentialScopes = options['credential-scopes'] ? options['credential-scopes'] : await getInputFromCommand('credential-scopes');
    await writeReadmeMd(packageName, packagePath, {
        title: title,
        description: description,
        inputFile: inputFile,
        packageVersion: packageVersion,
        credentialScopes: credentialScopes
    });

}

export async function modifyExistingReadmeMd(packageName: string, packagePath: string) {
    logger.logGreen(`${packageName} is found in ${packagePath}, please confirm whether the value is expected?
If yes, please input Enter directly. If not, please enter a new value.`);
    const readme = await getConfigFromReadmeMd(path.join(packagePath, 'swagger', 'README.md'));
    const title = await getInputFromCommandWithDefaultValue('title', readme['title']);
    const description = await getInputFromCommandWithDefaultValue('description', readme['description']);
    let existingInputArray;
    if (Array.isArray(readme['input-file'])) {
        existingInputArray = readme['input-file'].join(';');
    } else {
        existingInputArray = readme['input-file'];
    }
    let inputFile = await getInputFromCommandWithDefaultValue('input-file', existingInputArray);
    if (inputFile.includes(';')) {
        const inputFileArray = inputFile.split(';');
        inputFile = '';
        for (const i of inputFileArray) {
            inputFile = inputFile + '\n  -' + i;
        }
    }

    const packageVersion = await getInputFromCommandWithDefaultValue('package-version', readme['package-version']);
    const credentialScopes = await getInputFromCommandWithDefaultValue('credential-scopes', readme['credential-scopes']);

    await writeReadmeMd(packageName, packagePath, {
        title: title,
        description: description,
        inputFile: inputFile,
        packageVersion: packageVersion,
        credentialScopes: credentialScopes
    });
}

function getPackageNameFromPackageFolder(packageFolderName: string) {
    const regexResult = /(.*)-rest/.exec(packageFolderName);
    if (!regexResult || regexResult.length !== 2) {
        throw new Error(`Get invalid package folder name: ${packageFolderName}`);
    }
    return `@azure-rest/${regexResult[1]}`;
}

export async function generateAutorestConfigurationFileForSingleClientByPrComment(yamlContent: any, swaggerRepo: string, sdkRepo: string): Promise<string> {
    const outputFolderRegex = new RegExp(/sdk\/[^\/]*\/(.*-rest)/);
    const outputFolder = yamlContent['output-folder'];
    if (!outputFolder || !outputFolder.match(outputFolderRegex)) {
        throw new Error(`Get invalid output-folder: ${outputFolder}`);
    }
    const outputFolderMatch = outputFolderRegex.exec(outputFolder);
    if (!outputFolderMatch || outputFolderMatch.length !== 2) {
        throw new Error(`Parse output-folder failed: ${outputFolder}`);
    }
    const outputFolderPath = path.join(sdkRepo, outputFolder);
    const packageFolderName = outputFolderMatch[1];
    let packageName = getPackageNameFromPackageFolder(packageFolderName);
    const requiredReadme = changeRequiredReadmePath(yamlContent['require'], swaggerRepo);

    const autorestConfig = {
        'package-name': packageName,
        'generate-metadata': true,
        'license-header': 'MICROSOFT_MIT_NO_VERSION',
        'output-folder': '../',
        'source-code-folder-path': './src',
        require: requiredReadme,
        'rest-level-client': true,
        'use-extension': {
            '@autorest/typescript': `${await getLatestCodegen(outputFolderPath)}`
        }
    }

    for (const key of Object.keys(yamlContent)) {
        if (!['output-folder', 'require'].includes(key)) {
            autorestConfig[key] = yamlContent[key];
        }
    }

    fs.mkdirSync(path.join(outputFolderPath, 'swagger'), {recursive: true});

    const readmeMd = `# JavaScript

> see https://aka.ms/autorest

## Configuration

\`\`\`yaml
${yaml.dump(autorestConfig, {indent: 2, lineWidth: 200})}\`\`\`  
`;
    const readmeMdPath = path.join(outputFolderPath, 'swagger', 'README.md');
    fs.writeFileSync(readmeMdPath, readmeMd, {encoding: 'utf-8'});
    return readmeMdPath;
}

export async function generateAutorestConfigurationFileForMultiClientByPrComment(yamlBlocks: {
    condition: string;
    yamlContent: any;
}[], swaggerRepo: string, sdkRepo: string): Promise<string> {
    let packageFolderName: string | undefined = undefined;
    let packageName: string | undefined = undefined;
    let outputFolderPath: string | undefined = undefined;
    let requiredReadme: any = undefined;
    for (const block of yamlBlocks) {
        if (block.condition.includes('multi-client')) {
            // change required readme.md path to local path
            requiredReadme = changeRequiredReadmePath(block.yamlContent['require'], swaggerRepo);
            if (!requiredReadme) {
                throw new Error(`Cannot get required readme file in: ${yaml.dump(block.yamlContent, {indent: 2})}`);
            }
        } else {
            // calculate packageName and package outputFolderPath
            if (!block.yamlContent['output-folder']) {
                throw new Error(`Cannot get output-folder from: ${yaml.dump(block.yamlContent, {indent: 2})}`);
            }
            const outputFolderMatches = /sdk\/[^\/]*\/(.*-rest)/gm.exec(block.yamlContent['output-folder']);
            if (!outputFolderMatches || outputFolderMatches.length !== 2) {
                throw new Error(`Get invalid output-folder: ${block.yamlContent['output-folder']}`);
            }
            outputFolderPath = outputFolderMatches[0];
            packageFolderName = outputFolderMatches[1];
            packageName = getPackageNameFromPackageFolder(packageFolderName);
        }
    }
    if (!outputFolderPath) {
        throw new Error(`Get invalid output-folder: ${outputFolderPath}`);
    }
    outputFolderPath = path.join(sdkRepo, outputFolderPath);

    const autorestConfigs: {
        condition: string;
        yamlContent: any;
    }[] = [];

    for (const block of yamlBlocks) {
        let yamlContent;
        if (block.condition.includes('multi-client')) {
            yamlContent = {
                'package-name': packageName,
                'generate-metadata': true,
                'license-header': 'MICROSOFT_MIT_NO_VERSION',
                'rest-level-client': true,
                require: requiredReadme,
                'use-extension': {
                    '@autorest/typescript': `${await getLatestCodegen(outputFolderPath)}`
                }
            }
            for (const key of Object.keys(block.yamlContent)) {
                if (!['require'].includes(key)) {
                    yamlContent[key] = block.yamlContent[key];
                }
            }
        } else {
            yamlContent = {
                'output-folder': '../',
                'source-code-folder-path': `./${path.relative(outputFolderPath, block.yamlContent['output-folder'])}`
            }
            for (const key of Object.keys(block.yamlContent)) {
                if (!['output-folder'].includes(key)) {
                    yamlContent[key] = block.yamlContent[key];
                }
            }
        }
        autorestConfigs.push({
            condition: block.condition,
            yamlContent
        });
    }

    let readmeMd: string[] = [`# JavaScript

> see https://aka.ms/autorest

## Configuration
`];
    for (const config of autorestConfigs) {
        readmeMd.push(`\`\`\`${config.condition}
${yaml.dump(config.yamlContent, {indent: 2, lineWidth: 200})}\`\`\``);
    }

    const readmeMdPath = path.join(outputFolderPath, 'swagger', 'README.md');
    fs.writeFileSync(readmeMdPath, readmeMd.join('\n'), {encoding: 'utf-8'});
    return readmeMdPath;
}

export function replaceRequireInAutorestConfigurationFile(autorestConfigFilePath: string, ori: string, latest: string) {
    const readmeMdContent = fs.readFileSync(autorestConfigFilePath, 'utf-8');
    fs.writeFileSync(autorestConfigFilePath, readmeMdContent.replace(ori, latest), {encoding: 'utf-8'});
}