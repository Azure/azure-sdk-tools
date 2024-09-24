import * as path from "path";
import {logger} from "../../utils/logger";
import {execSync} from "child_process";
import {generateChangelog} from "../utils/generateChangelog";
import {changeConfigOfTestAndSample, ChangeModel, SdkType} from "../../utils/changeConfigOfTestAndSample";
import {generateExtraFiles} from "../utils/generateExtraFiles";
import { defaultChildProcessTimeout } from "../../common/utils";

const shell = require('shelljs')

export async function generateCodes(sdkRepo: string, packagePath: string, packageName: string) {
    let cmd = `autorest  --typescript README.md`;
    shell.cd(path.join(packagePath, 'swagger'));
    logger.info(`Start to run command: ${cmd}.`);
    execSync(cmd, {stdio: 'inherit', timeout: defaultChildProcessTimeout});
    logger.info(`Start to generate config files.`);
    shell.cd(packagePath);
    await generateExtraFiles(packagePath, packageName, sdkRepo);
}

export async function buildGeneratedCodes(sdkrepo: string, packagePath: string, packageName: string) {
    shell.cd(sdkrepo);
    logger.info(`Start to update rush.`);
    execSync('node common/scripts/install-run-rush.js update', {stdio: 'inherit'});
    logger.info(`Start to build '${packageName}', except for tests and samples, which may be written manually`);
    // To build generated codes except test and sample, we need to change tsconfig.json.
    changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Rlc);
    execSync(`node common/scripts/install-run-rush.js build -t ${packageName}`, {stdio: 'inherit'});
    changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Rlc);
    shell.cd(packagePath);
    logger.info(`Start to Generate changelog.`);
    await generateChangelog(packagePath);
    logger.info(`Start to clean compiled outputs.`);
    execSync('rushx clean', {stdio: 'inherit'});
}
