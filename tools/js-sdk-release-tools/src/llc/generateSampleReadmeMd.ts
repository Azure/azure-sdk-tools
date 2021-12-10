import * as fs from "fs";
import * as path from "path";
import { getInputFromCommand, getLatestCodegen} from "./utils";
import {logger} from "../utils/logger";

export async function generateSampleReadmeMd(packageName, packagePath, options: any) {
    const title = options.title? options.title : await getInputFromCommand('title');
    const description = options.description? options.description : await getInputFromCommand('description');
    let inputFile = options['input-file']? options['input-file'] : await getInputFromCommand('input-file');
    if (inputFile.includes(';')) {
        const inputFileArray = inputFile.split(';');
        inputFile = '';
        for (const i of inputFileArray) {
            inputFile = inputFile + '\n  -' + i;
        }
    }
    const packageVersion = options['package-version']? options['package-version'] : await getInputFromCommand('package-version');
    const credentialScopes = options['credential-scopes']? options['credential-scopes'] : await getInputFromCommand('credential-scopes');

    const sampleReadme = `# Azure Sample Readme for RLC

> see https://aka.ms/autorest

## Configuration

\`\`\`yaml
package-name: "${packageName}"
title: ${title}
description: ${description}
generate-metadata: false
license-header: MICROSOFT_MIT_NO_VERSION
output-folder: ../
source-code-folder-path: ./src
input-file: ${inputFile}
package-version: ${packageVersion}
rest-level-client: true
add-credentials: true
credential-scopes: "${credentialScopes}"
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
    logger.logGreen(`${path.join(packagePath, 'swagger', 'README.md')} is generated.`);
    logger.log('');
    logger.logGreen(`You can refer to https://github.com/Azure/azure-sdk-for-js/blob/main/sdk/purview/purview-scanning-rest/swagger/README.md`)
    logger.logGreen('-------------------------------------------------------------');
    logger.log('');
}
